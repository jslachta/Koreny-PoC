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

        SyncNames(node, ind);
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

    /// <summary>
    /// Smaže osobu — z doménové projekce, ze stromu i ze všech odkazů na ni
    /// (HUSB/WIFE/CHIL v rodinách, ale i případné ASSO a podobné ukazatele).
    ///
    /// Odkaz na neexistující záznam by jinak zůstal v exportu a jiný software by na něm mohl
    /// selhat. Uklízí se výhradně po VLASTNÍM smazání; odkazy, které už byly rozbité v importu,
    /// se neopravují — jinak by uložení beze změny měnilo cizí soubor (viz docs/principy.md).
    /// </summary>
    public static void RemoveIndividual(GedcomDocument doc, GedcomIndividual ind)
    {
        var id = ind.Id;

        foreach (var fam in doc.Families)
        {
            if (fam.HusbandId == id)
            {
                fam.HusbandId = null;
            }

            if (fam.WifeId == id)
            {
                fam.WifeId = null;
            }

            fam.ChildrenIds.RemoveAll(c => string.Equals(c, id, StringComparison.Ordinal));
        }

        doc.Individuals.Remove(ind);
        RemoveRecordAndReferences(doc, ind.SourceNode, id);
    }

    /// <summary>
    /// Tagy, které v anonymizovaném záznamu osoby zůstávají: drží vazby na rodiny,
    /// samy o osobě neříkají nic. Cokoli jiného je identifikující obsah.
    /// </summary>
    private static readonly string[] AnonymousIndividualTags = { "FAMC", "FAMS" };

    /// <summary>
    /// Alternativa ke smazání: záznam osoby zůstane a drží vazby, ale přijde o veškerý
    /// identifikující obsah — potomci tak neztratí rodiče ani prarodiče, jen se stanou
    /// dětmi neznámé osoby.
    ///
    /// Zahazuje se VŠE kromě FAMC/FAMS, tedy i tagy, kterým editor nerozumí (OCCU, CHR,
    /// citace, _MHID…). Ponechat je by z „neznámé osoby" udělalo lež: uživatel vidí prázdné
    /// políčko a v exportu by zůstalo povolání i rodné číslo. Ztráta dat je tu zamýšlená —
    /// proti smazání celého záznamu je to pořád ta šetrnější varianta.
    /// </summary>
    public static void AnonymizeIndividual(GedcomDocument doc, GedcomIndividual ind)
    {
        ind.Name = null;
        ind.MaidenName = null;
        ind.Sex = null;
        ind.Birth = null;
        ind.Death = null;
        ind.Notes.Clear();

        var node = ind.SourceNode;
        if (node is null)
        {
            SyncIndividual(doc, ind); // osoba vzniklá v UI a zatím neuložená do stromu
            return;
        }

        node.Children.RemoveAll(c => Array.IndexOf(AnonymousIndividualTags, c.Tag) < 0);
    }

    /// <summary>
    /// Smaže rodinu — ze seznamu, ze stromu i z odkazů na ni (FAMS/FAMC v záznamech osob).
    /// Doménový model FAMS/FAMC nedrží (odvozuje je), takže v surovém stromu by po smazání
    /// rodiny zůstaly viset.
    /// </summary>
    public static void RemoveFamily(GedcomDocument doc, GedcomFamily fam)
    {
        doc.Families.Remove(fam);
        RemoveRecordAndReferences(doc, fam.SourceNode, fam.Id);
    }

    private static void RemoveRecordAndReferences(GedcomDocument doc, GedcomNode? record, string id)
    {
        if (record is not null)
        {
            doc.Nodes.Remove(record);
        }

        foreach (var node in doc.Nodes)
        {
            RemoveReferencesTo(node, id);
        }

        doc.InvalidateLookups();
    }

    /// <summary>Rekurzivně odstraní uzly, jejichž hodnota je ukazatel na daný záznam (i s podstromem).</summary>
    private static void RemoveReferencesTo(GedcomNode node, string id)
    {
        node.Children.RemoveAll(c => IsPointerTo(c.Value, id));
        foreach (var child in node.Children)
        {
            RemoveReferencesTo(child, id);
        }
    }

    /// <summary>Hodnota musí mít tvar ukazatele „@ID@“ — prostý text shodný s ID se nemaže.</summary>
    private static bool IsPointerTo(string value, string id)
    {
        var t = value.Trim();
        return t.Length >= 2
            && t[0] == '@'
            && t[^1] == '@'
            && string.Equals(t[1..^1], id, StringComparison.Ordinal);
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

    /// <summary>
    /// Zapíše hlavní i rodné jméno. Obojí je NAME, takže se nesmí trefit do toho druhého:
    /// hlavní jde na poslední NAME bez „TYPE maiden“, rodné na ten s ním. Vymazané rodné
    /// příjmení celý ten záznam odstraní — prázdný NAME s TYPE by byl jen šum v exportu.
    /// </summary>
    private static void SyncNames(GedcomNode node, GedcomIndividual ind)
    {
        var primaryValue = ind.Name?.Raw?.Trim();
        var primary = GedcomNameTypes.LastPrimaryName(node);
        if (string.IsNullOrEmpty(primaryValue))
        {
            if (primary is not null)
            {
                node.Children.Remove(primary);
            }
        }
        else if (primary is null)
        {
            node.Children.Add(new GedcomNode("NAME", value: primaryValue));
        }
        else
        {
            primary.Value = primaryValue;
        }

        var maidenValue = ind.MaidenName?.Raw?.Trim();
        var maiden = GedcomNameTypes.MaidenNameNode(node);
        if (string.IsNullOrEmpty(maidenValue))
        {
            if (maiden is not null)
            {
                node.Children.Remove(maiden);
            }

            return;
        }

        if (maiden is null)
        {
            maiden = new GedcomNode("NAME", value: maidenValue);
            maiden.Children.Add(new GedcomNode("TYPE", value: GedcomNameTypes.Maiden));

            // Jména patří k sobě: pořadí sourozenců pod INDI sice standard neurčuje, ale nové
            // jméno až za FAMC/FAMS by z minimálního diffu udělalo záznam, který se nedá číst.
            // Bez jediného NAME (index −1) vyjde vložení na začátek záznamu, kam jméno patří.
            node.Children.Insert(node.Children.FindLastIndex(c => c.Tag == "NAME") + 1, maiden);
            return;
        }

        maiden.Value = maidenValue;
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
