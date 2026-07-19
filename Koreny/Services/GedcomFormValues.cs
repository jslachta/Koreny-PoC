using Koreny.Models;

namespace Koreny.Services;

/// <summary>Sdílené převody formulářových řetězců na doménové hodnoty.</summary>
internal static class GedcomFormValues
{
    public static string? NullIfEmpty(string? s) =>
        string.IsNullOrEmpty(s) ? null : s;

    /// <summary>
    /// Sestaví událost z formulářových polí. Událost existuje, pokud existovala už dřív
    /// (i prázdná „BIRT Y“) NEBO uživatel vyplnil datum/místo. Prázdná existující událost
    /// se tak zachová (writer/sync jí ponechá původní hodnotu), datum se ukládá jako
    /// surový řetězec bez parsování.
    /// </summary>
    public static GedcomEvent? BuildEvent(bool present, string date, string place)
    {
        var d = date.Trim();
        var p = place.Trim();
        if (!present && d.Length == 0 && p.Length == 0)
        {
            return null;
        }

        return new GedcomEvent
        {
            Date = NullIfEmpty(d),
            Place = NullIfEmpty(p),
        };
    }
}
