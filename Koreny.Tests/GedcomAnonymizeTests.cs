using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// „Zachovat jako neznámou osobu" je alternativa ke smazání: vazby přežijí, identita ne.
/// Slib musí platit i pro tagy, kterým editor nerozumí — jinak by uživatel viděl prázdný
/// formulář a v exportu by zůstalo povolání i poznámka (docs/principy.md, principy 5 a 7).
/// </summary>
public class GedcomAnonymizeTests
{
    private readonly GedcomParser _parser = new();

    private const string OnePerson =
        "0 HEAD\n1 CHAR UTF-8\n" +
        "0 @I1@ INDI\n" +
        "1 NAME Petr /Novák/\n" +
        "1 SEX M\n" +
        "1 BIRT\n2 DATE 3 MAR 1901\n2 PLAC Brno\n" +
        "1 DEAT\n2 DATE 1980\n" +
        "1 OCCU Kovář\n" +
        "1 NOTE Nemoc v rodině\n" +
        "1 _MHID @X1@\n" +
        "1 FAMC @F1@\n2 PEDI birth\n" +
        "1 FAMS @F2@\n" +
        "0 @F1@ FAM\n1 CHIL @I1@\n" +
        "0 @F2@ FAM\n1 HUSB @I1@\n1 CHIL @I2@\n" +
        "0 @I2@ INDI\n1 NAME Syn /Novák/\n1 FAMC @F2@\n" +
        "0 TRLR\n";

    [Fact]
    public void AnonymizedPersonKeepsOnlyFamilyLinks()
    {
        var doc = _parser.Parse(OnePerson);
        var person = doc.Individuals.First(i => i.Id == "I1");

        GedcomSync.AnonymizeIndividual(doc, person);
        var exported = GedcomWriter.Write(doc);

        Assert.Equal(new[] { "FAMC", "FAMS" }, person.SourceNode!.Children.Select(c => c.Tag));
        Assert.DoesNotContain("Petr", exported);
        Assert.DoesNotContain("Kovář", exported);        // tag mimo editor
        Assert.DoesNotContain("Nemoc v rodině", exported);
        Assert.DoesNotContain("_MHID", exported);
        Assert.DoesNotContain("1 SEX", exported);
        Assert.DoesNotContain("MAR 1901", exported);
    }

    [Fact]
    public void AnonymizedPersonStillLinksFamilies()
    {
        var doc = _parser.Parse(OnePerson);
        var person = doc.Individuals.First(i => i.Id == "I1");

        GedcomSync.AnonymizeIndividual(doc, person);
        var exported = GedcomWriter.Write(doc);

        Assert.Contains(doc.Individuals, i => i.Id == "I1");             // záznam zůstává
        Assert.Equal("I1", doc.FindFamily("F2")!.HusbandId);             // dítě nepřijde o rodiče
        Assert.Equal(new[] { "I1" }, doc.FindFamily("F1")!.ChildrenIds); // ani o prarodiče
        Assert.Contains("2 PEDI birth", exported);                       // podtag vazby přežije
    }

    [Fact]
    public void AnonymizedPersonHasNoNameInProjection()
    {
        var doc = _parser.Parse(OnePerson);
        var person = doc.Individuals.First(i => i.Id == "I1");

        GedcomSync.AnonymizeIndividual(doc, person);

        Assert.Null(person.Name);
        Assert.Null(person.Sex);
        Assert.Null(person.Birth);
        Assert.Null(person.Death);
        Assert.Empty(person.Notes);
        Assert.Equal(PersonDisplay.UnknownLabel, PersonDisplay.Name(person));
    }

    /// <summary>Alias (druhý NAME) je pořád jméno — anonymizace nesmí nechat „poslední vyhrává" mezeru.</summary>
    [Fact]
    public void SecondNameNodeIsRemovedToo()
    {
        var doc = _parser.Parse(
            "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Petr /Novák/\n1 NAME Pierre /Nowak/\n0 TRLR\n");

        var person = doc.Individuals.First();
        GedcomSync.AnonymizeIndividual(doc, person);

        Assert.Empty(person.SourceNode!.Children);
        var exported = GedcomWriter.Write(doc);
        Assert.DoesNotContain("Novák", exported);
        Assert.DoesNotContain("Nowak", exported);
    }

    /// <summary>Osoba založená v UI ještě nemusí mít uzel ve stromu; anonymizace ho vyrobí prázdný.</summary>
    [Fact]
    public void PersonWithoutSourceNodeGetsEmptyRecord()
    {
        var doc = new GedcomDocument();
        var person = new GedcomIndividual { Id = "I9", Name = new GedcomName { Raw = "Jan /Nový/" } };
        doc.Individuals.Add(person);

        GedcomSync.AnonymizeIndividual(doc, person);

        Assert.NotNull(person.SourceNode);
        Assert.Empty(person.SourceNode!.Children);
        Assert.DoesNotContain("Jan", GedcomWriter.Write(doc));
    }
}
