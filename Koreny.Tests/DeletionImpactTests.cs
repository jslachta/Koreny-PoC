using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

public class DeletionImpactTests
{
    private static GedcomIndividual Person(string id, string? given = null, string? surname = null)
    {
        var p = new GedcomIndividual { Id = id };
        if (given is not null || surname is not null)
        {
            p.Name = new GedcomName { GivenName = given, Surname = surname, Raw = $"{given} /{surname}/" };
        }

        return p;
    }

    /// <summary>Děda+bába → otec; otec+matka → syn a dcera. Smazání otce je „uprostřed rodokmenu".</summary>
    private static GedcomDocument Tree()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(Person("GF", "Josef", "Novák"));
        doc.Individuals.Add(Person("GM", "Marie", "Nováková"));
        doc.Individuals.Add(Person("F", "Petr", "Novák"));
        doc.Individuals.Add(Person("M", "Jana", "Nováková"));
        doc.Individuals.Add(Person("S", "Tomáš", "Novák"));
        doc.Individuals.Add(Person("D", "Eva", "Nováková"));
        doc.Individuals.Add(Person("X", "Cizí", "Člověk"));

        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "GF", WifeId = "GM", ChildrenIds = { "F" } });
        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "F", WifeId = "M", ChildrenIds = { "S", "D" } });
        return doc;
    }

    [Fact]
    public void MiddlePerson_ReportsBothRolesAndOrphanedChildren()
    {
        var doc = Tree();

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("F")!);

        Assert.Equal("Petr Novák", impact.PersonName);
        Assert.Equal(1, impact.SpouseFamilyCount);   // F2
        Assert.Equal(1, impact.ChildFamilyCount);    // F1
        Assert.Equal(new[] { "Tomáš Novák", "Eva Nováková" }, impact.LosingParentNames);
        Assert.Empty(impact.EmptiedFamilyIds);       // v obou rodinách někdo zbude
        Assert.True(impact.HasAnyLinks);
    }

    [Fact]
    public void IsolatedPerson_HasNoLinks()
    {
        var doc = Tree();

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("X")!);

        Assert.Equal(0, impact.SpouseFamilyCount);
        Assert.Equal(0, impact.ChildFamilyCount);
        Assert.Empty(impact.LosingParentNames);
        Assert.False(impact.HasAnyLinks);
    }

    [Fact]
    public void LeafPerson_LosesNoOneButIsLinked()
    {
        var doc = Tree();

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("S")!);

        Assert.Equal(0, impact.SpouseFamilyCount);
        Assert.Equal(1, impact.ChildFamilyCount);
        Assert.Empty(impact.LosingParentNames);
        Assert.True(impact.HasAnyLinks);
    }

    [Fact]
    public void FamilyThatWouldBeLeftEmpty_IsReported()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(Person("A", "Sám", "Vojín"));
        doc.Families.Add(new GedcomFamily { Id = "F9", HusbandId = "A" }); // bez manželky i dětí

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("A")!);

        Assert.Equal(new[] { "F9" }, impact.EmptiedFamilyIds);
    }

    [Fact]
    public void FamilyKeepingOtherSpouse_IsNotReportedAsEmptied()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(Person("A", "Jan", "Malý"));
        doc.Individuals.Add(Person("B", "Ida", "Malá"));
        doc.Families.Add(new GedcomFamily { Id = "F9", HusbandId = "A", WifeId = "B" });

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("A")!);

        Assert.Empty(impact.EmptiedFamilyIds);
    }

    [Fact]
    public void ChildrenAcrossTwoMarriages_AreAllListedOnce()
    {
        var doc = new GedcomDocument();
        foreach (var id in new[] { "H", "W1", "W2", "C1", "C2" })
        {
            doc.Individuals.Add(Person(id, id, "Test"));
        }

        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "H", WifeId = "W1", ChildrenIds = { "C1" } });
        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "H", WifeId = "W2", ChildrenIds = { "C2", "C1" } });

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("H")!);

        Assert.Equal(2, impact.SpouseFamilyCount);
        Assert.Equal(new[] { "C1 Test", "C2 Test" }, impact.LosingParentNames.OrderBy(x => x));
    }

    [Fact]
    public void UnnamedPersonUsesPlaceholderLabel()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(Person("A"));                 // bez jména
        doc.Individuals.Add(Person("C"));                 // dítě také bez jména
        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "A", ChildrenIds = { "C" } });

        var impact = DeletionImpact.ForPerson(doc, doc.FindIndividual("A")!);

        Assert.Equal(PersonDisplay.UnknownLabel, impact.PersonName);
        Assert.Equal(new[] { PersonDisplay.UnknownLabel }, impact.LosingParentNames);
    }
}
