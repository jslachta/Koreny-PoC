using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Stav editačního formuláře rodiny, oddělený od komponenty kvůli testovatelnosti
/// (idempotence uložení, viz docs/session-2b-vysledky.md). Datum sňatku je surový
/// řetězec; prázdná událost „MARR“ (bez data i místa) se zachová.
/// </summary>
public sealed class FamilyFormState
{
    public string HusbandId { get; set; } = string.Empty;
    public string WifeId { get; set; } = string.Empty;
    public string MarriageDate { get; set; } = string.Empty;
    public string MarriagePlace { get; set; } = string.Empty;
    public List<string> ChildIds { get; set; } = new();

    private bool _marriagePresent;

    public void Fill(GedcomFamily fam)
    {
        HusbandId = fam.HusbandId ?? string.Empty;
        WifeId = fam.WifeId ?? string.Empty;
        _marriagePresent = fam.Marriage is not null;
        MarriageDate = fam.Marriage?.Date ?? string.Empty;
        MarriagePlace = fam.Marriage?.Place ?? string.Empty;
        ChildIds = new List<string>(fam.ChildrenIds);
    }

    public void ApplyTo(GedcomFamily fam)
    {
        fam.HusbandId = GedcomFormValues.NullIfEmpty(HusbandId);
        fam.WifeId = GedcomFormValues.NullIfEmpty(WifeId);
        fam.ChildrenIds.Clear();
        fam.ChildrenIds.AddRange(ChildIds);
        fam.Marriage = GedcomFormValues.BuildEvent(_marriagePresent, MarriageDate, MarriagePlace);
    }
}
