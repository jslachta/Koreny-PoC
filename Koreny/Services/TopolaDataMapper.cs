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
/// - <c>sex</c> jen pro SEX M/F; data narození, úmrtí a svatby celá, jak je GEDCOM zná —
///   <c>{ date: { day, month, year, qualifier } }</c>, u rozmezí <c>{ dateRange: { from, to } }</c>,
///   a co se přečíst nedá, jde dál jako <c>text</c>. Vypsat si je Topola umí sama (locale „cs"
///   nastavuje js/topola-view.js), takže v overlayi stojí „3. 3. 1901" a ne jen rok;
/// - <c>maidenName</c> = rodné PŘÍJMENÍ (ne celé jméno), stejně jak ho plní vlastní GEDCOM
///   čtečka Topoly — kreslí ho až obalení rendereru v js/topola-view.js.
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
                MaidenName = NullIfEmpty(ind.MaidenName?.Surname),
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

    /// <summary>
    /// Datum se předává rozložené na části, ne jako rok — Topola si ho vypisuje sama a z roku
    /// by nakreslila jen rok. Čte ho <see cref="GedcomDate"/> stejnými pravidly, jaká má Topola
    /// ve své GEDCOM čtečce, takže náhled i overlay ukazují tentýž text.
    /// </summary>
    private static TopolaEvent? MakeEvent(GedcomEvent? ev)
    {
        var value = GedcomDate.Parse(ev?.Date);
        if (value is null)
        {
            return null;
        }

        var date = MakeDate(value.Date);
        var from = MakeDate(value.From);
        var to = MakeDate(value.To);

        if (date is null && from is null && to is null)
        {
            return null; // „BEF" bez data: hranice není a prázdná událost by jen přidala řádek
        }

        return new TopolaEvent
        {
            Date = date,
            DateRange = from is null && to is null ? null : new TopolaDateRange { From = from, To = to },
        };
    }

    private static TopolaDate? MakeDate(GedcomDateParts? parts) =>
        parts is null
            ? null
            : new TopolaDate
            {
                Day = parts.Day,
                Month = parts.Month,
                Year = parts.Year,
                Qualifier = parts.Qualifier,
                Text = parts.Text,
            };
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

    /// <summary>
    /// Rodné příjmení. Topola tenhle pojem sama zná (<c>getMaidenName()</c>) a její vlastní
    /// GEDCOM čtečka ho plní stejným pravidlem jako my — jen příjmení, ne celé jméno.
    /// Vykreslit ho zatím musí naše obalení rendereru v js/topola-view.js.
    /// </summary>
    [JsonPropertyName("maidenName")]
    public string? MaidenName { get; init; }

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

    /// <summary>Rozmezí („BET … AND …", „BEF …"); Topola ho vypíše jako „x .. y", „před x", „po x".</summary>
    [JsonPropertyName("dateRange")]
    public TopolaDateRange? DateRange { get; init; }
}

public sealed class TopolaDateRange
{
    [JsonPropertyName("from")]
    public TopolaDate? From { get; init; }

    [JsonPropertyName("to")]
    public TopolaDate? To { get; init; }
}

public sealed class TopolaDate
{
    [JsonPropertyName("day")]
    public int? Day { get; init; }

    [JsonPropertyName("month")]
    public int? Month { get; init; }

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    /// <summary>Nejistota data: <c>abt</c>, <c>cal</c> nebo <c>est</c> — Topola z toho udělá „okolo", „vypočt.", „odhadem".</summary>
    [JsonPropertyName("qualifier")]
    public string? Qualifier { get; init; }

    /// <summary>Datum, které se nepodařilo přečíst; Topola ho vypíše, jak je.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
