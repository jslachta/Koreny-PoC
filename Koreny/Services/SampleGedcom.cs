using System.Reflection;
using System.Text;

namespace Koreny.Services;

/// <summary>Přístup k vestavěnému vzorovému GEDCOMu (embedded resource Resources/sample.ged).</summary>
public static class SampleGedcom
{
    /// <summary>Manifest name embedded resource: {assembly}.{složka}.{soubor} → „Koreny.Resources.sample.ged".</summary>
    public const string ResourceName = "Koreny.Resources.sample.ged";

    public static string ReadText()
    {
        var assembly = typeof(SampleGedcom).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' nenalezen.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
