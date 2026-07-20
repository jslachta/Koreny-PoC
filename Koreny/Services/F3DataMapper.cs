using System.Text.Json;
using System.Text.Json.Serialization;
using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Mapování <see cref="GedcomDocument"/> na datový formát knihovny family-chart (f3):
/// pole osob { id, data{...}, rels{ father, mother, spouses[], children[] } }.
/// f3 0.9.0 tento „legacy" tvar normalizuje v <c>formatData</c> (father/mother → parents),
/// takže je bezpečné ho emitovat.
///
/// Pravidla mapování:
/// - father/mother dítěte = HUSB/WIFE rodiny, kde je dítě CHIL — bere se PRVNÍ taková
///   rodina v pořadí souboru (<see cref="GedcomDocument.FindFamilyAsChild"/>), konzistentně
///   se zbytkem aplikace (HourglassTree).
/// - spouses = partneři ze VŠECH rodin osoby (pořadí rodin dle souboru). Tím se řeší
///   vícenásobná manželství: f3 přiřazuje dítě páru podle father+mother dítěte, takže
///   dítě z druhého manželství se zobrazí u druhého partnera — chybná atribuce
///   z DescendantTree (partner vždy z první rodiny) tímto zaniká.
/// - children = CHIL ze všech rodin osoby, pořadí dle rodin a uzlů v souboru, bez duplicit.
/// - Osoby bez jakýchkoli vazeb se do datasetu zahrnují (f3 je umí zobrazit po přepnutí kořene).
/// - Odkazy na neexistující osoby (HUSB/WIFE/CHIL na chybějící ID) se vynechávají,
///   aby f3 nedostalo nerozřešitelné id.
/// - gender se emituje jen pro SEX M/F; jiné/chybějící pohlaví se vynechá (f3 na něm
///   jen větví layout párů, chybějící hodnota není chyba).
/// </summary>
public static class F3DataMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(GedcomDocument doc) =>
        JsonSerializer.Serialize(Map(doc), JsonOpts);

    public static List<F3Person> Map(GedcomDocument doc)
    {
        var result = new List<F3Person>(doc.Individuals.Count);

        foreach (var ind in doc.Individuals)
        {
            var person = new F3Person
            {
                Id = ind.Id,
                Data = BuildData(ind),
                Rels = BuildRels(doc, ind.Id),
            };
            result.Add(person);
        }

        return result;
    }

    private static F3PersonData BuildData(GedcomIndividual ind)
    {
        return new F3PersonData
        {
            FirstName = ind.Name?.GivenName ?? string.Empty,
            LastName = ind.Name?.Surname ?? string.Empty,
            Years = FormatYears(ind),
            Gender = ind.Sex is "M" or "F" ? ind.Sex : null,
        };
    }

    private static F3Rels BuildRels(GedcomDocument doc, string personId)
    {
        var rels = new F3Rels();

        var famAsChild = doc.FindFamilyAsChild(personId);
        if (famAsChild is not null)
        {
            rels.Father = ExistingId(doc, famAsChild.HusbandId);
            rels.Mother = ExistingId(doc, famAsChild.WifeId);
        }

        var spouses = new List<string>();
        var children = new List<string>();
        var seenSpouse = new HashSet<string>(StringComparer.Ordinal);
        var seenChild = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fam in doc.Families)
        {
            string? partnerId = null;
            if (fam.HusbandId == personId)
            {
                partnerId = fam.WifeId;
            }
            else if (fam.WifeId == personId)
            {
                partnerId = fam.HusbandId;
            }
            else
            {
                continue;
            }

            var partner = ExistingId(doc, partnerId);
            if (partner is not null && seenSpouse.Add(partner))
            {
                spouses.Add(partner);
            }

            foreach (var childId in fam.ChildrenIds)
            {
                var child = ExistingId(doc, childId);
                if (child is not null && seenChild.Add(child))
                {
                    children.Add(child);
                }
            }
        }

        rels.Spouses = spouses;
        rels.Children = children;
        return rels;
    }

    private static string? ExistingId(GedcomDocument doc, string? id) =>
        doc.FindIndividual(id) is null ? null : id;

    private static string FormatYears(GedcomIndividual i)
    {
        var b = i.Birth?.ParsedYear;
        var d = i.Death?.ParsedYear;
        if (b is not null && d is not null)
        {
            return $"{b}–{d}";
        }

        if (b is not null)
        {
            return $"{b}–";
        }

        if (d is not null)
        {
            return $"–{d}";
        }

        return string.Empty;
    }
}

public sealed class F3Person
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("data")]
    public required F3PersonData Data { get; init; }

    [JsonPropertyName("rels")]
    public required F3Rels Rels { get; init; }
}

public sealed class F3PersonData
{
    [JsonPropertyName("first name")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("last name")]
    public string LastName { get; init; } = string.Empty;

    /// <summary>Roky života pro druhý řádek karty, např. „1901–1978".</summary>
    [JsonPropertyName("years")]
    public string Years { get; init; } = string.Empty;

    /// <summary>„M"/„F"; null (vynecháno v JSON) pro neznámé pohlaví.</summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; init; }
}

public sealed class F3Rels
{
    [JsonPropertyName("father")]
    public string? Father { get; set; }

    [JsonPropertyName("mother")]
    public string? Mother { get; set; }

    [JsonPropertyName("spouses")]
    public List<string> Spouses { get; set; } = new();

    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = new();
}
