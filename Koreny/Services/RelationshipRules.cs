using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Pravidla bránící vzniku cyklů v rodokmenu při editaci z UI.
///
/// Cyklus vznikne, když se dítě rodiny stane (nepřímo) vlastním předkem. Nestačí tedy
/// vyloučit jen manžela a manželku — vyloučit je nutné celou jejich předkovskou linii:
/// pokud by dědeček byl zapsán jako dítě svého vnuka, vznikne uzavřená smyčka.
///
/// Předci se počítají přes VŠECHNY rodiny, kde je osoba dítětem (ne jen přes první jako
/// <see cref="GedcomDocument.FindFamilyAsChild"/>) — u prevence cyklů je konzervativnější
/// přístup bezpečnější. Průchod je odolný vůči cyklům, které už mohou být v načteném
/// GEDCOMu (navštívené uzly se evidují), takže se nezacyklí.
/// </summary>
public static class RelationshipRules
{
    /// <summary>Všichni předci osoby (rodiče, prarodiče, …). Osoba sama ve výsledku není.</summary>
    public static HashSet<string> Ancestors(GedcomDocument doc, string? personId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (doc is null || string.IsNullOrEmpty(personId))
        {
            return result;
        }

        var queue = new Queue<string>();
        queue.Enqueue(personId);
        var visited = new HashSet<string>(StringComparer.Ordinal) { personId };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var fam in doc.Families)
            {
                if (!fam.ChildrenIds.Contains(current, StringComparer.Ordinal))
                {
                    continue;
                }

                foreach (var parentId in new[] { fam.HusbandId, fam.WifeId })
                {
                    if (string.IsNullOrEmpty(parentId))
                    {
                        continue;
                    }

                    result.Add(parentId);
                    if (visited.Add(parentId))
                    {
                        queue.Enqueue(parentId);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Smí být <paramref name="candidateId"/> dítětem rodiny s těmito rodiči?
    /// Nesmí jím být samotný rodič (osoba nemůže být svým vlastním dítětem) ani kterýkoli
    /// jeho předek (to by uzavřelo smyčku).
    /// </summary>
    public static bool CanBeChild(GedcomDocument doc, string? husbandId, string? wifeId, string candidateId)
    {
        if (string.IsNullOrEmpty(candidateId))
        {
            return false;
        }

        if (candidateId == husbandId || candidateId == wifeId)
        {
            return false;
        }

        return !Ancestors(doc, husbandId).Contains(candidateId)
            && !Ancestors(doc, wifeId).Contains(candidateId);
    }

    /// <summary>
    /// Osoby, které lze nabídnout jako děti rodiny s danými rodiči.
    /// <paramref name="alwaysInclude"/> (typicky už uložené děti editované rodiny) se v seznamu
    /// ponechají i tehdy, když pravidlo nesplňují — jinak by se při otevření formuláře tiše
    /// ztratila data importovaná z GEDCOMu, který cyklus už obsahuje.
    /// </summary>
    public static List<GedcomIndividual> ChildCandidates(
        GedcomDocument doc,
        string? husbandId,
        string? wifeId,
        IEnumerable<string>? alwaysInclude = null)
    {
        var keep = alwaysInclude is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(alwaysInclude, StringComparer.Ordinal);

        // Předky spočítáme jednou pro oba rodiče, ne pro každého kandidáta zvlášť.
        var blocked = Ancestors(doc, husbandId);
        blocked.UnionWith(Ancestors(doc, wifeId));
        if (!string.IsNullOrEmpty(husbandId))
        {
            blocked.Add(husbandId);
        }

        if (!string.IsNullOrEmpty(wifeId))
        {
            blocked.Add(wifeId);
        }

        return doc.Individuals
            .Where(p => keep.Contains(p.Id) || !blocked.Contains(p.Id))
            .ToList();
    }
}
