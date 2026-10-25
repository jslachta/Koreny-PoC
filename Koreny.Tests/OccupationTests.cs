using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Povolání (OCCU). Na rozdíl od narození a úmrtí se smí legitimně opakovat — jedno
/// na každé zaměstnání, každé s vlastním DATE/PLAC. Editor umí jedno (první) a testy
/// hlídají hlavně to, že se o ostatní neotře: ani o jejich hodnoty, ani o jejich podtagy.
/// </summary>
public class OccupationTests
{
    private readonly GedcomParser _parser = new();

    /// <summary>Dvě povolání, první s datem a místem; druhé je to, o kterém editor neví.</summary>
    private const string TwoOccupations =
        "0 HEAD\n1 CHAR UTF-8\n" +
        "0 @I1@ INDI\n" +
        "1 NAME Josef /Novák/\n" +
        "1 SEX M\n" +
        "1 OCCU mlynář\n2 DATE FROM 1880 TO 1895\n2 PLAC Vyšní Lhoty\n" +
        "1 OCCU obecní strážník\n2 DATE FROM 1896\n" +
        "1 NOTE Nájemce panského mlýna\n" +
        "0 TRLR\n";

    private const string NoOccupation =
        "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Jan /Dvořák/\n1 SEX M\n0 TRLR\n";

    private static GedcomIndividual Person(GedcomDocument doc) => doc.Individuals.First(i => i.Id == "I1");

    /// <summary>Vyhrává první výskyt — u opakovatelného atributu není „poslední přepíše" na místě.</summary>
    [Fact]
    public void Parse_ReadsFirstOccupation()
    {
        Assert.Equal("mlynář", Person(_parser.Parse(TwoOccupations)).Occupation);
    }

    [Fact]
    public void Parse_PersonWithoutOccupation_HasNone()
    {
        Assert.Null(Person(_parser.Parse(NoOccupation)).Occupation);
    }

    /// <summary>Načíst → uložit beze změny přes formulář je sémantická identita i s dvěma OCCU.</summary>
    [Fact]
    public void Edit_SaveUnchanged_IsIdempotent()
    {
        var doc = _parser.Parse(TwoOccupations);
        var person = Person(doc);

        var form = new PersonFormState();
        form.Fill(person);
        form.ApplyTo(person);
        GedcomSync.SyncIndividual(doc, person);

        var report = GedcomSemanticDiff.Compare(TwoOccupations, GedcomWriter.Write(doc));
        Assert.True(report.IsEmpty, $"Uložení beze změny změnilo dokument.{Environment.NewLine}{report}");
    }

    /// <summary>
    /// Změna povolání sahá jen na hodnotu prvního OCCU: jeho DATE/PLAC zůstávají (patří
    /// k témuž zaměstnání) a druhé povolání se nehýbe ani nepřepisuje.
    /// </summary>
    [Fact]
    public void Edit_ChangeOccupation_KeepsSubtagsAndOtherOccupation()
    {
        var doc = _parser.Parse(TwoOccupations);
        var person = Person(doc);

        var form = new PersonFormState();
        form.Fill(person);
        form.Occupation = "mlynářský pomocník";
        form.ApplyTo(person);
        GedcomSync.SyncIndividual(doc, person);
        var exported = GedcomWriter.Write(doc);

        Assert.Contains("1 OCCU mlynářský pomocník\n2 DATE FROM 1880 TO 1895\n2 PLAC Vyšní Lhoty\n", exported);
        Assert.Contains("1 OCCU obecní strážník\n2 DATE FROM 1896\n", exported);
        Assert.DoesNotContain("OCCU mlynář\n", exported);
    }

    /// <summary>
    /// Vymazané povolání odstraní celý první uzel i s podtagy — osiřelé „DATE FROM 1880"
    /// by v exportu bylo horší než nic. Druhé OCCU zůstává a stane se tím, které editor
    /// příště ukáže.
    /// </summary>
    [Fact]
    public void Edit_ClearOccupation_RemovesFirstNodeWithSubtags()
    {
        var doc = _parser.Parse(TwoOccupations);
        var person = Person(doc);

        var form = new PersonFormState();
        form.Fill(person);
        form.Occupation = "   ";
        form.ApplyTo(person);
        GedcomSync.SyncIndividual(doc, person);
        var exported = GedcomWriter.Write(doc);

        Assert.Null(person.Occupation);
        Assert.DoesNotContain("mlynář", exported);
        Assert.DoesNotContain("Vyšní Lhoty", exported);
        Assert.DoesNotContain("FROM 1880", exported);
        Assert.Contains("1 OCCU obecní strážník\n2 DATE FROM 1896\n", exported);
        Assert.Contains("1 NOTE Nájemce panského mlýna\n", exported); // sousedy to nerozhodí

        Assert.Equal("obecní strážník", Person(_parser.Parse(exported)).Occupation);
    }

    /// <summary>Povolání osobě, která žádné nemá, přibude jako nový uzel OCCU.</summary>
    [Fact]
    public void Edit_AddOccupation_CreatesNode()
    {
        var doc = _parser.Parse(NoOccupation);
        var person = Person(doc);

        var form = new PersonFormState();
        form.Fill(person);
        form.Occupation = " kovář ";
        form.ApplyTo(person);
        GedcomSync.SyncIndividual(doc, person);

        Assert.Equal("kovář", person.Occupation); // okrajové mezery se do souboru nepíšou
        Assert.Contains("1 OCCU kovář\n", GedcomWriter.Write(doc));
    }

    /// <summary>Osoba založená v UI: povolání se zapíše i do záznamu, který ještě ve stromu není.</summary>
    [Fact]
    public void Edit_NewPersonWithOccupation_IsWritten()
    {
        var doc = _parser.Parse(NoOccupation);
        var person = new GedcomIndividual
        {
            Id = "I2",
            Name = new GedcomName { GivenName = "Marie", Surname = "Nováková", Raw = "Marie /Nováková/" },
            Occupation = "učitelka",
        };
        doc.Individuals.Add(person);
        GedcomSync.SyncIndividual(doc, person);

        Assert.Contains("1 OCCU učitelka\n", GedcomWriter.Write(doc));
    }

    /// <summary>Povolání je identifikující údaj — v anonymizovaném záznamu nesmí zůstat.</summary>
    [Fact]
    public void Anonymize_DropsEveryOccupation()
    {
        var doc = _parser.Parse(TwoOccupations);
        var person = Person(doc);

        GedcomSync.AnonymizeIndividual(doc, person);
        var exported = GedcomWriter.Write(doc);

        Assert.Null(person.Occupation);
        Assert.DoesNotContain("OCCU", exported);
        Assert.DoesNotContain("strážník", exported);
    }

    /// <summary>Povolání je v náhledu vybrané osoby, hned za životními daty.</summary>
    [Fact]
    public void Display_ShowsOccupationAfterDeath()
    {
        var details = PersonDisplay.Details(Person(_parser.Parse(TwoOccupations)));

        Assert.Equal(("Povolání", "mlynář"), details[^3]);
        Assert.Equal(("Poznámka", "Nájemce panského mlýna"), details[^2]);
    }
}
