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

    /// <summary>Datum narození česky („3. 3. 1901"), nebo <c>null</c>, když v souboru není.</summary>
    public static string? BirthDate(GedcomIndividual? person) => GedcomDate.Format(person?.Birth?.Date);

    /// <summary>Datum úmrtí česky, nebo <c>null</c>, když v souboru není.</summary>
    public static string? DeathDate(GedcomIndividual? person) => GedcomDate.Format(person?.Death?.Date);

    /// <summary>
    /// Životní data jako „3. 3. 1901 – 1. 3. 1980", u žijící osoby „12. 8. 1978 –".
    /// Celé datum, ne jen rok: dva jmenovci narození v témže roce se rokem rozlišit nedají
    /// a datum narození je zároveň ten údaj, kvůli kterému se v rodokmenu listuje.
    ///
    /// Ukazuje se i tam, kde není co ukázat: „?" říká „tenhle údaj v souboru není",
    /// kdežto prázdno by vypadalo jako chyba zobrazení.
    ///
    /// Datum rozepisuje <see cref="GedcomDate"/>; surové datum, které se ukládá zpátky,
    /// zůstává nedotčené (princip 3).
    /// </summary>
    public static string Lifespan(GedcomIndividual? person)
    {
        var birth = BirthDate(person);
        var death = DeathDate(person);

        if (birth is not null && death is not null)
        {
            return $"{birth} – {death}";
        }

        if (birth is not null)
        {
            return $"{birth} –";
        }

        if (death is not null)
        {
            return $"– {death}";
        }

        return "?";
    }

    /// <summary>
    /// Údaje osoby pro proužek s vybranou osobou jako dvojice popisek–hodnota, v pořadí formuláře.
    /// Na rozdíl od <see cref="Lifespan"/> se chybějící údaj vynechá: tady nejde o rozlišení
    /// jmenovců, ale o přehled, a řádek „Úmrtí: ?" by u žijících zabíral místo bez informace.
    /// Datum se ukazuje surové, tak jak je v souboru (princip 3).
    /// </summary>
    public static IReadOnlyList<(string Label, string Value)> Details(GedcomIndividual person)
    {
        var details = new List<(string, string)>();

        if (MaidenSurname(person) is { } maiden)
        {
            details.Add(("Rodné příjmení", maiden));
        }

        if (SexLabel(person.Sex) is { } sex)
        {
            details.Add(("Pohlaví", sex));
        }

        if (EventText(person.Birth) is { } birth)
        {
            details.Add(("Narození", birth));
        }

        if (EventText(person.Death) is { } death)
        {
            details.Add(("Úmrtí", death));
        }

        if (person.Notes.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) is { } note)
        {
            details.Add(("Poznámka", note.Trim()));
        }

        details.Add(("ID", person.Id));
        return details;
    }

    private static string? MaidenSurname(GedcomIndividual person)
    {
        var surname = person.MaidenName?.Surname?.Trim();
        return string.IsNullOrEmpty(surname) ? null : surname;
    }

    /// <summary>Nestandardní hodnotu (třeba „U" nebo „X") nepřekládáme ani neschováváme — ukáže se, jak je.</summary>
    private static string? SexLabel(string? sex) => sex?.Trim() switch
    {
        null or "" => null,
        "M" => "muž",
        "F" => "žena",
        var other => other,
    };

    /// <summary>„3 MAR 1901, Praha"; událost bez data i místa (holé „BIRT Y") nemá co ukázat.</summary>
    private static string? EventText(GedcomEvent? ev)
    {
        var parts = new[] { ev?.Date?.Trim(), ev?.Place?.Trim() }.Where(s => !string.IsNullOrEmpty(s));
        var text = string.Join(", ", parts);
        return text.Length == 0 ? null : text;
    }
}
