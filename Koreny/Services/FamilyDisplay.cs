using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Jednotné zobrazení rodiny v UI. Rodinu identifikují její rodiče — a protože oba jsou
/// nepovinní (import je klidně nemá, nová rodina je nemá ještě), musí mít smysl i rodina
/// s jedním rodičem nebo bez rodičů. Nikdy nevrací prázdný řetězec.
/// </summary>
public static class FamilyDisplay
{
    public const string NoPartnersLabel = "(rodina bez rodičů)";

    /// <summary>„James Potter &amp; Lilly Evans" — jména rodičů tak, jak je ukazuje seznam osob.</summary>
    public static string Partners(GedcomDocument? document, GedcomFamily? family)
    {
        if (document is null || family is null)
        {
            return NoPartnersLabel;
        }

        var husband = document.FindIndividual(family.HusbandId);
        var wife = document.FindIndividual(family.WifeId);

        if (husband is null && wife is null)
        {
            return NoPartnersLabel;
        }

        if (husband is null)
        {
            return PersonDisplay.Name(wife);
        }

        if (wife is null)
        {
            return PersonDisplay.Name(husband);
        }

        return $"{PersonDisplay.Name(husband)} & {PersonDisplay.Name(wife)}";
    }

    /// <summary>Rok sňatku, pokud ho z data lze vyčíst — jinak null (datum se nikdy neparsuje destruktivně).</summary>
    public static int? MarriageYear(GedcomFamily? family) => family?.Marriage?.ParsedYear;
}
