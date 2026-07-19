using System.Text;
using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Editační sada — testuje druhou půlku slibu bezeztrátovosti: „vše, co jste přidali
/// nebo upravili, přežije a vše ostatní zůstane nedotčené na svém místě“.
///
/// Testy pracují na úrovni služeb (GedcomParser → doménová úprava → GedcomSync → GedcomWriter),
/// nikoli přes UI formulář Index.razor: formulář je záměrně ztrátový (rok místo plného data,
/// poslední jméno vyhrává) a testovaly bychom jeho omezení, ne kontrakt syncu. Očekávané
/// výstupy v Corpus/Expected/ jsou psané RUČNĚ (ne generované writerem).
/// </summary>
public class GedcomEditPreservationTests
{
    private readonly GedcomParser _parser = new();

    private static string Read(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        var bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void AssertMatches(string exported, string expectedFile)
    {
        var expected = Read(Path.Combine("Corpus", "Expected", expectedFile));
        var report = GedcomSemanticDiff.Compare(expected, exported);
        Assert.True(report.IsEmpty, $"Export neodpovídá {expectedFile}.{Environment.NewLine}{report}");
    }

    /// <summary>
    /// (a) Přejmenování osoby @I1@ v corpus-01: změní se NAME, ale proprietární tagy
    /// (_MHID, _UID, RIN, CHAN) i pořadí uzlů zůstávají nedotčené na svém místě.
    /// </summary>
    [Fact]
    public void Edit_RenameIndividual_PreservesProprietaryTagsAndOrder()
    {
        var doc = _parser.Parse(Read(Path.Combine("Corpus", "corpus-01-myheritage.ged")));
        var ind = doc.Individuals.First(i => i.Id == "I1");

        // Úprava tak, jak by ji provedlo UI (příjmení-first slash formát; viz Index.razor ApplyPersonForm).
        ind.Name = new GedcomName { GivenName = "Honza", Surname = "Novák", Raw = "/Novák/ Honza" };
        GedcomSync.SyncIndividual(doc, ind);

        AssertMatches(GedcomWriter.Write(doc), "edit-a-rename.ged");
    }

    /// <summary>
    /// (b) Přidání data úmrtí osobě @I1@ v corpus-04 (má OCCU a CHR): DEAT dostane DATE,
    /// zatímco OCCU/CHR/EVEN/BURI, „BIRT Y“ i druhé NAME zůstávají zachované.
    /// </summary>
    [Fact]
    public void Edit_AddDeathDate_KeepsUnknownSiblingEvents()
    {
        var doc = _parser.Parse(Read(Path.Combine("Corpus", "corpus-04-events.ged")));
        var ind = doc.Individuals.First(i => i.Id == "I1");

        ind.Death = new GedcomEvent { Date = "13 DEC 1957" };
        GedcomSync.SyncIndividual(doc, ind);

        AssertMatches(GedcomWriter.Write(doc), "edit-b-add-death.ged");
    }

    /// <summary>
    /// (c) Nová osoba @I11@ zařazená jako CHIL do @F1@ v corpus-05: nový INDI záznam
    /// vznikne před TRLR, existující CHIL (i s _FREL/_MREL) si drží pořadí a nový CHIL
    /// přibude na konec.
    /// </summary>
    [Fact]
    public void Edit_AddPersonAndChild_AppendsWithoutDisturbingExisting()
    {
        var doc = _parser.Parse(Read(Path.Combine("Corpus", "corpus-05-relations.ged")));

        var newInd = new GedcomIndividual
        {
            Id = "I11",
            Name = new GedcomName { GivenName = "Petr", Surname = "Horák", Raw = "/Horák/ Petr" },
            Sex = "M",
        };
        doc.Individuals.Add(newInd);
        GedcomSync.SyncIndividual(doc, newInd);

        var fam = doc.Families.First(f => f.Id == "F1");
        fam.ChildrenIds.Add("I11");
        GedcomSync.SyncFamily(doc, fam);

        AssertMatches(GedcomWriter.Write(doc), "edit-c-add-child.ged");
    }

    /// <summary>
    /// (d) Smazání osoby @I4@ v corpus-05: zmizí jen její INDI záznam. Ostatní záznamy
    /// zůstávají beze změny — včetně reference WIFE @I4@ v @F2@, kterou sync záměrně
    /// neuklízí (viz zadání Session 2).
    /// </summary>
    [Fact]
    public void Edit_DeleteIndividual_RemovesOnlyItsRecord()
    {
        var doc = _parser.Parse(Read(Path.Combine("Corpus", "corpus-05-relations.ged")));
        var ind = doc.Individuals.First(i => i.Id == "I4");

        doc.Individuals.Remove(ind);
        GedcomSync.RemoveIndividualNode(doc, ind);

        AssertMatches(GedcomWriter.Write(doc), "edit-d-delete.ged");
    }

    /// <summary>
    /// (e) IDEMPOTENCE: pro každý korpus otevři a ulož BEZE ZMĚNY každou osobu i rodinu
    /// (Fill → ApplyTo → sync — stejná cesta jako UI). Export musí být sémanticky totožný
    /// s importem. Toto je hlavní důkaz, že editační formulář už není ztrátový.
    /// </summary>
    [Theory]
    [InlineData("corpus-01-myheritage.ged")]
    [InlineData("corpus-02-ancestry.ged")]
    [InlineData("corpus-03-notes.ged")]
    [InlineData("corpus-04-events.ged")]
    [InlineData("corpus-05-relations.ged")]
    [InlineData("corpus-06-utf8-diakritika.ged")]
    public void Edit_SaveUnchanged_IsIdempotent(string corpusFile)
    {
        var original = Read(Path.Combine("Corpus", corpusFile));
        var doc = _parser.Parse(original);

        foreach (var ind in doc.Individuals.ToList())
        {
            var form = new PersonFormState();
            form.Fill(ind);
            form.ApplyTo(ind);
            GedcomSync.SyncIndividual(doc, ind);
        }

        foreach (var fam in doc.Families.ToList())
        {
            var form = new FamilyFormState();
            form.Fill(fam);
            form.ApplyTo(fam);
            GedcomSync.SyncFamily(doc, fam);
        }

        var report = GedcomSemanticDiff.Compare(original, GedcomWriter.Write(doc));
        Assert.True(report.IsEmpty, $"Uložení beze změny změnilo {corpusFile}.{Environment.NewLine}{report}");
    }

    /// <summary>
    /// (f) Změna jen křestního jména osobě s plným datem narození: export nese změněné jméno
    /// A zachované plné datum „3 MAR 1901" (žádná degradace na rok).
    /// </summary>
    [Fact]
    public void Edit_RenameKeepsFullBirthDate()
    {
        var doc = _parser.Parse(Read(Path.Combine("Corpus", "corpus-01-myheritage.ged")));
        var ind = doc.Individuals.First(i => i.Id == "I1");

        var form = new PersonFormState();
        form.Fill(ind);
        form.Given = "Honza"; // změna jen jména; příjmení, datum, místo beze změny
        form.ApplyTo(ind);
        GedcomSync.SyncIndividual(doc, ind);

        var exported = GedcomWriter.Write(doc);
        Assert.Contains("/Novák/ Honza", exported);
        Assert.Contains("3 MAR 1901", exported);
        Assert.DoesNotContain("Jan /Novák/", exported);
    }
}
