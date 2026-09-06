using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Rozlišení záznamů NAME podle podřízeného tagu TYPE (GEDCOM 5.5.1, NAME_TYPE:
/// aka | birth | immigrant | maiden | married | uživatelská hodnota).
///
/// Editor rozumí jediné hodnotě — „maiden“, tedy jménu před sňatkem. Ostatní typy zná
/// jen potud, že je nepovažuje za rodné jméno; zůstávají netknuté v surovém stromu
/// (docs/principy.md, princip 3). Sdílí to parser i <see cref="GedcomSync"/>, aby čtení
/// a zápis nikdy neurčovaly „který NAME je který“ každý po svém.
/// </summary>
internal static class GedcomNameTypes
{
    internal const string Maiden = "maiden";

    /// <summary>
    /// Je tenhle NAME rodné jméno? Hodnota TYPE se porovnává bez ohledu na velikost písmen —
    /// standard ji předepisuje malými, ale v exportech se potkáš i s „Maiden“.
    /// </summary>
    internal static bool IsMaiden(GedcomNode nameNode) =>
        string.Equals(nameNode.FirstChild("TYPE")?.Value.Trim(), Maiden, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Hlavní jméno osoby: poslední NAME, který NENÍ rodné. Rodné jméno je z definice
    /// jméno bývalé, takže se hlavním stát nesmí — jinak by osobě po přidání rodného
    /// příjmení skočilo jméno v seznamu i v rodokmenu na to dřívější.
    /// </summary>
    internal static GedcomNode? LastPrimaryName(GedcomNode individual)
    {
        for (var i = individual.Children.Count - 1; i >= 0; i--)
        {
            var child = individual.Children[i];
            if (child.Tag == "NAME" && !IsMaiden(child))
            {
                return child;
            }
        }

        return null;
    }

    internal static GedcomNode? MaidenNameNode(GedcomNode individual)
    {
        foreach (var child in individual.Children)
        {
            if (child.Tag == "NAME" && IsMaiden(child))
            {
                return child;
            }
        }

        return null;
    }
}
