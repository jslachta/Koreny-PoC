namespace Koreny.Models;

/// <summary>
/// Surový GEDCOM řádek jako uzel stromu — nosič pravdy pro bezeztrátový round-trip
/// (viz docs/audit-gedcom.md, sekce 4). Level se neukládá: je dán hloubkou ve stromu
/// a writer ho z hloubky odvozuje.
///
/// CONC/CONT se do stromu NIKDY nedostanou jako samostatné uzly — jejich hodnota je už
/// slitá do <see cref="Value"/> (víceřádkový obsah oddělený '\n'; CONC se napojuje přímo
/// bez mezery, CONT vkládá '\n'). Slévání dělá výhradně parser (fáze 1), rozlámání zpět
/// dělá výhradně writer.
/// </summary>
public sealed class GedcomNode
{
    public GedcomNode(string tag, string? xref = null, string value = "")
    {
        Tag = tag;
        Xref = xref;
        Value = value;
    }

    /// <summary>Cross-reference identifikátor uvedený PŘED tagem (např. „I1“ v „0 @I1@ INDI“). Null u řádků bez xref.</summary>
    public string? Xref { get; set; }

    public string Tag { get; set; }

    /// <summary>Logická (slitá) hodnota. Pointerové hodnoty jako „@F1@“ zůstávají zde, ne v <see cref="Xref"/>.</summary>
    public string Value { get; set; }

    public List<GedcomNode> Children { get; } = new();

    public GedcomNode? FirstChild(string tag)
    {
        foreach (var c in Children)
        {
            if (c.Tag == tag)
            {
                return c;
            }
        }

        return null;
    }

    public GedcomNode? LastChild(string tag)
    {
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].Tag == tag)
            {
                return Children[i];
            }
        }

        return null;
    }
}
