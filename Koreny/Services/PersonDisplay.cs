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
}
