using System.Globalization;
using System.Text;
using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Writer = rekurzivní serializace stromu <see cref="GedcomDocument.Nodes"/>
/// (viz docs/audit-gedcom.md, sekce 4). Pořadí uzlů je pořadím výstupu — žádné řazení
/// podle ID. Dlouhé hodnoty se lámou zpět na CONC/CONT (rozlámání dělá výhradně writer).
///
/// Hlavička: importovaný dokument si nese původní HEAD (pouze SOUR/DATE uvnitř HEAD se
/// přepíší razítkem Kořeny); dokument vytvořený od nuly (bez uzlů) dostane syntetickou
/// hlavičku. TRLR se doplní, pokud chybí.
/// </summary>
public static class GedcomWriter
{
    /// <summary>Konzervativní rozpočet znaků hodnoty na jeden fyzický řádek (5.5.1: level+delim+tag+value ≤ 255).</summary>
    private const int MaxValueLength = 200;

    private const string AppSource = "Koreny";
    private const string AppName = "Kořeny";
    private const string GedcomVersion = "5.5.1";

    /// <summary>Serialises a document to GEDCOM 5.5.1 lines (UTF-8 string; caller handles file encoding).</summary>
    public static string Write(GedcomDocument doc)
    {
        var sb = new StringBuilder();
        foreach (var root in PrepareRoots(doc))
        {
            WriteNode(sb, root, 0);
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------- příprava kořenů

    private static List<GedcomNode> PrepareRoots(GedcomDocument doc)
    {
        var result = new List<GedcomNode>();

        var head = doc.Nodes.FirstOrDefault(n => n.Tag == "HEAD");
        if (head is null)
        {
            result.Add(BuildSyntheticHead());
        }
        else
        {
            StampHead(head);
            result.Add(head);
        }

        foreach (var n in doc.Nodes)
        {
            if (n.Tag is "HEAD" or "TRLR")
            {
                continue;
            }

            result.Add(n);
        }

        result.Add(doc.Nodes.FirstOrDefault(n => n.Tag == "TRLR") ?? new GedcomNode("TRLR"));
        return result;
    }

    private static GedcomNode BuildSyntheticHead()
    {
        var head = new GedcomNode("HEAD");
        var sour = new GedcomNode("SOUR", value: AppSource);
        sour.Children.Add(new GedcomNode("NAME", value: AppName));
        sour.Children.Add(new GedcomNode("VERS", value: GedcomVersion));
        head.Children.Add(sour);

        var gedc = new GedcomNode("GEDC");
        gedc.Children.Add(new GedcomNode("VERS", value: GedcomVersion));
        // FORM Lineage-Linked is required when INDI/FAM use lineage-linked structures.
        gedc.Children.Add(new GedcomNode("FORM", value: "Lineage-Linked"));
        head.Children.Add(gedc);

        head.Children.Add(new GedcomNode("CHAR", value: "UTF-8"));
        head.Children.Add(new GedcomNode("DATE", value: StampDate()));
        return head;
    }

    /// <summary>
    /// Přepíše razítkovací tagy uvnitř původní HEAD (SOUR → Kořeny, DATE → dnešek).
    /// Zbytek hlavičky (GEDC/FORM, CHAR, LANG, SUBM…) zůstává. Tyto tagy jsou navíc
    /// při sémantickém porovnání ignorovány (viz normalizace d v GedcomSemanticDiff),
    /// takže razítko round-trip test neovlivní.
    /// </summary>
    private static void StampHead(GedcomNode head)
    {
        var sour = head.FirstChild("SOUR");
        if (sour is null)
        {
            sour = new GedcomNode("SOUR");
            head.Children.Insert(0, sour);
        }

        sour.Value = AppSource;
        sour.Children.Clear();
        sour.Children.Add(new GedcomNode("NAME", value: AppName));
        sour.Children.Add(new GedcomNode("VERS", value: GedcomVersion));

        var date = head.FirstChild("DATE");
        if (date is null)
        {
            head.Children.Add(new GedcomNode("DATE", value: StampDate()));
        }
        else
        {
            date.Value = StampDate();
            date.Children.Clear();
        }
    }

    private static string StampDate() =>
        DateTime.UtcNow.ToString("d MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    // ------------------------------------------------------------- serializace uzlů

    private static void WriteNode(StringBuilder sb, GedcomNode node, int level)
    {
        WriteValueLines(sb, level, node.Xref, node.Tag, node.Value);
        foreach (var child in node.Children)
        {
            WriteNode(sb, child, level + 1);
        }
    }

    /// <summary>
    /// Rozloží logickou hodnotu zpět na fyzické řádky: logické řádky (oddělené '\n')
    /// jako CONT, příliš dlouhé segmenty jako CONC. Fyzický řádek nikdy nekončí mezerou
    /// (diff dělá TrimEnd), takže mezera na hranici lámání přejde na začátek dalšího
    /// CONC řádku — přesně to, co slévání v parseru a v diffu očekává.
    /// </summary>
    private static void WriteValueLines(StringBuilder sb, int level, string? xref, string tag, string value)
    {
        var logicalLines = value.Split('\n');
        for (var li = 0; li < logicalLines.Length; li++)
        {
            var chunks = SplitForConc(logicalLines[li]);
            for (var ci = 0; ci < chunks.Count; ci++)
            {
                if (li == 0 && ci == 0)
                {
                    WritePhysical(sb, level, xref, tag, chunks[ci]);
                }
                else if (ci == 0)
                {
                    WritePhysical(sb, level + 1, null, "CONT", chunks[ci]);
                }
                else
                {
                    WritePhysical(sb, level + 1, null, "CONC", chunks[ci]);
                }
            }
        }
    }

    private static List<string> SplitForConc(string text)
    {
        var chunks = new List<string>();
        if (text.Length <= MaxValueLength)
        {
            chunks.Add(text);
            return chunks;
        }

        var pos = 0;
        while (pos < text.Length)
        {
            var len = Math.Min(MaxValueLength, text.Length - pos);
            if (pos + len < text.Length)
            {
                // Nenechávej koncovou mezeru na fyzickém řádku — přesuň ji na začátek dalšího CONC.
                while (len > 1 && text[pos + len - 1] == ' ')
                {
                    len--;
                }
            }

            chunks.Add(text.Substring(pos, len));
            pos += len;
        }

        return chunks;
    }

    private static void WritePhysical(StringBuilder sb, int level, string? xref, string tag, string value)
    {
        sb.Append(level.ToString(CultureInfo.InvariantCulture));
        sb.Append(' ');
        if (xref is not null)
        {
            sb.Append('@').Append(xref).Append('@').Append(' ');
        }

        sb.Append(tag);
        if (value.Length > 0)
        {
            sb.Append(' ');
            sb.Append(value);
        }

        sb.Append('\n');
    }
}
