using System.Globalization;
using System.Text;
using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Hledání osob v seznamu. Porovnává se bez ohledu na velikost písmen a na diakritiku,
/// aby „novak" našlo „Nováková" — u českých jmen je psaní bez háčků běžné.
/// Dotaz se dělí na slova a musí sedět VŠECHNA (v libovolném pořadí), takže „har pot"
/// najde „Harry Potter". Kromě jména se hledá i v xref ID (např. „I00001").
/// </summary>
public static class PersonSearch
{
    public static IEnumerable<GedcomIndividual> Filter(IEnumerable<GedcomIndividual> people, string? query)
    {
        var tokens = Tokenize(query);
        return tokens.Length == 0 ? people : people.Where(p => MatchesTokens(p, tokens));
    }

    /// <summary>Odpovídá osoba dotazu? Prázdný dotaz odpovídá vždy.</summary>
    public static bool Matches(GedcomIndividual person, string? query)
    {
        var tokens = Tokenize(query);
        return tokens.Length == 0 || MatchesTokens(person, tokens);
    }

    private static bool MatchesTokens(GedcomIndividual person, string[] tokens)
    {
        var haystack = Fold($"{person.FullName} {person.Id}");
        foreach (var token in tokens)
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] Tokenize(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Array.Empty<string>()
            : Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Malá písmena bez diakritiky — „Nováková" → „novakova".</summary>
    public static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            // Kombinující znaménka (háčky, čárky) po dekompozici zahodíme.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
