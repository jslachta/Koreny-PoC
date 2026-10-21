using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

public class PersonDisplayTests
{
    private static GedcomIndividual Person(string? birth, string? death) => new()
    {
        Id = "I1",
        Name = new GedcomName { GivenName = "Jiří", Surname = "Šlachta", Raw = "Jiří /Šlachta/" },
        Birth = birth is null ? null : new GedcomEvent { Date = birth },
        Death = death is null ? null : new GedcomEvent { Date = death },
    };

    [Fact]
    public void BothYears_AreJoined()
    {
        Assert.Equal("1901–1980", PersonDisplay.LifespanYears(Person("3 MAR 1901", "1 MAR 1980")));
    }

    /// <summary>Žijící osoba: pomlčka bez druhého roku, ne prázdno — to je hlavní případ rozlišení jmenovců.</summary>
    [Fact]
    public void LivingPerson_ShowsOpenRange()
    {
        Assert.Equal("1978–", PersonDisplay.LifespanYears(Person("12 AUG 1978", null)));
    }

    [Fact]
    public void OnlyDeathKnown_ShowsLeadingDash()
    {
        Assert.Equal("–1980", PersonDisplay.LifespanYears(Person(null, "1 MAR 1980")));
    }

    /// <summary>„?" je informace („v souboru to není"), prázdno by vypadalo jako chyba zobrazení.</summary>
    [Fact]
    public void NothingKnown_ShowsQuestionMark()
    {
        Assert.Equal("?", PersonDisplay.LifespanYears(Person(null, null)));
        Assert.Equal("?", PersonDisplay.LifespanYears(new GedcomIndividual { Id = "I2" }));
        Assert.Equal("?", PersonDisplay.LifespanYears(null));
    }

    /// <summary>Událost bez data (třeba holé „BIRT Y") rok nedá — a nesmí spadnout.</summary>
    [Fact]
    public void EventWithoutDate_CountsAsUnknown()
    {
        var person = new GedcomIndividual { Id = "I3", Birth = new GedcomEvent { Place = "Praha" } };

        Assert.Equal("?", PersonDisplay.LifespanYears(person));
    }

    /// <summary>Neúplné či nestandardní datum se nepřepisuje — rok se z něj jen vyčte, když tam je.</summary>
    [Theory]
    [InlineData("ABT 1850", "1850–")]
    [InlineData("BET 1840 AND 1845", "1840–")]
    [InlineData("kolem sv. Václava", "?")]
    public void YearIsReadFromRawDateWhenPresent(string date, string expected)
    {
        Assert.Equal(expected, PersonDisplay.LifespanYears(Person(date, null)));
    }

    [Fact]
    public void Details_ListAllKnownFieldsInFormOrder()
    {
        var person = new GedcomIndividual
        {
            Id = "I5",
            Name = new GedcomName { GivenName = "Marie", Surname = "Nováková", Raw = "Marie /Nováková/" },
            MaidenName = new GedcomName { GivenName = "Marie", Surname = "Svobodová", Raw = "Marie /Svobodová/" },
            Sex = "F",
            Birth = new GedcomEvent { Date = "ABT 1850", Place = "Praha" },
            Death = new GedcomEvent { Place = "Brno" },
        };
        person.Notes.Add("Učitelka");

        Assert.Equal(
            new (string, string)[]
            {
                ("Rodné příjmení", "Svobodová"),
                ("Pohlaví", "žena"),
                ("Narození", "ABT 1850, Praha"),
                ("Úmrtí", "Brno"),
                ("Poznámka", "Učitelka"),
                ("ID", "I5"),
            },
            PersonDisplay.Details(person));
    }

    /// <summary>Chybějící údaj se vynechá; holé „BIRT Y" nic neukazuje a nestandardní pohlaví zůstane, jak je.</summary>
    [Fact]
    public void Details_SkipMissingFields()
    {
        var person = new GedcomIndividual { Id = "I6", Sex = "U", Birth = new GedcomEvent() };

        Assert.Equal(new (string, string)[] { ("Pohlaví", "U"), ("ID", "I6") }, PersonDisplay.Details(person));
    }

    [Fact]
    public void UnnamedPerson_KeepsItsLabel()
    {
        Assert.Equal(PersonDisplay.UnknownLabel, PersonDisplay.Name(new GedcomIndividual { Id = "I4" }));
    }
}
