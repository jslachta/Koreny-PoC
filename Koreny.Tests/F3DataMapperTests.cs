using System.Text;
using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Testy mapování GedcomDocument → datový formát family-chart (f3).
/// Klíčový je scénář vícenásobného manželství — chybná atribuce dětí prvnímu
/// partnerovi je důvod, proč se DescendantTree knihovnou f3 nahrazuje.
/// </summary>
public class F3DataMapperTests
{
    private readonly GedcomParser _parser = new();

    private static string ReadCorpus(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", fileName);
        var bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static F3Person Person(List<F3Person> data, string id) =>
        data.First(p => p.Id == id);

    /// <summary>
    /// (a) corpus-05: I1 má dvě manželství (F1 s I2, F2 s I4) → oba spouses v pořadí
    /// souboru; děti se slévají ze všech rodin bez duplicit. Každé dítě nese
    /// father/mother podle SVÉ rodiny: I10 (jen F1) má matku I2.
    /// (I3 je CHIL v obou rodinách, spadá tedy pod pravidlo „první rodina" — test b.)
    /// </summary>
    [Fact]
    public void Corpus05_TwoMarriages_BothSpousesAndPerFamilyParents()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-05-relations.ged"));
        var data = F3DataMapper.Map(doc);

        var i1 = Person(data, "I1");
        Assert.Equal(new[] { "I2", "I4" }, i1.Rels.Spouses);
        Assert.Equal(new[] { "I10", "I3" }, i1.Rels.Children);

        // Druhá manželka zrcadlově: partner I1, dítě I3 (CHIL v F2).
        var i4 = Person(data, "I4");
        Assert.Equal(new[] { "I1" }, i4.Rels.Spouses);
        Assert.Equal(new[] { "I3" }, i4.Rels.Children);

        // Dítě výlučně z prvního manželství nese první manželku jako mother.
        var i10 = Person(data, "I10");
        Assert.Equal("I1", i10.Rels.Father);
        Assert.Equal("I2", i10.Rels.Mother);
    }

    /// <summary>
    /// (a-doplněk) Dítě VÝLUČNĚ z druhého manželství nese druhou partnerku jako mother.
    /// Corpus-05 takové dítě nemá (I3 je CHIL v obou rodinách), proto in-memory scénář:
    /// H×W1 → C1, H×W2 → C2. Přesně tohle DescendantTree kreslil špatně (C2 pod W1).
    /// </summary>
    [Fact]
    public void ChildOfSecondMarriage_BearsSecondWifeAsMother()
    {
        var doc = new GedcomDocument();
        foreach (var (id, sex) in new[] { ("H", "M"), ("W1", "F"), ("W2", "F"), ("C1", "M"), ("C2", "F") })
        {
            doc.Individuals.Add(new GedcomIndividual { Id = id, Sex = sex });
        }

        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "H", WifeId = "W1", ChildrenIds = { "C1" } });
        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "H", WifeId = "W2", ChildrenIds = { "C2" } });

        var data = F3DataMapper.Map(doc);

        var c1 = Person(data, "C1");
        Assert.Equal("H", c1.Rels.Father);
        Assert.Equal("W1", c1.Rels.Mother);

        var c2 = Person(data, "C2");
        Assert.Equal("H", c2.Rels.Father);
        Assert.Equal("W2", c2.Rels.Mother); // druhá partnerka, ne první

        var h = Person(data, "H");
        Assert.Equal(new[] { "W1", "W2" }, h.Rels.Spouses);
        Assert.Equal(new[] { "C1", "C2" }, h.Rels.Children);
    }

    /// <summary>
    /// (b) corpus-05: I3 je dítětem ve dvou rodinách (CHIL v F1 i F2, FAMC×2 s PEDI).
    /// father/mother se bere z PRVNÍ rodiny v pořadí souboru (F1) — konzistentně
    /// s FindFamilyAsChild ve zbytku aplikace.
    /// </summary>
    [Fact]
    public void Corpus05_ChildInTwoFamilies_ParentsFromFirstFamily()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-05-relations.ged"));
        var data = F3DataMapper.Map(doc);

        var i3 = Person(data, "I3");
        Assert.Equal("I1", i3.Rels.Father);
        Assert.Equal("I2", i3.Rels.Mother); // F1 (první), ne I4 z F2
    }

    /// <summary>
    /// (c) corpus-03: osoby bez jediné rodiny (žádný FAM záznam v souboru) jsou
    /// v datasetu přítomné s prázdnými rels.
    /// </summary>
    [Fact]
    public void Corpus03_PersonWithoutFamilies_PresentWithEmptyRels()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-03-notes.ged"));
        var data = F3DataMapper.Map(doc);

        var i1 = Person(data, "I1");
        Assert.Null(i1.Rels.Father);
        Assert.Null(i1.Rels.Mother);
        Assert.Empty(i1.Rels.Spouses);
        Assert.Empty(i1.Rels.Children);

        Assert.Equal(doc.Individuals.Count, data.Count); // nikdo nevypadl
    }

    /// <summary>JSON má klíče, které f3 očekává (id/data/rels, father/mother/spouses/children).</summary>
    [Fact]
    public void ToJson_UsesF3Keys()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-05-relations.ged"));
        var json = F3DataMapper.ToJson(doc);

        Assert.Contains("\"id\":\"I1\"", json);
        Assert.Contains("\"rels\":", json);
        Assert.Contains("\"father\":\"I1\"", json);
        Assert.Contains("\"spouses\":[\"I2\",\"I4\"]", json);
        Assert.Contains("\"first name\":\"Tomáš\"", json);
        Assert.Contains("\"gender\":\"M\"", json);
    }
}
