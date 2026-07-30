using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>Pravidla bránící vzniku cyklů při výběru dětí rodiny.</summary>
public class RelationshipRulesTests
{
    /// <summary>Děda → otec → syn (F1: děda+babička → otec, F2: otec+matka → syn).</summary>
    private static GedcomDocument ThreeGenerations()
    {
        var doc = new GedcomDocument();
        foreach (var (id, sex) in new[] { ("GF", "M"), ("GM", "F"), ("F", "M"), ("M", "F"), ("S", "M"), ("X", "M") })
        {
            doc.Individuals.Add(new GedcomIndividual { Id = id, Sex = sex });
        }

        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "GF", WifeId = "GM", ChildrenIds = { "F" } });
        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "F", WifeId = "M", ChildrenIds = { "S" } });
        return doc;
    }

    [Fact]
    public void Ancestors_WalksUpAllGenerations()
    {
        var doc = ThreeGenerations();

        Assert.Equal(new[] { "F", "GF", "GM", "M" }, RelationshipRules.Ancestors(doc, "S").OrderBy(x => x));
        Assert.Equal(new[] { "GF", "GM" }, RelationshipRules.Ancestors(doc, "F").OrderBy(x => x));
        Assert.Empty(RelationshipRules.Ancestors(doc, "GF"));
    }

    [Fact]
    public void ParentCannotBeOwnChild()
    {
        var doc = ThreeGenerations();

        Assert.False(RelationshipRules.CanBeChild(doc, "F", "M", "F"));  // manžel
        Assert.False(RelationshipRules.CanBeChild(doc, "F", "M", "M"));  // manželka
    }

    [Fact]
    public void AncestorOfParentCannotBeChild()
    {
        var doc = ThreeGenerations();

        // Děda ani babička nesmí být dítětem rodiny svého syna — vznikl by cyklus.
        Assert.False(RelationshipRules.CanBeChild(doc, "F", "M", "GF"));
        Assert.False(RelationshipRules.CanBeChild(doc, "F", "M", "GM"));
    }

    [Fact]
    public void UnrelatedPersonCanBeChild()
    {
        var doc = ThreeGenerations();

        Assert.True(RelationshipRules.CanBeChild(doc, "F", "M", "X"));
        Assert.True(RelationshipRules.CanBeChild(doc, "F", "M", "S"));
    }

    [Fact]
    public void ChildCandidates_ExcludeParentsAndTheirAncestors()
    {
        var doc = ThreeGenerations();

        var ids = RelationshipRules.ChildCandidates(doc, "F", "M").Select(p => p.Id).ToArray();

        Assert.Equal(new[] { "S", "X" }, ids.OrderBy(x => x));
    }

    [Fact]
    public void ChildCandidates_WithoutParents_OfferEveryone()
    {
        var doc = ThreeGenerations();

        var ids = RelationshipRules.ChildCandidates(doc, null, null).Select(p => p.Id);

        Assert.Equal(doc.Individuals.Count, ids.Count());
    }

    /// <summary>Už uložené dítě zůstane v seznamu, i kdyby pravidlo nesplňovalo — jinak by se tiše ztratilo.</summary>
    [Fact]
    public void ChildCandidates_KeepAlreadyAssignedChildEvenIfInvalid()
    {
        var doc = ThreeGenerations();

        var ids = RelationshipRules.ChildCandidates(doc, "F", "M", alwaysInclude: new[] { "GF" })
            .Select(p => p.Id)
            .ToArray();

        Assert.Contains("GF", ids);
        Assert.DoesNotContain("GM", ids); // ostatní blokovaní zůstávají skrytí
    }

    /// <summary>Načtený GEDCOM už může cyklus obsahovat — výpočet předků se nesmí zacyklit.</summary>
    [Fact]
    public void Ancestors_SurvivesExistingCycleInData()
    {
        var doc = new GedcomDocument();
        doc.Individuals.Add(new GedcomIndividual { Id = "A" });
        doc.Individuals.Add(new GedcomIndividual { Id = "B" });
        // A je dítětem B a zároveň B je dítětem A.
        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "B", ChildrenIds = { "A" } });
        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "A", ChildrenIds = { "B" } });

        var ancestors = RelationshipRules.Ancestors(doc, "A");

        Assert.Equal(new[] { "A", "B" }, ancestors.OrderBy(x => x));
    }

    /// <summary>Dítě ve dvou rodinách: předci se berou z obou rodičovských linií.</summary>
    [Fact]
    public void Ancestors_ConsidersAllFamiliesWhereChildAppears()
    {
        var doc = new GedcomDocument();
        foreach (var id in new[] { "C", "P1", "P2" })
        {
            doc.Individuals.Add(new GedcomIndividual { Id = id });
        }

        doc.Families.Add(new GedcomFamily { Id = "F1", HusbandId = "P1", ChildrenIds = { "C" } });
        doc.Families.Add(new GedcomFamily { Id = "F2", HusbandId = "P2", ChildrenIds = { "C" } });

        Assert.Equal(new[] { "P1", "P2" }, RelationshipRules.Ancestors(doc, "C").OrderBy(x => x));
    }
}
