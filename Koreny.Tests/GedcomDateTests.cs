using Koreny.Services;

namespace Koreny.Tests;

/// <summary>
/// Čtení a české vypsání data. Očekávané texty odpovídají tomu, co ze stejného data nakreslí
/// Topola v overlayi (locale „cs") — v tom je celý smysl toho, mít vlastní parser: kdyby se
/// obě strany rozešly, tatáž osoba by měla v náhledu jiné datum než v živém rodokmenu.
/// </summary>
public class GedcomDateTests
{
    [Theory]
    [InlineData("3 MAR 1901", "3. 3. 1901")]
    [InlineData("12 MAY 1880", "12. 5. 1880")]
    [InlineData("MAR 1901", "březen 1901")]
    [InlineData("1901", "1901")]
    [InlineData("3 mar 1901", "3. 3. 1901")]
    public void PlainDate_IsWrittenInCzech(string raw, string expected)
    {
        Assert.Equal(expected, GedcomDate.Format(raw));
    }

    /// <summary>Nejistota je součástí údaje — zahodit ji by z odhadu udělalo fakt.</summary>
    [Theory]
    [InlineData("ABT 1850", "okolo 1850")]
    [InlineData("EST 3 MAR 1901", "odhadem 3. 3. 1901")]
    [InlineData("CAL 1901", "vypočt. 1901")]
    public void Qualifier_IsKept(string raw, string expected)
    {
        Assert.Equal(expected, GedcomDate.Format(raw));
    }

    [Theory]
    [InlineData("BET 1840 AND 1845", "1840 .. 1845")]
    [InlineData("BEF 1900", "před 1900")]
    [InlineData("AFT 3 MAR 1901", "po 3. 3. 1901")]
    [InlineData("FROM 1920 TO 1925", "1920 .. 1925")]
    [InlineData("FROM 1920", "po 1920")]
    [InlineData("TO 1925", "před 1925")]
    public void Range_IsWrittenAsBoundaries(string raw, string expected)
    {
        Assert.Equal(expected, GedcomDate.Format(raw));
    }

    /// <summary>Escape kalendáře je pro čtenáře šum; datum za ním se přečte normálně.</summary>
    [Fact]
    public void CalendarEscape_IsDropped()
    {
        Assert.Equal("3. 3. 1901", GedcomDate.Format("@#DJULIAN@ 3 MAR 1901"));
    }

    /// <summary>Fráze v závorkách je v GEDCOM vysvětlení data; obsah se zkusí přečíst.</summary>
    [Fact]
    public void ParenthesizedDate_IsRead()
    {
        Assert.Equal("1901", GedcomDate.Format("(1901)"));
    }

    /// <summary>
    /// Co se přečíst nedá, se vrátí tak, jak v souboru stojí — nikdy se z poloviny rozpoznaného
    /// data nesmí stát datum, které tam nikdo nenapsal (princip 3).
    /// </summary>
    [Theory]
    [InlineData("kolem sv. Václava")]
    [InlineData("1901/02")]
    [InlineData("BET 1840")]
    public void UnreadableDate_StaysAsItIs(string raw)
    {
        Assert.Equal(raw, GedcomDate.Format(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDate_IsNull(string? raw)
    {
        Assert.Null(GedcomDate.Format(raw));
        Assert.Null(GedcomDate.Parse(raw));
    }

    /// <summary>Části data jdou i do Topoly, takže musí sedět, ne jen jejich vypsání.</summary>
    [Fact]
    public void Parse_SplitsDateIntoParts()
    {
        var value = GedcomDate.Parse("ABT 3 MAR 1901");

        Assert.NotNull(value);
        Assert.Equal(new GedcomDateParts("abt", 3, 3, 1901, null), value!.Date);
        Assert.Null(value.From);
        Assert.Null(value.To);
    }

    [Fact]
    public void Parse_SplitsRangeIntoBoundaries()
    {
        var value = GedcomDate.Parse("BET 1840 AND MAR 1845");

        Assert.NotNull(value);
        Assert.Null(value!.Date);
        Assert.Equal(new GedcomDateParts(null, null, null, 1840, null), value.From);
        Assert.Equal(new GedcomDateParts(null, null, 3, 1845, null), value.To);
    }
}
