using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

public class PersonSearchTests
{
    private static GedcomIndividual Person(string id, string given, string surname) => new()
    {
        Id = id,
        Name = new GedcomName { GivenName = given, Surname = surname, Raw = $"{given} /{surname}/" },
    };

    private static readonly GedcomIndividual[] People =
    {
        Person("I1", "Jana", "Nováková"),
        Person("I2", "Tomáš", "Horák"),
        Person("I3", "Harry", "Potter"),
        Person("I42", "Šárka", "Dvořáková"),
    };

    [Theory]
    [InlineData("novak", "I1")]      // bez diakritiky
    [InlineData("Nováková", "I1")]   // s diakritikou
    [InlineData("NOVÁK", "I1")]      // velikost písmen
    [InlineData("sarka", "I42")]     // Š → s
    [InlineData("dvorakova", "I42")]
    [InlineData("horak", "I2")]
    public void FindsByNameIgnoringCaseAndDiacritics(string query, string expectedId)
    {
        var found = PersonSearch.Filter(People, query).Select(p => p.Id);

        Assert.Equal(new[] { expectedId }, found);
    }

    [Fact]
    public void AllTokensMustMatch_InAnyOrder()
    {
        Assert.Equal(new[] { "I3" }, PersonSearch.Filter(People, "har pot").Select(p => p.Id));
        Assert.Equal(new[] { "I3" }, PersonSearch.Filter(People, "potter harry").Select(p => p.Id));
        Assert.Empty(PersonSearch.Filter(People, "harry novak"));
    }

    [Fact]
    public void SearchesAlsoInXrefId()
    {
        Assert.Equal(new[] { "I42" }, PersonSearch.Filter(People, "I42").Select(p => p.Id));
    }

    [Fact]
    public void EmptyQueryReturnsEveryone()
    {
        Assert.Equal(People.Length, PersonSearch.Filter(People, "").Count());
        Assert.Equal(People.Length, PersonSearch.Filter(People, "   ").Count());
        Assert.Equal(People.Length, PersonSearch.Filter(People, null).Count());
    }

    [Fact]
    public void NoMatchReturnsEmpty()
    {
        Assert.Empty(PersonSearch.Filter(People, "malfoy"));
    }

    [Fact]
    public void PersonWithoutNameIsSearchableById()
    {
        var nameless = new[] { new GedcomIndividual { Id = "I99" } };

        Assert.Single(PersonSearch.Filter(nameless, "i99"));
        Assert.Empty(PersonSearch.Filter(nameless, "nekdo"));
    }

    [Fact]
    public void Fold_StripsCzechDiacritics()
    {
        Assert.Equal("prilis zlutoucky kun", PersonSearch.Fold("Příliš žluťoučký kůň"));
    }
}
