using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Co se stane při smazání osoby. Mazání nikdy nekaskáduje — potomci ani rodiny se nemažou —
/// ale vazby vedoucí „přes" smazanou osobu se přetrhnou a rodokmen se může rozpadnout na ostrovy.
/// Uživatel to má vědět PŘED potvrzením, ne až podle exportu (viz docs/principy.md, princip 7).
/// </summary>
public sealed record PersonDeletionImpact(
    string PersonName,
    int SpouseFamilyCount,
    int ChildFamilyCount,
    IReadOnlyList<string> LosingParentNames,
    IReadOnlyList<string> EmptiedFamilyIds)
{
    /// <summary>Je osoba vůbec na něco navázaná? Bez vazeb nemá „zachovat jako neznámou" smysl.</summary>
    public bool HasAnyLinks => SpouseFamilyCount > 0 || ChildFamilyCount > 0;
}

public static class DeletionImpact
{
    public static PersonDeletionImpact ForPerson(GedcomDocument doc, GedcomIndividual person)
    {
        var id = person.Id;
        var spouseFamilies = doc.Families
            .Where(f => f.HusbandId == id || f.WifeId == id)
            .ToList();
        var childFamilies = doc.Families
            .Where(f => f.ChildrenIds.Contains(id, StringComparer.Ordinal))
            .ToList();

        // Děti z rodin, kde je osoba rodičem, přijdou o tohoto rodiče.
        var losingParent = spouseFamilies
            .SelectMany(f => f.ChildrenIds)
            .Distinct(StringComparer.Ordinal)
            .Where(cid => !string.Equals(cid, id, StringComparison.Ordinal))
            .Select(doc.FindIndividual)
            .Where(p => p is not null)
            .Select(p => PersonDisplay.Name(p))
            .ToList();

        // Rodiny, ze kterých po odebrání osoby nezbude vůbec nic.
        var emptied = spouseFamilies.Concat(childFamilies)
            .Distinct()
            .Where(f => WouldBeEmpty(f, id))
            .Select(f => f.Id)
            .ToList();

        return new PersonDeletionImpact(
            PersonDisplay.Name(person),
            spouseFamilies.Count,
            childFamilies.Count,
            losingParent,
            emptied);
    }

    private static bool WouldBeEmpty(GedcomFamily fam, string removedId)
    {
        var husb = string.Equals(fam.HusbandId, removedId, StringComparison.Ordinal) ? null : fam.HusbandId;
        var wife = string.Equals(fam.WifeId, removedId, StringComparison.Ordinal) ? null : fam.WifeId;
        var children = fam.ChildrenIds.Count(c => !string.Equals(c, removedId, StringComparison.Ordinal));

        return string.IsNullOrEmpty(husb) && string.IsNullOrEmpty(wife) && children == 0;
    }
}
