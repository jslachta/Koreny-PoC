using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

public class FamilyDisplayTests
{
    private static GedcomDocument Doc()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual
        {
            Id = "I1",
            Name = new GedcomName { GivenName = "James", Surname = "Potter", Raw = "James /Potter/" },
        });
        doc.Individuals.Add(new GedcomIndividual
        {
            Id = "I2",
            Name = new GedcomName { GivenName = "Lilly", Surname = "Evans", Raw = "Lilly /Evans/" },
        });
        doc.Individuals.Add(new GedcomIndividual { Id = "I3" }); // vědomě ponechaná neznámá osoba
        return doc;
    }

    [Fact]
    public void BothParents_AreJoined()
    {
        var doc = Doc();
        var fam = new GedcomFamily { Id = "F1", HusbandId = "I1", WifeId = "I2" };

        Assert.Equal("James Potter & Lilly Evans", FamilyDisplay.Partners(doc, fam));
    }

    [Theory]
    [InlineData("I1", null, "James Potter")]
    [InlineData(null, "I2", "Lilly Evans")]
    public void SingleParent_StandsAlone(string? husbandId, string? wifeId, string expected)
    {
        var doc = Doc();
        var fam = new GedcomFamily { Id = "F1", HusbandId = husbandId, WifeId = wifeId };

        Assert.Equal(expected, FamilyDisplay.Partners(doc, fam));
    }

    /// <summary>Neznámá osoba je platný člen rodiny — popisek ji pojmenuje stejně jako seznam osob.</summary>
    [Fact]
    public void UnknownPerson_UsesPersonDisplayLabel()
    {
        var doc = Doc();
        var fam = new GedcomFamily { Id = "F1", HusbandId = "I3", WifeId = "I2" };

        Assert.Equal($"{PersonDisplay.UnknownLabel} & Lilly Evans", FamilyDisplay.Partners(doc, fam));
    }

    [Fact]
    public void NoParents_HasItsOwnLabel()
    {
        var doc = Doc();

        Assert.Equal(FamilyDisplay.NoPartnersLabel, FamilyDisplay.Partners(doc, new GedcomFamily { Id = "F1" }));
    }

    /// <summary>Odkaz na neexistující osobu se chová jako chybějící rodič, ne jako pád (princip 3).</summary>
    [Fact]
    public void DanglingParentReference_BehavesAsMissing()
    {
        var doc = Doc();
        var fam = new GedcomFamily { Id = "F1", HusbandId = "I404", WifeId = "I2" };

        Assert.Equal("Lilly Evans", FamilyDisplay.Partners(doc, fam));
    }

    [Fact]
    public void MarriageYear_IsReadFromDateWhenPresent()
    {
        var withYear = new GedcomFamily { Id = "F1", Marriage = new GedcomEvent { Date = "15 JUN 1925" } };
        var withoutDate = new GedcomFamily { Id = "F2", Marriage = new GedcomEvent { Place = "Praha" } };
        var withoutMarriage = new GedcomFamily { Id = "F3" };

        Assert.Equal(1925, FamilyDisplay.MarriageYear(withYear));
        Assert.Null(FamilyDisplay.MarriageYear(withoutDate));
        Assert.Null(FamilyDisplay.MarriageYear(withoutMarriage));
    }
}
