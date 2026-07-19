using System.Text;
using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Round-trip testy import → export nad korpusem v Koreny.Tests/Corpus/.
///
/// POZOR: tyto testy jsou SPECIFIKACE, ne regrese. Podle auditu
/// (docs/audit-gedcom.md, sekce „Co by dnes ztratil round-trip“, body 1–9)
/// dnes MUSÍ selhávat; zelené budou až po přestavbě parseru/writeru na
/// bezeztrátový raw strom. Čísla nálezů v komentářích odkazují na body 1–9
/// v auditu.
/// </summary>
public class GedcomRoundTripTests
{
    private readonly GedcomParser _parser = new();

    /// <summary>Načte korpusový soubor stejnou cestou jako aplikace (Index.razor:943–945).</summary>
    private static string ReadCorpus(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", fileName);
        var bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private void AssertRoundTrip(string corpusFile)
    {
        var original = ReadCorpus(corpusFile);
        var doc = _parser.Parse(original);
        var exported = GedcomWriter.Write(doc);
        var report = GedcomSemanticDiff.Compare(original, exported);

        Assert.True(
            report.IsEmpty,
            $"Round-trip {corpusFile} není bezeztrátový.{Environment.NewLine}{report}");
    }

    /// <summary>
    /// Styl MyHeritage: _MHID/_UID/RIN/CHAN na záznamech, bohatší hlavička.
    /// Očekávaná selhání dle auditu: nález 1 (hlavička — LANG, SOUR podstrom),
    /// nález 5 (proprietární tagy _MHID, _UID; dále RIN a CHAN).
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus01_MyHeritage() => AssertRoundTrip("corpus-01-myheritage.ged");

    /// <summary>
    /// Styl Ancestry: top-level SOUR/REPO/OBJE záznamy, citace s PAGE/QUAY/_APID, FAMS.
    /// Očekávaná selhání dle auditu: nález 2 (záznamy SOUR/REPO/OBJE),
    /// nález 3 (citace u BIRT, FAMS odkazy), nález 5 (_APID).
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus02_Ancestry() => AssertRoundTrip("corpus-02-ancestry.ged");

    /// <summary>
    /// Poznámky: CONT/CONC lámání (včetně CONC uprostřed slova), NOTE pointer @N1@,
    /// top-level NOTE záznam, poznámka delší než 255 znaků.
    /// Očekávaná selhání dle auditu: nález 6 (víceřádkové poznámky oříznuté na první
    /// fyzický řádek; pointer zůstane textově, ale cílový @N1@ záznam zmizí — nález 2).
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus03_Notes() => AssertRoundTrip("corpus-03-notes.ged");

    /// <summary>
    /// Události a jména: BIRT Y / DEAT Y, dvě NAME, podtagy jména (GIVN/SURN/NICK/TYPE),
    /// CHR/OCCU/EVEN/BURI, druhé MARR, ENGA, DIV.
    /// Očekávaná selhání dle auditu: nález 3 (události a atributy osob, druhá jména,
    /// podtagy jmen), nález 4 (ENGA/DIV/druhé MARR — „poslední MARR vyhrává“),
    /// nález 7 (BIRT Y / DEAT Y zmizí úplně).
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus04_Events() => AssertRoundTrip("corpus-04-events.ged");

    /// <summary>
    /// Vztahy: osoba jako CHIL ve dvou rodinách (FAMC + PEDI adopted), _FREL/_MREL,
    /// vícenásobná manželství, pořadí záznamů I10 před I2 v exportu (ordinální řazení)
    /// a pořadí podtagů (@I2@ má SEX před NAME, writer pořadí obrací).
    /// Očekávaná selhání dle auditu: nález 3 (FAMC/PEDI), nález 5 (_FREL/_MREL),
    /// nález 8 (pořadí záznamů — writer řadí ordinálně podle ID, GedcomWriter.cs:15 —
    /// i pořadí podtagů, které writer generuje v pevném pořadí).
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus05_Relations() => AssertRoundTrip("corpus-05-relations.ged");

    /// <summary>
    /// UTF-8 s diakritikou, bez BOM. Podle auditu NAME/DATE/PLAC/NOTE (jednořádkové)
    /// a vazby přežívají — tento test by měl jako jediný z korpusu PROJÍT a slouží
    /// jako pozitivní kontrola, že diff nehlásí falešné rozdíly.
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus06_Utf8NoBom() => AssertRoundTrip("corpus-06-utf8-diakritika.ged");

    /// <summary>
    /// Táž data s UTF-8 BOM na začátku — BOM přidán za běhu, aby varianta nezávisela
    /// na tom, jak editor/git s BOM soubory zachází. Čtení kopíruje aplikaci
    /// (StreamReader s výchozí detekcí BOM), takže by měl také PROJÍT.
    /// </summary>
    [Fact]
    public void RoundTrip_Corpus06_Utf8WithBom()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Corpus", "corpus-06-utf8-diakritika.ged");
        var bytes = File.ReadAllBytes(path);
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(bytes).ToArray();

        using var reader = new StreamReader(new MemoryStream(withBom), Encoding.UTF8);
        var original = reader.ReadToEnd();

        var doc = _parser.Parse(original);
        var exported = GedcomWriter.Write(doc);
        var report = GedcomSemanticDiff.Compare(original, exported);

        Assert.True(
            report.IsEmpty,
            $"Round-trip corpus-06 (s BOM) není bezeztrátový.{Environment.NewLine}{report}");
    }

    /// <summary>
    /// Placeholder: ANSEL kódování je oddělená session. Aplikace dnes čte vždy UTF-8
    /// (Index.razor:944), takže ANSEL vstup skončí mojibake — viz docs/audit-gedcom.md,
    /// sekce 2.2 „Kódování“ a nález 9. Korpusový soubor se záměrně nepíše.
    /// </summary>
    [Fact(Skip = "ANSEL kódování řeší samostatná session — viz docs/audit-gedcom.md, sekce 2.2 „Kódování“ (nález 9).")]
    public void RoundTrip_Ansel_Placeholder()
    {
    }
}
