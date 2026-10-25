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

    /// <summary>Celá data, ne roky: dva jmenovci narození v témže roce se rokem rozlišit nedají.</summary>
    [Fact]
    public void BothDates_AreJoined()
    {
        Assert.Equal("3. 3. 1901 – 1. 3. 1980", PersonDisplay.Lifespan(Person("3 MAR 1901", "1 MAR 1980")));
    }

    /// <summary>Žijící osoba: pomlčka bez druhého data, ne prázdno — to je hlavní případ rozlišení jmenovců.</summary>
    [Fact]
    public void LivingPerson_ShowsOpenRange()
    {
        Assert.Equal("12. 8. 1978 –", PersonDisplay.Lifespan(Person("12 AUG 1978", null)));
    }

    [Fact]
    public void OnlyDeathKnown_ShowsLeadingDash()
    {
        Assert.Equal("– 1. 3. 1980", PersonDisplay.Lifespan(Person(null, "1 MAR 1980")));
    }

    /// <summary>„?" je informace („v souboru to není"), prázdno by vypadalo jako chyba zobrazení.</summary>
    [Fact]
    public void NothingKnown_ShowsQuestionMark()
    {
        Assert.Equal("?", PersonDisplay.Lifespan(Person(null, null)));
        Assert.Equal("?", PersonDisplay.Lifespan(new GedcomIndividual { Id = "I2" }));
        Assert.Equal("?", PersonDisplay.Lifespan(null));
    }

    /// <summary>Událost bez data (třeba holé „BIRT Y") datum nedá — a nesmí spadnout.</summary>
    [Fact]
    public void EventWithoutDate_CountsAsUnknown()
    {
        var person = new GedcomIndividual { Id = "I3", Birth = new GedcomEvent { Place = "Praha" } };

        Assert.Equal("?", PersonDisplay.Lifespan(person));
        Assert.Null(PersonDisplay.BirthDate(person));
    }

    /// <summary>
    /// Neúplné, nejisté i nestandardní datum se ukáže celé — rok z něj vybrat by znamenalo
    /// zahodit zrovna tu část, která říká, jak je údaj jistý.
    /// </summary>
    [Theory]
    [InlineData("ABT 1850", "okolo 1850 –")]
    [InlineData("BET 1840 AND 1845", "1840 .. 1845 –")]
    [InlineData("1850", "1850 –")]
    [InlineData("kolem sv. Václava", "kolem sv. Václava –")]
    public void IncompleteDate_IsShownWhole(string date, string expected)
    {
        Assert.Equal(expected, PersonDisplay.Lifespan(Person(date, null)));
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
            Occupation = "učitelka",
        };
        person.Notes.Add("Učila na obecné škole");

        Assert.Equal(
            new (string, string)[]
            {
                ("Rodné příjmení", "Svobodová"),
                ("Pohlaví", "žena"),
                ("Narození", "okolo 1850, Praha"),
                ("Úmrtí", "Brno"),
                ("Povolání", "učitelka"),
                ("Poznámka", "Učila na obecné škole"),
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
