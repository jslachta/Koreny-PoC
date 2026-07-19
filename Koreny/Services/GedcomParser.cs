using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Dvoufázový GEDCOM parser (viz docs/audit-gedcom.md, sekce 4).
///
/// Fáze 1 (<see cref="BuildTree"/>): řádky → strom <see cref="GedcomNode"/>. Nic se
/// nezahazuje — HEAD, SUBM, SOUR, OBJE, NOTE i neznámé/proprietární záznamy skončí
/// v <see cref="GedcomDocument.Nodes"/>. CONC/CONT se slévají do hodnoty nadřazeného
/// uzlu ZDE a jen zde.
///
/// Fáze 2 (<see cref="Project"/>): promítne rozpoznané uzly (INDI/FAM a jejich známé
/// podtagy) do doménového modelu; každý <see cref="GedcomIndividual"/>/<see cref="GedcomFamily"/>
/// si drží referenci na svůj <see cref="GedcomNode"/>. Rozpoznávání tagů odpovídá
/// původnímu parseru (poslední výskyt vyhrává u NAME/BIRT/DEAT/MARR, NOTE se hromadí).
/// </summary>
public class GedcomParser
{
    public GedcomDocument Parse(string content)
    {
        var doc = new GedcomDocument();
        var roots = BuildTree(content);
        doc.Nodes.AddRange(roots);
        Project(doc, roots);
        return doc;
    }

    // ------------------------------------------------------------- fáze 1: strom

    private static List<GedcomNode> BuildTree(string content)
    {
        var roots = new List<GedcomNode>();
        var stack = new Stack<(int Level, GedcomNode Node)>();

        foreach (var rawLine in SplitLines(content))
        {
            if (!TryParseLine(rawLine, out var level, out var tag, out var value, out var xref))
            {
                continue;
            }

            if (tag is "CONC" or "CONT")
            {
                // Slití do nejbližšího předka s nižším levelem (nositel hodnoty).
                while (stack.Count > 0 && stack.Peek().Level >= level)
                {
                    stack.Pop();
                }

                if (stack.Count > 0)
                {
                    var target = stack.Peek().Node;
                    target.Value = tag == "CONC" ? target.Value + value : target.Value + "\n" + value;
                }

                continue;
            }

            while (stack.Count > 0 && stack.Peek().Level >= level)
            {
                stack.Pop();
            }

            var node = new GedcomNode(tag, xref, value);
            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Node.Children.Add(node);
            }

            stack.Push((level, node));
        }

        return roots;
    }

    // ------------------------------------------------------------- fáze 2: projekce

    private static void Project(GedcomDocument doc, List<GedcomNode> roots)
    {
        foreach (var node in roots)
        {
            if (node.Tag == "INDI" && node.Xref is not null)
            {
                var indi = new GedcomIndividual { Id = node.Xref, SourceNode = node };
                ProjectIndividual(indi, node);
                doc.Individuals.Add(indi);
            }
            else if (node.Tag == "FAM" && node.Xref is not null)
            {
                var fam = new GedcomFamily { Id = node.Xref, SourceNode = node };
                ProjectFamily(fam, node);
                doc.Families.Add(fam);
            }

            // Ostatní záznamy levelu 0 zůstávají jen v doc.Nodes (nosič pravdy pro export).
        }
    }

    private static void ProjectIndividual(GedcomIndividual indi, GedcomNode node)
    {
        foreach (var c in node.Children)
        {
            switch (c.Tag)
            {
                case "NAME":
                    indi.Name = GedcomNameParser.Parse(c.Value); // poslední výskyt vyhrává
                    break;
                case "SEX":
                    indi.Sex = c.Value.Trim();
                    break;
                case "BIRT":
                    indi.Birth = ProjectEvent(c);
                    break;
                case "DEAT":
                    indi.Death = ProjectEvent(c);
                    break;
                case "NOTE":
                    indi.Notes.Add(c.Value);
                    break;
            }
        }
    }

    private static void ProjectFamily(GedcomFamily fam, GedcomNode node)
    {
        foreach (var c in node.Children)
        {
            switch (c.Tag)
            {
                case "HUSB":
                    fam.HusbandId = StripXref(c.Value);
                    break;
                case "WIFE":
                    fam.WifeId = StripXref(c.Value);
                    break;
                case "CHIL":
                    var id = StripXref(c.Value);
                    if (id is not null)
                    {
                        fam.ChildrenIds.Add(id);
                    }

                    break;
                case "MARR":
                    fam.Marriage = ProjectEvent(c); // poslední výskyt vyhrává
                    break;
                case "NOTE":
                    fam.Notes.Add(c.Value);
                    break;
            }
        }
    }

    /// <summary>Událost existuje, jakmile je přítomen její uzel (i „BIRT Y“ bez data); DATE/PLAC jsou volitelné.</summary>
    private static GedcomEvent ProjectEvent(GedcomNode eventNode)
    {
        var ev = new GedcomEvent();
        var date = eventNode.FirstChild("DATE");
        if (date is not null)
        {
            ev.Date = date.Value.Trim();
        }

        var plac = eventNode.FirstChild("PLAC");
        if (plac is not null)
        {
            ev.Place = plac.Value.Trim();
        }

        return ev;
    }

    // ------------------------------------------------------------- řádkový lexer

    private static IEnumerable<string> SplitLines(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    /// <summary>
    /// Rozloží fyzický řádek na level/xref/tag/value. Fyzický řádek se ořezává na obou
    /// koncích (odsazení i koncové mezery jsou dle 5.5.1 nespolehlivé), ale v hodnotě se
    /// zachovávají mezery za PRVNÍM oddělovačem po tagu — jinak by se ztratila mezera na
    /// hranici lámání CONC (např. „2 CONC  kterou“).
    /// </summary>
    private static bool TryParseLine(string line, out int level, out string tag, out string value, out string? xref)
    {
        level = 0;
        tag = string.Empty;
        value = string.Empty;
        xref = null;

        line = line.Trim();
        if (line.Length == 0)
        {
            return false;
        }

        var i = 0;
        while (i < line.Length && char.IsDigit(line[i]))
        {
            i++;
        }

        if (i == 0)
        {
            return false;
        }

        level = int.Parse(line.AsSpan(0, i), System.Globalization.NumberStyles.Integer, null);

        if (i >= line.Length || (line[i] != ' ' && line[i] != '\t'))
        {
            return false;
        }

        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        if (i >= line.Length)
        {
            return false;
        }

        var rest = line[i..];
        if (rest[0] == '@')
        {
            var end = rest.IndexOf('@', 1);
            if (end < 0)
            {
                return false;
            }

            xref = rest[1..end];
            rest = rest[(end + 1)..].TrimStart(' ', '\t');
            if (rest.Length == 0)
            {
                return false;
            }
        }

        var sep = rest.IndexOf(' ');
        if (sep < 0)
        {
            tag = rest;
            value = string.Empty;
            return true;
        }

        tag = rest[..sep];
        // Jen jediný oddělovač; další mezery jsou součástí hodnoty (leading mezery zůstávají).
        value = rest[(sep + 1)..];
        return true;
    }

    private static string? StripXref(string raw)
    {
        var t = raw.Trim();
        if (t.Length >= 2 && t[0] == '@' && t[^1] == '@')
        {
            return t[1..^1];
        }

        return t.Length > 0 ? t : null;
    }
}

internal static class GedcomNameParser
{
    internal static GedcomName Parse(string raw)
    {
        var name = new GedcomName { Raw = raw };
        var s = raw.Trim();
        var first = s.IndexOf('/');
        if (first < 0)
        {
            name.GivenName = s.Length > 0 ? s : null;
            return name;
        }

        var second = s.IndexOf('/', first + 1);
        if (second < 0)
        {
            name.GivenName = s.Length > 0 ? s : null;
            return name;
        }

        // Slash-delimited surname is the standard GEDCOM convention; avoids guessing on proprietary NAME extensions.
        name.Surname = s.Substring(first + 1, second - first - 1).Trim();
        var given = (s[..first] + " " + s[(second + 1)..]).Trim();
        name.GivenName = given.Length > 0 ? given : null;
        return name;
    }
}
