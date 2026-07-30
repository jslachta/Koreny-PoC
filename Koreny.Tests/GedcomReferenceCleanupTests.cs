using System.Text;
using Koreny.Models;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Mazání nesmí v dokumentu nechat ukazatele na neexistující záznam — export by jinak
/// jinému softwaru podstrčil rozbitý odkaz (docs/principy.md, princip 5).
/// Zároveň se nesmí „opravovat" odkazy, které už byly rozbité v importu (princip 3).
/// </summary>
public class GedcomReferenceCleanupTests
{
    private readonly GedcomParser _parser = new();

    private static string ReadCorpus(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", fileName);
        var bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Vypíše všechny ukazatele „@X@“ v exportu, které nemají cílový záznam.</summary>
    private static List<string> DanglingPointers(string gedcom)
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var used = new List<string>();

        foreach (var raw in gedcom.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split(' ', 3);
            if (parts.Length >= 2 && parts[1].StartsWith('@') && parts[1].EndsWith('@'))
            {
                defined.Add(parts[1][1..^1]); // „0 @I1@ INDI“ — definice záznamu
                continue;
            }

            if (parts.Length >= 3)
            {
                var value = parts[2].Trim();
                if (value.Length >= 2 && value[0] == '@' && value[^1] == '@')
                {
                    used.Add(value[1..^1]);
                }
            }
        }

        return used.Where(u => !defined.Contains(u)).Distinct(StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void SampleItselfHasNoDanglingPointers()
    {
        // Kontrola měřidla: vzorek je konzistentní, takže nález v dalších testech je náš.
        Assert.Empty(DanglingPointers(SampleGedcom.ReadText()));
    }

    [Fact]
    public void DeletingPerson_RemovesPointersToThem()
    {
        var doc = _parser.Parse(SampleGedcom.ReadText());
        var harry = doc.Individuals.First(i => i.Id == "I00001");

        GedcomSync.RemoveIndividual(doc, harry);
        var exported = GedcomWriter.Write(doc);

        Assert.DoesNotContain("@I00001@", exported);
        Assert.Empty(DanglingPointers(exported));
        Assert.DoesNotContain(doc.Individuals, i => i.Id == "I00001");
    }

    [Fact]
    public void DeletingFamily_RemovesFamsAndFamcPointers()
    {
        var doc = _parser.Parse(SampleGedcom.ReadText());
        var fam = doc.Families.First(f => f.Id == "F00001");

        GedcomSync.RemoveFamily(doc, fam);
        var exported = GedcomWriter.Write(doc);

        Assert.DoesNotContain("@F00001@", exported);
        Assert.Empty(DanglingPointers(exported));
    }

    [Fact]
    public void DeletingPerson_LeavesOtherRecordsIntact()
    {
        var doc = _parser.Parse(ReadCorpus("corpus-07-fullgraph.ged"));
        var before = doc.Individuals.Count;

        // I5 (Eva) je WIFE v F2 a CHIL v F3.
        GedcomSync.RemoveIndividual(doc, doc.Individuals.First(i => i.Id == "I5"));
        var exported = GedcomWriter.Write(doc);

        Assert.Equal(before - 1, doc.Individuals.Count);
        Assert.DoesNotContain("@I5@", exported);
        Assert.Contains("@I6@", exported);   // dítě z téže rodiny zůstává
        Assert.Contains("@I3@", exported);   // manžel zůstává
        Assert.Empty(DanglingPointers(exported));

        var f2 = doc.Families.First(f => f.Id == "F2");
        Assert.Null(f2.WifeId);
        Assert.Equal(new[] { "I6" }, f2.ChildrenIds);
    }

    /// <summary>Ukazatel je jen hodnota tvaru „@ID@“ — prostý text se stejným obsahem se nemaže.</summary>
    [Fact]
    public void PlainTextEqualToIdIsNotTreatedAsPointer()
    {
        var doc = new GedcomDocument();
        var indi = new GedcomNode("INDI", xref: "I1");
        indi.Children.Add(new GedcomNode("NOTE", value: "I1"));       // prostý text
        var other = new GedcomNode("INDI", xref: "I2");
        other.Children.Add(new GedcomNode("NOTE", value: "I1"));      // taky text
        other.Children.Add(new GedcomNode("ASSO", value: "@I1@"));    // skutečný ukazatel
        doc.Nodes.Add(indi);
        doc.Nodes.Add(other);
        var person = new GedcomIndividual { Id = "I1", SourceNode = indi };
        doc.Individuals.Add(person);
        doc.Individuals.Add(new GedcomIndividual { Id = "I2", SourceNode = other });

        GedcomSync.RemoveIndividual(doc, person);

        Assert.DoesNotContain(other.Children, c => c.Tag == "ASSO");
        Assert.Contains(other.Children, c => c.Tag == "NOTE" && c.Value == "I1");
    }

    /// <summary>
    /// Rozbitý odkaz, který přišel z importu, se neopravuje — uložení beze změny
    /// nesmí měnit cizí soubor (idempotence commitu).
    /// </summary>
    [Fact]
    public void PreexistingDanglingPointerIsPreservedOnUnchangedSave()
    {
        const string gedcom = "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME A /B/\n1 FAMS @FGONE@\n0 TRLR\n";

        var doc = _parser.Parse(gedcom);
        var exported = GedcomWriter.Write(doc);

        Assert.Contains("@FGONE@", exported);
        Assert.Equal(new[] { "FGONE" }, DanglingPointers(exported));
    }
}
