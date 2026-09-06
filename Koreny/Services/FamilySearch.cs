using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Hledání rodin v seznamu. Sdílí pravidla s <see cref="PersonSearch"/> — bez ohledu na
/// velikost písmen a diakritiku, dotaz se dělí na slova a musí sedět VŠECHNA v libovolném
/// pořadí. Prohledávají se jména obou rodičů, jména dětí a xref ID rodiny (např. „F12"),
/// aby „potter evans" i „harry" našly tutéž rodinu — uživatel si rodinu vybavuje přes lidi,
/// ne přes její identifikátor.
/// </summary>
public static class FamilySearch
{
    public static IEnumerable<GedcomFamily> Filter(GedcomDocument? document, IEnumerable<GedcomFamily> families, string? query)
    {
        var tokens = Tokenize(query);
        return tokens.Length == 0 ? families : families.Where(f => MatchesTokens(document, f, tokens));
    }

    /// <summary>Odpovídá rodina dotazu? Prázdný dotaz odpovídá vždy.</summary>
    public static bool Matches(GedcomDocument? document, GedcomFamily family, string? query)
    {
        var tokens = Tokenize(query);
        return tokens.Length == 0 || MatchesTokens(document, family, tokens);
    }

    private static bool MatchesTokens(GedcomDocument? document, GedcomFamily family, string[] tokens)
    {
        var haystack = PersonSearch.Fold(Haystack(document, family));
        foreach (var token in tokens)
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string Haystack(GedcomDocument? document, GedcomFamily family)
    {
        var parts = new List<string> { family.Id };

        void AddPerson(string? id)
        {
            var person = document?.FindIndividual(id);
            if (person is not null)
            {
                parts.Add(person.FullName);
                parts.Add(person.Id);
            }
        }

        AddPerson(family.HusbandId);
        AddPerson(family.WifeId);
        foreach (var childId in family.ChildrenIds)
        {
            AddPerson(childId);
        }

        return string.Join(' ', parts);
    }

    private static string[] Tokenize(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Array.Empty<string>()
            : PersonSearch.Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
