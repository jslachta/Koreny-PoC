using System.Text;

namespace Koreny.Tests;

public enum GedcomDiffKind
{
    /// <summary>Uzel existuje v originálu, ale chybí v exportu.</summary>
    MissingNode,

    /// <summary>Uzel existuje v exportu, ale ne v originálu.</summary>
    ExtraNode,

    /// <summary>Uzel existuje na obou stranách, ale liší se (slitá) hodnota.</summary>
    ValueMismatch,

    /// <summary>Obě strany mají tytéž uzly, ale v jiném pořadí.</summary>
    OrderMismatch,
}

public sealed record GedcomDiffEntry(GedcomDiffKind Kind, string Path, string Detail)
{
    public override string ToString() => $"[{Kind}] {Path}: {Detail}";
}

public sealed class GedcomDiffReport
{
    public List<GedcomDiffEntry> Entries { get; } = new();

    public bool IsEmpty => Entries.Count == 0;

    public override string ToString() =>
        IsEmpty
            ? "(žádné rozdíly)"
            : $"{Entries.Count} rozdílů:{Environment.NewLine}" + string.Join(Environment.NewLine, Entries);
}

/// <summary>
/// Sémantické porovnání dvou GEDCOM textů jako stromů (ne jako řádků).
///
/// Záměrně NEPOUŽÍVÁ <c>Koreny.Services.GedcomParser</c> — má vlastní minimální reader,
/// aby se parser aplikace netestoval sám sebou.
///
/// Normalizace (rozdíly, které NEJSOU chybou):
///  a) konce řádků LF/CRLF/CR (řeší <see cref="StringReader.ReadLine"/>),
///  b) CONC/CONT se slévají do logické hodnoty nadřazeného uzlu
///     (CONT = nový řádek "\n", CONC = přímé napojení bez vkládané mezery);
///     porovnává se slitá hodnota, fyzické lámání se ignoruje,
///  c) trailing whitespace fyzických řádků se ořezává,
///  d) uvnitř HEAD se uzly s tagem SOUR/VERS/DATE/TIME/FILE ignorují VČETNĚ podstromů
///     (aplikace smí exporty razítkovat vlastním SOUR/VERS/DATE); zbytek struktury
///     hlavičky (SUBM, LANG, CHAR, PLAC/FORM, GEDC/FORM…) se porovnává normálně.
///
/// Rozhodnutí u nejednoznačností GEDCOM 5.5.1 (součást kontraktu diffu):
///  1) CONC smí následovat i po prázdné hodnotě tagu — napojuje se přímo, bez mezery.
///     Spec to výslovně nezakazuje a reálné exporty to produkují.
///  2) Trailing whitespace se ořezává PŘED slitím CONC/CONT. Spec 5.5.1 varuje, že na
///     koncové mezery se nelze spolehnout; mezera na hranici lámání proto musí být na
///     ZAČÁTKU hodnoty CONC řádku (korpus je tak psán). Leading mezery hodnoty za
///     jediným oddělovačem po tagu se zachovávají.
///  3) Odsazené řádky (leading whitespace před levelem) se tolerují, přestože je 5.5.1
///     zakazuje — reálné soubory je obsahují a tolerantní je i parser aplikace.
///  4) Prázdné a nerozparsovatelné řádky se přeskakují; validace syntaxe není úkolem diffu.
///  5) CONC/CONT, který nenavazuje přímo na nositele hodnoty, se přisoudí nejbližšímu
///     předkovi s nižším levelem (lenientní chování; u validních souborů shodné se spec).
/// </summary>
public static class GedcomSemanticDiff
{
    private static readonly HashSet<string> HeadStampTags =
        new(StringComparer.Ordinal) { "SOUR", "VERS", "DATE", "TIME", "FILE" };

    public static GedcomDiffReport Compare(string expectedGedcom, string actualGedcom)
    {
        var report = new GedcomDiffReport();
        var expected = ParseTree(expectedGedcom);
        var actual = ParseTree(actualGedcom);

        foreach (var r in expected.Where(r => r.Tag == "HEAD"))
        {
            PruneHeadStamps(r);
        }

        foreach (var r in actual.Where(r => r.Tag == "HEAD"))
        {
            PruneHeadStamps(r);
        }

        CompareSiblings(report, parentPath: null, expected, actual, isRecordLevel: true);
        return report;
    }

    // ---------------------------------------------------------------- reader

    private sealed class Node
    {
        public int Level;
        public string? Xref;
        public string Tag = string.Empty;
        public string Value = string.Empty;
        public readonly List<Node> Children = new();
    }

    private static List<Node> ParseTree(string content)
    {
        var roots = new List<Node>();
        var stack = new Stack<Node>();
        using var reader = new StringReader(content);
        string? raw;
        var firstLine = true;

        while ((raw = reader.ReadLine()) is not null)
        {
            var line = raw;
            if (firstLine)
            {
                // BOM: StreamReader ho typicky odstraní, ale text může přijít i z jiného zdroje.
                line = line.TrimStart((char)0xFEFF);
                firstLine = false;
            }

            line = line.TrimEnd();
            var t = line.TrimStart(' ', '\t');
            if (t.Length == 0)
            {
                continue;
            }

            var i = 0;
            while (i < t.Length && char.IsDigit(t[i]))
            {
                i++;
            }

            if (i == 0 || i >= t.Length || (t[i] != ' ' && t[i] != '\t'))
            {
                continue;
            }

            var level = int.Parse(t.AsSpan(0, i));
            var rest = t[(i + 1)..].TrimStart(' ', '\t');

            string? xref = null;
            if (rest.StartsWith('@'))
            {
                var end = rest.IndexOf('@', 1);
                if (end > 0)
                {
                    xref = rest[1..end];
                    rest = rest[(end + 1)..].TrimStart(' ', '\t');
                }
            }

            string tag;
            string value;
            var sep = rest.IndexOf(' ');
            if (sep < 0)
            {
                tag = rest;
                value = string.Empty;
            }
            else
            {
                tag = rest[..sep];
                // Jen jediný oddělovač; další mezery jsou součástí hodnoty (viz rozhodnutí 2).
                value = rest[(sep + 1)..];
            }

            if (tag is "CONC" or "CONT")
            {
                while (stack.Count > 0 && stack.Peek().Level >= level)
                {
                    stack.Pop();
                }

                if (stack.Count > 0)
                {
                    var target = stack.Peek();
                    target.Value = tag == "CONC" ? target.Value + value : target.Value + "\n" + value;
                }

                continue;
            }

            while (stack.Count > 0 && stack.Peek().Level >= level)
            {
                stack.Pop();
            }

            var node = new Node { Level = level, Xref = xref, Tag = tag, Value = value };
            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
        }

        return roots;
    }

    private static void PruneHeadStamps(Node node)
    {
        node.Children.RemoveAll(c => HeadStampTags.Contains(c.Tag));
        foreach (var c in node.Children)
        {
            PruneHeadStamps(c);
        }
    }

    // ------------------------------------------------------------- porovnání

    /// <summary>Klíč pro párování: na úrovni záznamů „@I1@ INDI“, u potomků jen tag.</summary>
    private static string KeyOf(Node n, bool isRecordLevel) =>
        isRecordLevel && n.Xref is not null ? $"@{n.Xref}@ {n.Tag}" : n.Tag;

    private static void CompareSiblings(
        GedcomDiffReport report,
        string? parentPath,
        List<Node> expected,
        List<Node> actual,
        bool isRecordLevel)
    {
        // Páruje se i-tý výskyt klíče s i-tým výskytem téhož klíče na druhé straně.
        var expByKey = GroupByKey(expected, isRecordLevel);
        var actByKey = GroupByKey(actual, isRecordLevel);

        var pairedExp = new HashSet<Node>();
        var pairedAct = new HashSet<Node>();

        foreach (var (key, expNodes) in expByKey)
        {
            actByKey.TryGetValue(key, out var actNodes);
            var actCount = actNodes?.Count ?? 0;
            var pairCount = Math.Min(expNodes.Count, actCount);
            var multi = Math.Max(expNodes.Count, actCount) > 1;

            for (var i = 0; i < expNodes.Count; i++)
            {
                var path = ChildPath(parentPath, key, multi ? i : (int?)null);
                if (i >= pairCount)
                {
                    report.Entries.Add(new GedcomDiffEntry(
                        GedcomDiffKind.MissingNode,
                        path,
                        $"v exportu chybí uzel s hodnotou {Display(expNodes[i].Value)}"));
                    continue;
                }

                var expNode = expNodes[i];
                var actNode = actNodes![i];
                pairedExp.Add(expNode);
                pairedAct.Add(actNode);

                if (!string.Equals(expNode.Value, actNode.Value, StringComparison.Ordinal))
                {
                    report.Entries.Add(new GedcomDiffEntry(
                        GedcomDiffKind.ValueMismatch,
                        path,
                        $"očekáváno {Display(expNode.Value)}, exportováno {Display(actNode.Value)}"));
                }

                CompareSiblings(report, path, expNode.Children, actNode.Children, isRecordLevel: false);
            }
        }

        foreach (var (key, actNodes) in actByKey)
        {
            expByKey.TryGetValue(key, out var expNodes);
            var expCount = expNodes?.Count ?? 0;
            var multi = Math.Max(actNodes.Count, expCount) > 1;
            for (var i = expCount; i < actNodes.Count; i++)
            {
                report.Entries.Add(new GedcomDiffEntry(
                    GedcomDiffKind.ExtraNode,
                    ChildPath(parentPath, key, multi ? i : (int?)null),
                    $"v exportu přebývá uzel s hodnotou {Display(actNodes[i].Value)}"));
            }
        }

        // Kontrola pořadí sourozenců: porovnává se posloupnost klíčů SPÁROVANÝCH uzlů.
        var expOrder = expected.Where(pairedExp.Contains).Select(n => KeyOf(n, isRecordLevel)).ToList();
        var actOrder = actual.Where(pairedAct.Contains).Select(n => KeyOf(n, isRecordLevel)).ToList();
        if (!expOrder.SequenceEqual(actOrder, StringComparer.Ordinal))
        {
            report.Entries.Add(new GedcomDiffEntry(
                GedcomDiffKind.OrderMismatch,
                parentPath ?? "(záznamy levelu 0)",
                $"pořadí v originálu: [{string.Join(", ", expOrder)}]; v exportu: [{string.Join(", ", actOrder)}]"));
        }
    }

    private static Dictionary<string, List<Node>> GroupByKey(List<Node> nodes, bool isRecordLevel)
    {
        var dict = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
        foreach (var n in nodes)
        {
            var key = KeyOf(n, isRecordLevel);
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<Node>();
                dict[key] = list;
            }

            list.Add(n);
        }

        return dict;
    }

    private static string ChildPath(string? parentPath, string key, int? occurrence)
    {
        var segment = occurrence is null ? key : $"{key}[{occurrence.Value}]";
        return parentPath is null ? segment : $"{parentPath} > {segment}";
    }

    private static string Display(string value)
    {
        var v = value.Replace("\n", "\\n", StringComparison.Ordinal);
        if (v.Length > 80)
        {
            // Délka odliší hodnoty, které se po oříznutí zobrazují shodně (dlouhý společný prefix).
            return $"'{v[..79]}…' (délka {value.Length})";
        }

        return $"'{v}'";
    }
}
