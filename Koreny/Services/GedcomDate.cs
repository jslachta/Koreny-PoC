using System.Globalization;
using System.Text.RegularExpressions;

namespace Koreny.Services;

/// <summary>
/// Jedno datum rozložené na části: den, měsíc, rok a kvalifikátor nejistoty („ABT", „CAL", „EST").
/// Co se přečíst nedá, zůstane v <see cref="Text"/> jako původní řetězec — zobrazení nikdy nic
/// nedomýšlí ani nezahazuje (princip 3).
/// </summary>
public sealed record GedcomDateParts(string? Qualifier, int? Day, int? Month, int? Year, string? Text);

/// <summary>
/// Datum, nebo rozmezí. GEDCOM zná obojí: „3 MAR 1901" je datum, „BET 1840 AND 1845",
/// „BEF 1900" či „FROM 1920 TO 1925" jsou rozmezí s jednou nebo dvěma hranicemi.
/// </summary>
public sealed record GedcomDateValue(GedcomDateParts? Date, GedcomDateParts? From, GedcomDateParts? To);

/// <summary>
/// Čtení data GEDCOM události a jeho české vypsání („3. 3. 1901", „okolo 1850", „před 1900").
///
/// Pravidla čtení i výsledný text schválně kopírují Topolu (její <c>date_format</c> a GEDCOM
/// čtečku): overlay si datum vypisuje sám ze struktury, kterou mu pošleme, takže kdyby se obě
/// strany rozešly, tatáž osoba by v náhledu měla jiné datum než v živém rodokmenu.
/// Zkratky měsíců odpovídají <c>Intl.DateTimeFormat("cs", { month: "short" })</c>; psát je
/// natvrdo je spolehlivější než doufat, že WASM build má po ruce česká ICU data.
///
/// Surové datum se nikdy nepřepisuje — tohle je výhradně zobrazení.
/// </summary>
public static class GedcomDate
{
    /// <summary>Escape kalendáře („@#DJULIAN@ 3 MAR 1901") pro zobrazení nic neříká a zahodí se.</summary>
    private static readonly Regex CalendarEscape = new(@"@[^@]*@", RegexOptions.Compiled);

    private static readonly Regex YearPart = new(@"^\d{1,4}$", RegexOptions.Compiled);
    private static readonly Regex DayPart = new(@"^\d{1,2}$", RegexOptions.Compiled);

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1,
        ["feb"] = 2,
        ["mar"] = 3,
        ["apr"] = 4,
        ["may"] = 5,
        ["jun"] = 6,
        ["jul"] = 7,
        ["aug"] = 8,
        ["sep"] = 9,
        ["oct"] = 10,
        ["nov"] = 11,
        ["dec"] = 12,
    };

    /// <summary>Měsíc bez roku; „bře" je to, co dá <c>Intl.DateTimeFormat("cs", { month: "short" })</c>.</summary>
    private static readonly string[] CzechShortMonths =
        { "led", "úno", "bře", "dub", "kvě", "čvn", "čvc", "srp", "zář", "říj", "lis", "pro" };

    /// <summary>Měsíc s rokem; čeština tu má podle Intl plný název („březen 1901"), ne zkratku.</summary>
    private static readonly string[] CzechMonths =
    {
        "leden", "únor", "březen", "duben", "květen", "červen",
        "červenec", "srpen", "září", "říjen", "listopad", "prosinec",
    };

    /// <summary>České znění kvalifikátorů; stejná slova používá Topola pro locale „cs".</summary>
    private static readonly Dictionary<string, string> QualifierWords = new(StringComparer.Ordinal)
    {
        ["abt"] = "okolo",
        ["cal"] = "vypočt.",
        ["est"] = "odhadem",
        ["before"] = "před",
        ["after"] = "po",
    };

    /// <summary>
    /// Rozloží surové datum GEDCOM; <c>null</c> pro prázdné (třeba holé „BIRT Y" bez DATE).
    /// Nikdy nevyhodí výjimku — datum přichází ze souboru, tedy z jediného nedůvěryhodného
    /// vstupu aplikace (princip 8).
    /// </summary>
    public static GedcomDateValue? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = CalendarEscape.Replace(raw, " ").Trim();
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var keyword = parts[0].ToLowerInvariant();

        if (keyword.StartsWith("bet", StringComparison.Ordinal))
        {
            var and = Array.FindIndex(parts, p => string.Equals(p, "and", StringComparison.OrdinalIgnoreCase));
            if (and > 0)
            {
                return Range(ParseParts(parts[1..and]), ParseParts(parts[(and + 1)..]));
            }
        }
        else if (keyword == "from")
        {
            var to = Array.FindIndex(parts, p => string.Equals(p, "to", StringComparison.OrdinalIgnoreCase));
            return to > 0
                ? Range(ParseParts(parts[1..to]), ParseParts(parts[(to + 1)..]))
                : Range(ParseParts(parts[1..]), null);
        }
        else if (keyword == "to")
        {
            return Range(null, ParseParts(parts[1..]));
        }
        else if (keyword.StartsWith("bef", StringComparison.Ordinal))
        {
            return Range(null, ParseParts(parts[1..]));
        }
        else if (keyword.StartsWith("aft", StringComparison.Ordinal))
        {
            return Range(ParseParts(parts[1..]), null);
        }

        return new GedcomDateValue(ParseParts(parts) ?? AsText(text), null, null);
    }

    /// <summary>
    /// České znění data pro zobrazení; <c>null</c> když datum není. Nerozpoznaný text se vrátí,
    /// jak přišel — „kolem sv. Václava" je pořád lepší informace než prázdno.
    /// </summary>
    public static string? Format(string? raw)
    {
        var value = Parse(raw);
        if (value is null)
        {
            return null;
        }

        var text = FormatValue(value);
        return string.IsNullOrEmpty(text) ? raw!.Trim() : text;
    }

    private static string FormatValue(GedcomDateValue value)
    {
        if (value.Date is { } date)
        {
            return FormatParts(date);
        }

        var from = value.From is { } f ? FormatParts(f) : null;
        var to = value.To is { } t ? FormatParts(t) : null;

        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
        {
            return $"{from} .. {to}";
        }

        if (!string.IsNullOrEmpty(from))
        {
            return $"{QualifierWords["after"]} {from}";
        }

        if (!string.IsNullOrEmpty(to))
        {
            return $"{QualifierWords["before"]} {to}";
        }

        return string.Empty;
    }

    private static string FormatParts(GedcomDateParts parts)
    {
        if (parts.Text is { } text)
        {
            return text;
        }

        var date = FormatDayMonthYear(parts.Day, parts.Month, parts.Year);

        return parts.Qualifier is { } qualifier && QualifierWords.TryGetValue(qualifier, out var word)
            ? $"{word} {date}"
            : date;
    }

    /// <summary>
    /// Přesně to, co ze stejných částí vypíše <c>Intl.DateTimeFormat("cs", …)</c> uvnitř Topoly —
    /// a čeština se tu chová nečekaně: celé datum je číselné („3. 3. 1901"), měsíc s rokem má
    /// plný název („březen 1901") a samotný měsíc zkratku („bře").
    /// </summary>
    private static string FormatDayMonthYear(int? day, int? month, int? year)
    {
        if (day is { } d && month is { } m && year is { } y)
        {
            return $"{d}. {m}. {y}";
        }

        if (month is { } my && year is { } ym)
        {
            return $"{CzechMonths[my - 1]} {ym}";
        }

        if (day is { } dd && month is { } mm)
        {
            return $"{dd}. {mm}.";
        }

        if (year is { } onlyYear)
        {
            return onlyYear.ToString(CultureInfo.InvariantCulture);
        }

        if (day is { } onlyDay)
        {
            return $"{onlyDay}.";
        }

        if (month is { } onlyMonth)
        {
            return CzechShortMonths[onlyMonth - 1];
        }

        return string.Empty;
    }

    private static GedcomDateValue Range(GedcomDateParts? from, GedcomDateParts? to) => new(null, from, to);

    private static GedcomDateParts AsText(string text) => new(null, null, null, null, text);

    /// <summary>
    /// Přečte jednu hranici (nebo celé datum) odzadu: rok, měsíc, den. Zůstane-li cokoli
    /// nezpracovaného, je celá hranice text — rozebírat napůl rozpoznané datum by znamenalo
    /// ukázat něco, co v souboru nestojí.
    /// </summary>
    private static GedcomDateParts? ParseParts(string[] input)
    {
        if (input.Length == 0)
        {
            return null;
        }

        var parts = input.ToList();

        // Vysvětlující fráze je v GEDCOM v závorkách; Topola je taky odstraní a obsah zkusí přečíst.
        if (parts[0].StartsWith('(') && parts[^1].EndsWith(')'))
        {
            parts[0] = parts[0][1..];
            parts[^1] = parts[^1][..^1];
            parts.RemoveAll(string.IsNullOrEmpty);
            if (parts.Count == 0)
            {
                return null;
            }
        }

        var fullText = string.Join(" ", parts);

        string? qualifier = null;
        if (parts[0].ToLowerInvariant() is "cal" or "abt" or "est")
        {
            qualifier = parts[0].ToLowerInvariant();
            parts.RemoveAt(0);
        }

        int? year = null;
        if (parts.Count > 0 && YearPart.IsMatch(parts[^1]))
        {
            year = int.Parse(parts[^1], CultureInfo.InvariantCulture);
            parts.RemoveAt(parts.Count - 1);
        }

        int? month = null;
        if (parts.Count > 0 && Months.TryGetValue(parts[^1], out var m))
        {
            month = m;
            parts.RemoveAt(parts.Count - 1);
        }

        int? day = null;
        if (parts.Count > 0 && DayPart.IsMatch(parts[0]))
        {
            day = int.Parse(parts[0], CultureInfo.InvariantCulture);
            parts.RemoveAt(0);
        }

        if (parts.Count > 0 || (year is null && month is null && day is null))
        {
            return AsText(fullText);
        }

        return new GedcomDateParts(qualifier, day, month, year, null);
    }
}
