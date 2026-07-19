using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Promítá edity z doménového modelu zpět do stromu <see cref="GedcomNode"/>
/// (viz docs/audit-gedcom.md, sekce 4, krok 3).
///
/// Přepisuje/vytváří/maže VÝHRADNĚ uzly, které UI umí editovat: u osoby NAME, SEX,
/// BIRT/DATE/PLAC, DEAT/DATE/PLAC, NOTE; u rodiny HUSB, WIFE, CHIL, MARR/DATE/PLAC, NOTE.
/// Sourozenecké uzly, kterým UI nerozumí (_MHID, OCCU, CHR, citace SOUR, FAMC/PEDI…),
/// zůstávají nedotčené na svém místě.
///
/// Známé tagy s možným vícenásobným výskytem (NAME, BIRT, DEAT, MARR) se synchronizují
/// na POSLEDNÍM výskytu — stejně, jako je čte projekce (poslední vyhrává). Neupravená
/// osoba/rodina se tak přepíše sama na sebe a její případné další výskyty téhož tagu
/// zůstanou zachovány.
/// </summary>
public static class GedcomSync
{
    public static void SyncIndividual(GedcomDocument doc, GedcomIndividual ind)
    {
        var node = ind.SourceNode;
        if (node is null)
        {
            node = new GedcomNode("INDI", xref: ind.Id);
            ind.SourceNode = node;
            InsertRecordBeforeTrlr(doc, node);
        }

        SetSingleValue(node, "NAME", ind.Name?.Raw?.Trim());
        SetSingleValue(node, "SEX", ind.Sex);
        SyncEvent(node, "BIRT", ind.Birth);
        SyncEvent(node, "DEAT", ind.Death);
        SyncNotes(node, ind.Notes);
    }

    public static void SyncFamily(GedcomDocument doc, GedcomFamily fam)
    {
        var node = fam.SourceNode;
        if (node is null)
        {
            node = new GedcomNode("FAM", xref: fam.Id);
            fam.SourceNode = node;
            InsertRecordBeforeTrlr(doc, node);
        }

        SetSingleValue(node, "HUSB", Pointer(fam.HusbandId));
        SetSingleValue(node, "WIFE", Pointer(fam.WifeId));
        SyncChildren(node, fam.ChildrenIds);
        SyncEvent(node, "MARR", fam.Marriage);
        SyncNotes(node, fam.Notes);
    }

    /// <summary>Odstraní uzel osoby ze stromu. Reference v rodinách (HUSB/WIFE/CHIL) se ZÁMĚRNĚ neuklízejí (viz zadání Session 2).</summary>
    public static void RemoveIndividualNode(GedcomDocument doc, GedcomIndividual ind)
    {
        if (ind.SourceNode is not null)
        {
            doc.Nodes.Remove(ind.SourceNode);
        }
    }

    /// <summary>Odstraní uzel rodiny ze stromu.</summary>
    public static void RemoveFamilyNode(GedcomDocument doc, GedcomFamily fam)
    {
        if (fam.SourceNode is not null)
        {
            doc.Nodes.Remove(fam.SourceNode);
        }
    }

    // ------------------------------------------------------------- pomocné

    private static string? Pointer(string? id) =>
        string.IsNullOrEmpty(id) ? null : $"@{id}@";

    private static void InsertRecordBeforeTrlr(GedcomDocument doc, GedcomNode record)
    {
        var idx = doc.Nodes.FindIndex(n => n.Tag == "TRLR");
        if (idx < 0)
        {
            doc.Nodes.Add(record);
        }
        else
        {
            doc.Nodes.Insert(idx, record);
        }
    }

    /// <summary>Nastaví hodnotu posledního výskytu tagu (vytvoří na konci, když chybí); prázdná hodnota poslední výskyt odstraní.</summary>
    private static void SetSingleValue(GedcomNode parent, string tag, string? value)
    {
        var existing = parent.LastChild(tag);
        if (string.IsNullOrEmpty(value))
        {
            if (existing is not null)
            {
                parent.Children.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            parent.Children.Add(new GedcomNode(tag, value: value));
        }
        else
        {
            existing.Value = value;
        }
    }

    /// <summary>
    /// Synchronizuje událost na posledním výskytu tagu. Prázdná událost bez data i místa
    /// (např. „BIRT Y“) se zachová i s původní hodnotou — hodnota se maže jen tehdy,
    /// když událost dostane DATE/PLAC.
    /// </summary>
    private static void SyncEvent(GedcomNode parent, string tag, GedcomEvent? ev)
    {
        var node = parent.LastChild(tag);

        if (ev is null)
        {
            if (node is not null)
            {
                parent.Children.Remove(node);
            }

            return;
        }

        node ??= AddChild(parent, tag);

        SetSingleValue(node, "DATE", ev.Date);
        SetSingleValue(node, "PLAC", ev.Place);

        if (ev.Date is not null || ev.Place is not null)
        {
            node.Value = string.Empty;
        }
    }

    private static GedcomNode AddChild(GedcomNode parent, string tag)
    {
        var node = new GedcomNode(tag);
        parent.Children.Add(node);
        return node;
    }

    /// <summary>Ponechá existující CHIL uzly (i s podtagy jako _FREL) pro ID, která přetrvávají, v původním pořadí; odebere zmizející, přidá nové na konec.</summary>
    private static void SyncChildren(GedcomNode parent, IReadOnlyList<string> childIds)
    {
        var wanted = new List<string>(childIds);

        // Odeber CHIL uzly, jejichž ID už není mezi dětmi.
        for (var i = parent.Children.Count - 1; i >= 0; i--)
        {
            var c = parent.Children[i];
            if (c.Tag != "CHIL")
            {
                continue;
            }

            var id = Unpointer(c.Value);
            var pos = wanted.IndexOf(id);
            if (pos < 0)
            {
                parent.Children.RemoveAt(i);
            }
            else
            {
                // Toto ID je pokryté existujícím uzlem; nebudeme ho znovu přidávat.
                wanted[pos] = null!;
            }
        }

        // Přidej nové CHIL uzly na konec (pořadí dle childIds).
        foreach (var id in wanted)
        {
            if (id is not null)
            {
                parent.Children.Add(new GedcomNode("CHIL", value: $"@{id}@"));
            }
        }
    }

    private static void SyncNotes(GedcomNode parent, IReadOnlyList<string> notes)
    {
        var noteNodes = parent.Children.Where(c => c.Tag == "NOTE").ToList();

        for (var i = 0; i < notes.Count; i++)
        {
            if (i < noteNodes.Count)
            {
                noteNodes[i].Value = notes[i];
            }
            else
            {
                parent.Children.Add(new GedcomNode("NOTE", value: notes[i]));
            }
        }

        for (var i = noteNodes.Count - 1; i >= notes.Count; i--)
        {
            parent.Children.Remove(noteNodes[i]);
        }
    }

    private static string Unpointer(string raw)
    {
        var t = raw.Trim();
        if (t.Length >= 2 && t[0] == '@' && t[^1] == '@')
        {
            return t[1..^1];
        }

        return t;
    }
}
