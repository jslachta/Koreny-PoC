using System.Text.Json;
using System.Text.Json.Serialization;
using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Mapování <see cref="GedcomDocument"/> na vstupní JSON knihovny Topola
/// (<c>JsonGedcomData { indis, fams }</c>).
///
/// Mapování je family-based, tedy téměř 1:1 z GEDCOM struktury — na rozdíl od f3 se
/// neodvozuje father/mother, rodiny se předávají jak jsou a vícenásobná manželství si
/// řeší Topola sama (osoba nese všechny své <c>fams</c>, dítě se zobrazí u páru své rodiny).
///
/// Pravidla:
/// - všechny osoby a rodiny dokumentu (i osoby bez vazeb) jsou v datasetu;
/// - <c>famc</c> = PRVNÍ rodina, kde je osoba dítětem (<see cref="GedcomDocument.FindFamilyAsChild"/>),
///   protože Topola má <c>famc</c> jen jako jediný string — dítě ve dvou rodinách (adopce) tak
///   nese jen první rodičovskou vazbu (konzistentně se zbytkem aplikace; viz known limitations);
/// - <c>fams</c> = všechny rodiny, kde je osoba HUSB/WIFE, v pořadí souboru;
/// - <c>children</c> = CHIL v pořadí uzlů; odkazy na neexistující osoby se vynechávají;
/// - <c>sex</c> jen pro SEX M/F; roky narození/úmrtí jako <c>{ date: { year } }</c>, když jsou.
/// </summary>
public static class TopolaDataMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(GedcomDocument doc) =>
        JsonSerializer.Serialize(Map(doc), JsonOpts);

    public static TopolaData Map(GedcomDocument doc)
    {
        var data = new TopolaData();

        foreach (var ind in doc.Individuals)
        {
            var indi = new TopolaIndi
            {
                Id = ind.Id,
                FirstName = NullIfEmpty(ind.Name?.GivenName),
                LastName = NullIfEmpty(ind.Name?.Surname),
                Sex = ind.Sex is "M" or "F" ? ind.Sex : null,
                Birth = MakeEvent(ind.Birth),
                Death = MakeEvent(ind.Death),
            };

            var famc = doc.FindFamilyAsChild(ind.Id);
            if (famc is not null)
            {
                indi.Famc = famc.Id;
            }

            var fams = new List<string>();
            foreach (var fam in doc.Families)
            {
                if (fam.HusbandId == ind.Id || fam.WifeId == ind.Id)
                {
                    fams.Add(fam.Id);
                }
            }

            if (fams.Count > 0)
            {
                indi.Fams = fams;
            }

            data.Indis.Add(indi);
        }

        foreach (var fam in doc.Families)
        {
            var f = new TopolaFam
            {
                Id = fam.Id,
                Husb = ExistingId(doc, fam.HusbandId),
                Wife = ExistingId(doc, fam.WifeId),
                Marriage = MakeEvent(fam.Marriage),
            };

            foreach (var c in fam.ChildrenIds)
            {
                var cid = ExistingId(doc, c);
                if (cid is not null)
                {
                    f.Children.Add(cid);
                }
            }

            data.Fams.Add(f);
        }

        return data;
    }

    private static string? ExistingId(GedcomDocument doc, string? id) =>
        doc.FindIndividual(id) is null ? null : id;

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static TopolaEvent? MakeEvent(GedcomEvent? ev)
    {
        var year = ev?.ParsedYear;
        return year is null ? null : new TopolaEvent { Date = new TopolaDate { Year = year } };
    }
}

public sealed class TopolaData
{
    [JsonPropertyName("indis")]
    public List<TopolaIndi> Indis { get; } = new();

    [JsonPropertyName("fams")]
    public List<TopolaFam> Fams { get; } = new();
}

public sealed class TopolaIndi
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("sex")]
    public string? Sex { get; init; }

    [JsonPropertyName("famc")]
    public string? Famc { get; set; }

    [JsonPropertyName("fams")]
    public List<string>? Fams { get; set; }

    [JsonPropertyName("birth")]
    public TopolaEvent? Birth { get; init; }

    [JsonPropertyName("death")]
    public TopolaEvent? Death { get; init; }
}

public sealed class TopolaFam
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("husb")]
    public string? Husb { get; init; }

    [JsonPropertyName("wife")]
    public string? Wife { get; init; }

    [JsonPropertyName("children")]
    public List<string> Children { get; } = new();

    [JsonPropertyName("marriage")]
    public TopolaEvent? Marriage { get; init; }
}

public sealed class TopolaEvent
{
    [JsonPropertyName("date")]
    public TopolaDate? Date { get; init; }
}

public sealed class TopolaDate
{
    [JsonPropertyName("year")]
    public int? Year { get; init; }
}
