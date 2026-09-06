using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

public class FamilySearchTests
{
    private static GedcomIndividual Person(string id, string given, string surname) => new()
    {
        Id = id,
        Name = new GedcomName { GivenName = given, Surname = surname, Raw = $"{given} /{surname}/" },
    };

    /// <summary>
    /// F1: Tomáš Horák &amp; Jana Nováková, děti Šárka a Petr.
    /// F2: Harry Potter &amp; (bez manželky), bez dětí.
    /// F3: prázdná rodina bez rodičů i dětí.
    /// </summary>
    private static GedcomDocument Doc()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(Person("I1", "Tomáš", "Horák"));
        doc.Individuals.Add(Person("I2", "Jana", "Nováková"));
        doc.Individuals.Add(Person("I3", "Šárka", "Horáková"));
        doc.Individuals.Add(Person("I4", "Petr", "Horák"));
        doc.Individuals.Add(Person("I5", "Harry", "Potter"));

        var f1 = new GedcomFamily { Id = "F1", HusbandId = "I1", WifeId = "I2" };
        f1.ChildrenIds.Add("I3");
        f1.ChildrenIds.Add("I4");
        doc.Families.Add(f1);

        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "I5" });
        doc.Families.Add(new GedcomFamily { Id = "F3" });

        return doc;
    }

    private static string[] Ids(GedcomDocument doc, string? query) =>
        FamilySearch.Filter(doc, doc.Families, query).Select(f => f.Id).ToArray();

    [Theory]
    [InlineData("horak", "F1")]      // bez diakritiky
    [InlineData("Nováková", "F1")]   // s diakritikou
    [InlineData("POTTER", "F2")]     // velikost písmen
    public void FindsByParentNameIgnoringCaseAndDiacritics(string query, string expectedId)
    {
        var doc = Doc();

        Assert.Equal(new[] { expectedId }, Ids(doc, query));
    }

    [Fact]
    public void AllTokensMustMatch_InAnyOrder()
    {
        var doc = Doc();

        Assert.Equal(new[] { "F1" }, Ids(doc, "horak novakova"));
        Assert.Equal(new[] { "F1" }, Ids(doc, "novakova horak"));
        Assert.Empty(Ids(doc, "potter novakova"));
    }

    /// <summary>Rodinu si uživatel vybavuje přes lidi v ní — včetně dětí, ne jen rodičů.</summary>
    [Fact]
    public void SearchesAlsoInChildrenNames()
    {
        var doc = Doc();

        Assert.Equal(new[] { "F1" }, Ids(doc, "sarka"));
        Assert.Equal(new[] { "F1" }, Ids(doc, "petr"));
    }

    [Fact]
    public void SearchesAlsoInXrefIds()
    {
        var doc = Doc();

        Assert.Equal(new[] { "F2" }, Ids(doc, "F2"));
        Assert.Equal(new[] { "F2" }, Ids(doc, "I5")); // ID člena rodiny
    }

    [Fact]
    public void EmptyQueryReturnsEverything()
    {
        var doc = Doc();

        Assert.Equal(new[] { "F1", "F2", "F3" }, Ids(doc, ""));
        Assert.Equal(new[] { "F1", "F2", "F3" }, Ids(doc, "   "));
        Assert.Equal(new[] { "F1", "F2", "F3" }, Ids(doc, null));
    }

    /// <summary>Rodina bez rodičů i dětí nesmí hledání shodit — je to legitimní rozpracovaný stav.</summary>
    [Fact]
    public void EmptyFamilyMatchesOnlyItsOwnId()
    {
        var doc = Doc();

        Assert.Equal(new[] { "F3" }, Ids(doc, "f3"));
        Assert.Empty(Ids(doc, "kdokoliv"));
    }

    /// <summary>Odkaz na neexistující osobu (rozbitý už v importu, princip 3) se přeskočí, ne shodí.</summary>
    [Fact]
    public void DanglingMemberReferenceIsIgnored()
    {
        var doc = Doc();
        var broken = new GedcomFamily { Id = "F9", HusbandId = "I404" };
        broken.ChildrenIds.Add("I405");
        doc.Families.Add(broken);

        Assert.Equal(new[] { "F9" }, Ids(doc, "f9"));
        Assert.Empty(Ids(doc, "i404"));
    }
}
