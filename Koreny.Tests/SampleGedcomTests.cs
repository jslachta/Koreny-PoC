using Koreny.Services;

namespace Koreny.Tests;

/// <summary>Vestavěný vzorový GEDCOM: parsuje se bez chyb, sedí počty a round-trip je sémanticky čistý.</summary>
public class SampleGedcomTests
{
    [Fact]
    public void Sample_RoundTripsClean()
    {
        var text = SampleGedcom.ReadText();
        var doc = new GedcomParser().Parse(text);
        var exported = GedcomWriter.Write(doc);

        var report = GedcomSemanticDiff.Compare(text, exported);
        Assert.True(report.IsEmpty, $"Vzorový GEDCOM neroundtripuje čistě.{Environment.NewLine}{report}");
    }
}
