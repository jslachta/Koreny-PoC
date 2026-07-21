using System.Text;
using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Testy mapování GedcomDocument → vstupní JSON Topoly. Mapování je family-based
/// (near 1:1 z GEDCOM), takže vícenásobná manželství jdou do dat přímo a atribuci
/// dětí páru řeší Topola sama.
/// </summary>
public class TopolaDataMapperTests
{
    private readonly GedcomParser _parser = new();

    private static string ReadCorpus(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", fileName);
        var bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static TopolaIndi Indi(TopolaData d, string id) => d.Indis.First(i => i.Id == id);

    private static TopolaFam Fam(TopolaData d, string id) => d.Fams.First(f => f.Id == id);

    /// <summary>
    /// (a) corpus-05: I1 je HUSB ve dvou rodinách (F1 s I2, F2 s I4) → nese oba fams v pořadí
    /// souboru; děti zůstávají po rodinách (F1 → I10, I3; F2 → I3), nikoli slité na osobě.
    /// </summary>
    [Fact]
    public void Corpus05_TwoMarriages_BothFamsAndChildrenPerFamily()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-05-relations.ged"));
        var data = TopolaDataMapper.Map(doc);

        Assert.Equal(new[] { "F1", "F2" }, Indi(data, "I1").Fams);
        Assert.Equal(new[] { "I10", "I3" }, Fam(data, "F1").Children);
        Assert.Equal(new[] { "I3" }, Fam(data, "F2").Children);

        // Druhá partnerka drží jen svou rodinu.
        Assert.Equal(new[] { "F2" }, Indi(data, "I4").Fams);
    }

    /// <summary>
    /// (b) corpus-05: I3 je CHIL v F1 i F2 (FAMC×2). Topola má famc jen jako jediný string,
    /// takže se bere PRVNÍ rodina v pořadí souboru (F1) — konzistentně s FindFamilyAsChild.
    /// (Adopce/druhá rodičovská vazba se ztrácí — known limitation.)
    /// </summary>
    [Fact]
    public void Corpus05_ChildInTwoFamilies_FamcIsFirstFamily()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-05-relations.ged"));
        var data = TopolaDataMapper.Map(doc);

        Assert.Equal("F1", Indi(data, "I3").Famc);
    }

    /// <summary>
    /// Přivdaná osoba nese vlastní famc (vedlejší větev): corpus-07 Eva Svobodová (I5) je
    /// WIFE v F2 a zároveň CHIL v F3 (rodiče Karel + Anna).
    /// </summary>
    [Fact]
    public void Corpus07_InMarriedPerson_KeepsOwnFamcAndFams()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-07-fullgraph.ged"));
        var data = TopolaDataMapper.Map(doc);

        var eva = Indi(data, "I5");
        Assert.Equal("F3", eva.Famc);          // vlastní rodiče
        Assert.Equal(new[] { "F2" }, eva.Fams); // manželství s Petrem
    }

    /// <summary>
    /// (c) corpus-03: osoby bez jediné rodiny jsou v datasetu s prázdnými vazbami.
    /// </summary>
    [Fact]
    public void Corpus03_PersonWithoutFamilies_PresentWithNoRels()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-03-notes.ged"));
        var data = TopolaDataMapper.Map(doc);

        var i1 = Indi(data, "I1");
        Assert.Null(i1.Famc);
        Assert.Null(i1.Fams);
        Assert.Equal(doc.Individuals.Count, data.Indis.Count); // nikdo nevypadl
    }

    /// <summary>Odkaz na neexistující osobu ve vazbě rodiny se vynechá.</summary>
    [Fact]
    public void DanglingReference_IsDropped()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual { Id = "I1", Sex = "M" });
        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "I1", WifeId = "IGHOST", ChildrenIds = { "I1", "ICHILDGHOST" } });

        var data = TopolaDataMapper.Map(doc);
        var f1 = Fam(data, "F1");

        Assert.Equal("I1", f1.Husb);
        Assert.Null(f1.Wife);                 // IGHOST neexistuje → vynecháno
        Assert.Equal(new[] { "I1" }, f1.Children); // ICHILDGHOST vynecháno
    }

    /// <summary>JSON má klíče, které Topola očekává (indis/fams, famc/fams/husb/wife/children).</summary>
    [Fact]
    public void ToJson_UsesTopolaKeys()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-05-relations.ged"));
        var json = TopolaDataMapper.ToJson(doc);

        Assert.Contains("\"indis\":", json);
        Assert.Contains("\"fams\":", json);
        Assert.Contains("\"id\":\"I1\"", json);
        Assert.Contains("\"fams\":[\"F1\",\"F2\"]", json);
        Assert.Contains("\"famc\":\"F1\"", json);
        Assert.Contains("\"husb\":\"I1\"", json);
        Assert.Contains("\"children\":[\"I10\",\"I3\"]", json);
        Assert.Contains("\"firstName\":\"Tomáš\"", json);
        Assert.Contains("\"sex\":\"M\"", json);
    }
}
