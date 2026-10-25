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

    /// <summary>
    /// Rodné jméno jde do <c>maidenName</c>, tedy pod klíč, který Topola sama zná — a jen
    /// jako PŘÍJMENÍ, ne celé jméno. Osoba bez rodného jména klíč vůbec nemá.
    /// </summary>
    [Fact]
    public void MaidenName_IsMappedAsSurnameOnly()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-09-rodne-prijmeni.ged"));
        var data = TopolaDataMapper.Map(doc);

        Assert.Equal("Svobodová", Indi(data, "I1").MaidenName);
        Assert.Equal("Nováková", Indi(data, "I1").LastName); // hlavní jméno zůstává hlavní
        Assert.Null(Indi(data, "I3").MaidenName);

        var json = TopolaDataMapper.ToJson(doc);
        Assert.Contains("\"maidenName\":\"Svobodová\"", json);
    }

    /// <summary>
    /// Datum jde do Topoly rozložené na části, ne jako rok — z roku by v overlayi vzniklo
    /// „1901" tam, kde v souboru stojí „3 MAR 1901". Vypsat si je Topola umí sama (locale „cs").
    /// </summary>
    [Fact]
    public void FullDate_IsMappedIntoParts()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual
        {
            Id = "I1",
            Birth = new GedcomEvent { Date = "ABT 3 MAR 1901" },
            Death = new GedcomEvent { Date = "BET 1980 AND 1985" },
        });

        var indi = Indi(TopolaDataMapper.Map(doc), "I1");

        var birth = indi.Birth?.Date;
        Assert.NotNull(birth);
        Assert.Equal((3, 3, 1901, "abt"), (birth!.Day, birth.Month, birth.Year, birth.Qualifier));

        // Rozmezí má místo data dvě hranice; Topola z nich nakreslí „1980 .. 1985".
        Assert.Null(indi.Death?.Date);
        Assert.Equal(1980, indi.Death?.DateRange?.From?.Year);
        Assert.Equal(1985, indi.Death?.DateRange?.To?.Year);
    }

    /// <summary>Datum, které se přečíst nedá, projde jako text — Topola ho vypíše, jak je.</summary>
    [Fact]
    public void UnreadableDate_IsMappedAsText()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual { Id = "I1", Birth = new GedcomEvent { Date = "kolem sv. Václava" } });

        var json = TopolaDataMapper.ToJson(doc);

        Assert.Contains("\"birth\":{\"date\":{\"text\":\"kolem sv. Václava\"}}", json);
    }

    /// <summary>Událost bez data (holé „BIRT Y") klíč nedostane — prázdná by jen přidala řádek.</summary>
    [Fact]
    public void EventWithoutDate_IsNotMapped()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual { Id = "I1", Birth = new GedcomEvent { Place = "Praha" } });

        Assert.Null(Indi(TopolaDataMapper.Map(doc), "I1").Birth);
    }

    private static GedcomDocument DocWithPlacesAndOccupation()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual
        {
            Id = "I1",
            Occupation = "kovář",
            Birth = new GedcomEvent { Date = "3 MAR 1901", Place = "Praha" },
            Death = new GedcomEvent { Date = "1980", Place = "Brno" },
        });
        doc.Individuals.Add(new GedcomIndividual { Id = "I2" });
        doc.Families.Add(new GedcomFamily
        {
            Id = "F1",
            HusbandId = "I1",
            WifeId = "I2",
            Marriage = new GedcomEvent { Date = "1925", Place = "Plzeň" },
        });

        return doc;
    }

    /// <summary>
    /// Výchozí nastavení nedá do datasetu ani místa, ani povolání — a protože Topola kreslí
    /// jen to, co dostane, karty zůstanou kompaktní. Data v dokumentu přitom jsou.
    /// </summary>
    [Fact]
    public void OptionalDetails_AreAbsentByDefault()
    {
        var data = TopolaDataMapper.Map(DocWithPlacesAndOccupation());
        var i1 = Indi(data, "I1");

        Assert.Null(i1.Occupation);
        Assert.Null(i1.Birth?.Place);
        Assert.Null(i1.Death?.Place);
        Assert.Null(Fam(data, "F1").Marriage?.Place);

        // Data samotná nezmizela: datum narození je v datasetu dál.
        Assert.Equal(1901, i1.Birth?.Date?.Year);
    }

    /// <summary>Zapnutá volba přidá do datasetu právě svůj údaj, ne ostatní.</summary>
    [Fact]
    public void BirthPlace_IsMappedOnlyWhenEnabled()
    {
        var view = new TreeViewSettings { ShowBirthPlace = true };
        var data = TopolaDataMapper.Map(DocWithPlacesAndOccupation(), view);
        var i1 = Indi(data, "I1");

        Assert.Equal("Praha", i1.Birth?.Place);
        Assert.Null(i1.Death?.Place);   // vlastní volba, vlastní zaškrtávátko
        Assert.Null(i1.Occupation);
        Assert.Null(Fam(data, "F1").Marriage?.Place);
    }

    [Fact]
    public void DeathAndMarriagePlace_AreMappedWhenEnabled()
    {
        var view = new TreeViewSettings { ShowDeathPlace = true, ShowMarriagePlace = true };
        var data = TopolaDataMapper.Map(DocWithPlacesAndOccupation(), view);

        Assert.Equal("Brno", Indi(data, "I1").Death?.Place);
        Assert.Equal("Plzeň", Fam(data, "F1").Marriage?.Place);
        Assert.Null(Indi(data, "I1").Birth?.Place);
    }

    /// <summary>
    /// Povolání jde pod klíč <c>occupation</c>, který Topola sama nezná — kreslí ho až patch
    /// v js/topola-view.js, takže vypnutá volba znamená chybějící klíč, ne jen skrytý řádek.
    /// </summary>
    [Fact]
    public void Occupation_IsMappedWhenEnabled()
    {
        var doc = DocWithPlacesAndOccupation();
        var view = new TreeViewSettings { ShowOccupation = true };

        Assert.Equal("kovář", Indi(TopolaDataMapper.Map(doc, view), "I1").Occupation);
        Assert.Contains("\"occupation\":\"kovář\"", TopolaDataMapper.ToJson(doc, view));
        Assert.DoesNotContain("occupation", TopolaDataMapper.ToJson(doc));
    }

    /// <summary>
    /// Se zapnutým místem ospravedlní událost i samotné místo: „BIRT" jen s PLAC se stane
    /// řádkem „* Praha". Bez volby zůstává událost nenamapovaná (EventWithoutDate_IsNotMapped).
    /// </summary>
    [Fact]
    public void PlaceOnlyEvent_IsMappedWhenPlaceEnabled()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual { Id = "I1", Birth = new GedcomEvent { Place = "Praha" } });

        var indi = Indi(TopolaDataMapper.Map(doc, new TreeViewSettings { ShowBirthPlace = true }), "I1");

        Assert.Equal("Praha", indi.Birth?.Place);
        Assert.Null(indi.Birth?.Date);      // datum v souboru není a dopisovat se nemá
        Assert.Null(indi.Birth?.DateRange);
    }

    /// <summary>Prázdné PLAC („2 PLAC" bez hodnoty) řádek nepřidá ani se zapnutou volbou.</summary>
    [Fact]
    public void BlankPlace_IsNotMapped()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual { Id = "I1", Birth = new GedcomEvent { Place = "   " } });

        Assert.Null(Indi(TopolaDataMapper.Map(doc, new TreeViewSettings { ShowBirthPlace = true }), "I1").Birth);
    }

    /// <summary>
    /// Volby zobrazení se čtou z vlastní kopie: <see cref="TreeViewSettings.Copy"/> musí nést
    /// i údaje na kartách, jinak by „Zrušit" v dialogu část nastavení propsalo stejně.
    /// </summary>
    [Fact]
    public void Copy_CarriesCardDetailOptions()
    {
        var original = new TreeViewSettings
        {
            ShowOccupation = true,
            ShowBirthPlace = true,
            ShowDeathPlace = true,
            ShowMarriagePlace = true,
        };

        var copy = original.Copy();

        Assert.True(copy.ShowOccupation);
        Assert.True(copy.ShowBirthPlace);
        Assert.True(copy.ShowDeathPlace);
        Assert.True(copy.ShowMarriagePlace);

        // Kopie je samostatná: změna v ní se do originálu nepropíše.
        copy.ShowOccupation = false;
        Assert.True(original.ShowOccupation);
    }
}
