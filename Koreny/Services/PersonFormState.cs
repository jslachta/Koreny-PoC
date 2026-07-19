using Koreny.Models;

namespace Koreny.Services;

/// <summary>
/// Stav editačního formuláře osoby, oddělený od komponenty kvůli testovatelnosti
/// (idempotence uložení, viz docs/session-2b-vysledky.md).
///
/// Formulář drží data jako SUROVÉ řetězce — datum se nikdy neparsuje na rok ani DateTime,
/// zobrazení == editace == uložení téhož řetězce. Co formulář needituje (formát původního
/// jména, nestandardní pohlaví, druhé a další jméno/poznámky, existence prázdné události
/// „BIRT Y“), se přes tento stav přenese beze změny, takže <see cref="Fill"/> následované
/// <see cref="ApplyTo"/> je sémantická identita.
/// </summary>
public sealed class PersonFormState
{
    // Editovatelná pole (bindovaná v UI).
    public string Given { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public int SexCode { get; set; } // 0 = neznámé, 1 = M, 2 = F
    public string BirthDate { get; set; } = string.Empty;
    public string BirthPlace { get; set; } = string.Empty;
    public string DeathDate { get; set; } = string.Empty;
    public string DeathPlace { get; set; } = string.Empty;
    public string NoteFirst { get; set; } = string.Empty;

    // Zachovávaná, needitovaná data (mimo UI).
    private bool _hadName;
    private string _nameRaw = string.Empty;
    private string _origGiven = string.Empty;
    private string _origSurname = string.Empty;
    private string? _origSex;
    private int _origSexCode;
    private bool _birthPresent;
    private bool _deathPresent;
    private readonly List<string> _extraNotes = new(); // NOTE[1..], nese se beze změny

    public void Fill(GedcomIndividual ind)
    {
        _hadName = ind.Name is not null;
        _nameRaw = ind.Name?.Raw ?? string.Empty;
        Given = ind.Name?.GivenName ?? string.Empty;
        Surname = ind.Name?.Surname ?? string.Empty;
        _origGiven = Given;
        _origSurname = Surname;

        _origSex = ind.Sex;
        SexCode = ind.Sex switch { "M" => 1, "F" => 2, _ => 0 };
        _origSexCode = SexCode;

        _birthPresent = ind.Birth is not null;
        BirthDate = ind.Birth?.Date ?? string.Empty;
        BirthPlace = ind.Birth?.Place ?? string.Empty;

        _deathPresent = ind.Death is not null;
        DeathDate = ind.Death?.Date ?? string.Empty;
        DeathPlace = ind.Death?.Place ?? string.Empty;

        _extraNotes.Clear();
        for (var i = 1; i < ind.Notes.Count; i++)
        {
            _extraNotes.Add(ind.Notes[i]);
        }

        NoteFirst = ind.Notes.Count > 0 ? ind.Notes[0] : string.Empty;
    }

    public void ApplyTo(GedcomIndividual ind)
    {
        ApplyName(ind);
        ApplySex(ind);
        ind.Birth = GedcomFormValues.BuildEvent(_birthPresent, BirthDate, BirthPlace);
        ind.Death = GedcomFormValues.BuildEvent(_deathPresent, DeathDate, DeathPlace);
        ApplyNotes(ind);
    }

    private void ApplyName(GedcomIndividual ind)
    {
        var g = Given.Trim();
        var s = Surname.Trim();

        // Jméno beze změny → zachovej původní surový řetězec (formát „Jan /Novák/“ i „/Novák/ Jan“).
        if (_hadName && g == _origGiven.Trim() && s == _origSurname.Trim())
        {
            ind.Name = new GedcomName
            {
                Raw = _nameRaw,
                GivenName = GedcomFormValues.NullIfEmpty(g),
                Surname = GedcomFormValues.NullIfEmpty(s),
            };
            return;
        }

        if (!_hadName && g.Length == 0 && s.Length == 0)
        {
            ind.Name = null;
            return;
        }

        // Skutečná změna jména: rekonstruuj v konvenci UI (příjmení ve lomítkách).
        ind.Name = new GedcomName
        {
            GivenName = GedcomFormValues.NullIfEmpty(g),
            Surname = GedcomFormValues.NullIfEmpty(s),
            Raw = $"/{s}/ {g}".Trim(),
        };
    }

    private void ApplySex(GedcomIndividual ind)
    {
        // Beze změny → zachovej původní řetězec (i nestandardní hodnoty jako „U“).
        if (SexCode == _origSexCode)
        {
            ind.Sex = _origSex;
            return;
        }

        ind.Sex = SexCode switch { 1 => "M", 2 => "F", _ => null };
    }

    private void ApplyNotes(GedcomIndividual ind)
    {
        ind.Notes.Clear();
        if (NoteFirst.Length > 0)
        {
            ind.Notes.Add(NoteFirst);
        }

        ind.Notes.AddRange(_extraNotes);
    }
}
