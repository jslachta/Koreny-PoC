using System.Text;
using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Rodné příjmení. V GEDCOM 5.5.1 je to samostatný záznam NAME s podřízeným „TYPE maiden“,
/// takže se osoba potkává s více NAME záznamy naráz — a testy hlídají hlavně to, aby si
/// hlavní a rodné jméno navzájem nepřepisovala, ani při čtení, ani při zápisu.
/// </summary>
public class MaidenNameTests
{
    private readonly GedcomParser _parser = new();

    private static string ReadCorpus(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", fileName);
        var bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private GedcomDocument Corpus() => _parser.Parse(ReadCorpus("corpus-09-rodne-prijmeni.ged"));

    [Fact]
    public void Parse_ReadsBothNames()
    {
        var marie = Corpus().Individuals.First(i => i.Id == "I1");

        Assert.Equal("Nováková", marie.Name?.Surname);
        Assert.Equal("Svobodová", marie.MaidenName?.Surname);
        Assert.Equal("Marie", marie.MaidenName?.GivenName);
    }

    /// <summary>Hodnotu TYPE předepisuje standard malými písmeny, exporty ji píší různě.</summary>
    [Fact]
    public void Parse_TypeIsCaseInsensitive()
    {
        var anna = Corpus().Individuals.First(i => i.Id == "I2");

        Assert.Equal("Černá", anna.MaidenName?.Surname);
    }

    /// <summary>
    /// „Poslední NAME vyhrává“ platí dál, ale rodné jméno se do té soutěže nepočítá —
    /// jinak by osobě po zadání rodného příjmení skočilo jméno na to dřívější.
    /// </summary>
    [Fact]
    public void Parse_MaidenNameNeverBecomesTheDisplayName()
    {
        var doc = Corpus();

        Assert.Equal("Nováková", doc.Individuals.First(i => i.Id == "I1").Name?.Surname);
        // U I2 stojí rodné jméno mezi hlavním a přezdívkou; vyhrát má „aka“, ne „maiden“.
        Assert.Equal("Anička /Dvořáková/", doc.Individuals.First(i => i.Id == "I2").Name?.Raw?.Trim());
    }

    [Fact]
    public void Parse_PersonWithoutMaidenName_HasNone()
    {
        Assert.Null(Corpus().Individuals.First(i => i.Id == "I3").MaidenName);
    }

    /// <summary>Načíst → uložit beze změny je sémantická identita i s několika NAME záznamy.</summary>
    [Fact]
    public void RoundTrip_IsLossless()
    {
        var original = ReadCorpus("corpus-09-rodne-prijmeni.ged");
        var exported = GedcomWriter.Write(_parser.Parse(original));

        var report = GedcomSemanticDiff.Compare(original, exported);
        Assert.True(report.IsEmpty, $"Round-trip není bezeztrátový.{Environment.NewLine}{report}");
    }

    /// <summary>Uložení beze změny nesmí sáhnout ani na hlavní, ani na rodné jméno.</summary>
    [Fact]
    public void Edit_SaveUnchanged_IsIdempotent()
    {
        var doc = Corpus();
        foreach (var ind in doc.Individuals)
        {
            var form = new PersonFormState();
            form.Fill(ind);
            form.ApplyTo(ind);
            GedcomSync.SyncIndividual(doc, ind);
        }

        var report = GedcomSemanticDiff.Compare(ReadCorpus("corpus-09-rodne-prijmeni.ged"), GedcomWriter.Write(doc));
        Assert.True(report.IsEmpty, $"Uložení beze změny změnilo soubor.{Environment.NewLine}{report}");
    }

    /// <summary>Rodné jméno bez křestního („/Zelená/“) se beze změny nesmí doplnit na plné.</summary>
    [Fact]
    public void Edit_SaveUnchanged_KeepsSurnameOnlyMaidenName()
    {
        var doc = Corpus();
        var eva = doc.Individuals.First(i => i.Id == "I4");

        var form = new PersonFormState();
        form.Fill(eva);
        Assert.Equal("Zelená", form.MaidenSurname);

        form.ApplyTo(eva);
        Assert.Equal("/Zelená/", eva.MaidenName?.Raw);
    }

    [Fact]
    public void Edit_AddMaidenName_WritesTypedNameRecord()
    {
        var doc = Corpus();
        var eva = doc.Individuals.First(i => i.Id == "I4");
        eva.MaidenName = null;
        GedcomSync.SyncIndividual(doc, eva);

        var form = new PersonFormState();
        form.Fill(eva);
        form.MaidenSurname = "Modrá";
        form.ApplyTo(eva);
        GedcomSync.SyncIndividual(doc, eva);

        var record = doc.Nodes.First(n => n.Xref == "I4");
        var names = record.Children.Where(c => c.Tag == "NAME").ToList();

        Assert.Equal(2, names.Count);
        Assert.Equal("Eva Bílá", names[0].Value.Replace("/", "").Trim());
        Assert.Equal("Eva /Modrá/", names[1].Value);
        Assert.Equal("maiden", names[1].FirstChild("TYPE")?.Value);
    }

    /// <summary>Změna hlavního příjmení se nesmí zapsat do rodného jména (a naopak).</summary>
    [Fact]
    public void Edit_RenameSurname_LeavesMaidenNameAlone()
    {
        var doc = Corpus();
        var marie = doc.Individuals.First(i => i.Id == "I1");

        var form = new PersonFormState();
        form.Fill(marie);
        form.Surname = "Procházková";
        form.ApplyTo(marie);
        GedcomSync.SyncIndividual(doc, marie);

        var names = doc.Nodes.First(n => n.Xref == "I1").Children.Where(c => c.Tag == "NAME").ToList();

        Assert.Equal("Marie /Procházková/", names[0].Value);
        Assert.Equal("Marie /Svobodová/", names[1].Value);
        Assert.Equal("maiden", names[1].FirstChild("TYPE")?.Value);
    }

    /// <summary>Vymazané rodné příjmení odstraní celý záznam včetně TYPE, ne jen jeho hodnotu.</summary>
    [Fact]
    public void Edit_ClearMaidenName_RemovesTheWholeRecord()
    {
        var doc = Corpus();
        var marie = doc.Individuals.First(i => i.Id == "I1");

        var form = new PersonFormState();
        form.Fill(marie);
        form.MaidenSurname = string.Empty;
        form.ApplyTo(marie);
        GedcomSync.SyncIndividual(doc, marie);

        var record = doc.Nodes.First(n => n.Xref == "I1");
        var names = record.Children.Where(c => c.Tag == "NAME").ToList();

        Assert.Single(names);
        Assert.Equal("Marie /Nováková/", names[0].Value);
        Assert.NotNull(record.FirstChild("_MHID")); // cizí tagy se úklidem nedotknou
    }

    /// <summary>Nová osoba z UI: rodné jméno vzniká rovnou jako druhý NAME s typem.</summary>
    [Fact]
    public void Edit_NewPersonWithMaidenName_GetsBothNames()
    {
        var doc = Corpus();
        var ind = new GedcomIndividual { Id = "I9" };
        doc.Individuals.Add(ind);

        var form = new PersonFormState();
        form.Given = "Jana";
        form.Surname = "Horáková";
        form.MaidenSurname = "Malá";
        form.ApplyTo(ind);
        GedcomSync.SyncIndividual(doc, ind);

        var names = doc.Nodes.First(n => n.Xref == "I9").Children.Where(c => c.Tag == "NAME").ToList();

        Assert.Equal("Jana /Horáková/", names[0].Value);
        Assert.Equal("Jana /Malá/", names[1].Value);
        Assert.Equal("maiden", names[1].FirstChild("TYPE")?.Value);
    }

    /// <summary>
    /// Nové rodné jméno se vkládá hned za hlavní, ne na konec záznamu. Pořadí sourozenců pod
    /// INDI sice standard neurčuje, ale jméno až za FAMC/FAMS dělá z minimálního diffu nečitelný
    /// záznam — a ten si uživatel podle principu 4 verzuje ve vlastním gitu.
    /// </summary>
    [Fact]
    public void Edit_AddMaidenName_LandsRightAfterThePrimaryName()
    {
        var doc = Corpus();
        var josef = doc.Individuals.First(i => i.Id == "I3");

        var form = new PersonFormState();
        form.Fill(josef);
        form.MaidenSurname = "Krátký";
        form.ApplyTo(josef);
        GedcomSync.SyncIndividual(doc, josef);

        var tags = doc.Nodes.First(n => n.Xref == "I3").Children.Select(c => c.Tag).ToArray();

        Assert.Equal(new[] { "NAME", "NAME", "SEX" }, tags);
    }

    /// <summary>Anonymizace bere rodné jméno stejně jako každý jiný identifikující údaj.</summary>
    [Fact]
    public void Anonymize_DropsMaidenName()
    {
        var doc = Corpus();
        var marie = doc.Individuals.First(i => i.Id == "I1");

        GedcomSync.AnonymizeIndividual(doc, marie);

        Assert.Null(marie.MaidenName);
        Assert.DoesNotContain(doc.Nodes.First(n => n.Xref == "I1").Children, c => c.Tag == "NAME");
    }
}
