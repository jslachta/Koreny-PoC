using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Jednotné zobrazení osoby v UI. Osoba bez jména je legitimní stav — buď tak přišla
/// z importu, nebo ji uživatel vědomě ponechal jako neznámou, aby nepřetrhal vazbu potomků.
/// </summary>
public static class PersonDisplay
{
    public const string UnknownLabel = "(neznámá osoba)";

    public static string Name(GedcomIndividual? person) =>
        string.IsNullOrWhiteSpace(person?.FullName) ? UnknownLabel : person!.FullName;

    /// <summary>
    /// Životní data jako „1901–1980", u žijící osoby „1978–". Slouží k rozlišení jmenovců,
    /// takže se ukazuje i tam, kde není co ukázat: „?" říká „tenhle údaj v souboru není",
    /// kdežto prázdno by vypadalo jako chyba zobrazení.
    ///
    /// Rok se bere z <see cref="GedcomEvent.ParsedYear"/>, tedy jako první čtyřčíslí v datu —
    /// zobrazení nikdy nepřepisuje surové datum, které se ukládá zpátky (princip 3).
    /// </summary>
    public static string LifespanYears(GedcomIndividual? person)
    {
        var birth = person?.Birth?.ParsedYear;
        var death = person?.Death?.ParsedYear;

        if (birth is not null && death is not null)
        {
            return $"{birth}–{death}";
        }

        if (birth is not null)
        {
            return $"{birth}–";
        }

        if (death is not null)
        {
            return $"–{death}";
        }

        return "?";
    }
}
