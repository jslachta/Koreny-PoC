using System.Text.RegularExpressions;

namespace Koreny.Models;

/// <summary>Root document produced from a GEDCOM 5.5.1 parse.</summary>
public class GedcomDocument
{
    public List<GedcomIndividual> Individuals { get; } = new();
    public List<GedcomFamily> Families { get; } = new();

    /// <summary>
    /// Všechny záznamy levelu 0 v původním pořadí (HEAD, INDI, FAM, SOUR, NOTE, TRLR,
    /// neznámé…). Toto je nosič pravdy pro export; <see cref="Individuals"/> a
    /// <see cref="Families"/> jsou jen projekce vybraných uzlů pro UI a renderery.
    /// Prázdné u dokumentu vytvořeného od nuly v UI — writer pak hlavičku i TRLR syntetizuje.
    /// </summary>
    public List<GedcomNode> Nodes { get; } = new();
}

public class GedcomIndividual
{
    public string Id { get; set; } = string.Empty;
    public GedcomName? Name { get; set; }
    public string? Sex { get; set; }
    public GedcomEvent? Birth { get; set; }
    public GedcomEvent? Death { get; set; }
    public List<string> Notes { get; } = new();

    /// <summary>Uzel INDI ve stromu, z něhož je tato osoba projekcí. Editace se přes něj promítají do exportu.</summary>
    public GedcomNode? SourceNode { get; set; }

    /// <summary>Display name: given + surname when parsed; otherwise raw NAME line.</summary>
    public string FullName
    {
        get
        {
            if (Name is null)
            {
                return string.Empty;
            }

            var given = Name.GivenName?.Trim();
            var surname = Name.Surname?.Trim();
            if (!string.IsNullOrEmpty(given) && !string.IsNullOrEmpty(surname))
            {
                return $"{given} {surname}";
            }

            if (!string.IsNullOrEmpty(given))
            {
                return given;
            }

            if (!string.IsNullOrEmpty(surname))
            {
                return surname;
            }

            return Name.Raw.Trim();
        }
    }
}

public class GedcomName
{
    public string Raw { get; set; } = string.Empty;
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
}

public class GedcomEvent
{
    private static readonly Regex YearRegex = new(@"\b(1[0-9]{3}|2[0-9]{3})\b", RegexOptions.Compiled);

    public string? Date { get; set; }
    public string? Place { get; set; }

    /// <summary>First 4-digit year found in <see cref="Date"/> (GEDCOM allows many formats).</summary>
    public int? ParsedYear
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Date))
            {
                return null;
            }

            var m = YearRegex.Match(Date!);
            return m.Success ? int.Parse(m.Value) : null;
        }
    }
}

public class GedcomFamily
{
    public string Id { get; set; } = string.Empty;
    public string? HusbandId { get; set; }
    public string? WifeId { get; set; }
    public List<string> ChildrenIds { get; } = new();
    public GedcomEvent? Marriage { get; set; }
    public List<string> Notes { get; } = new();

    /// <summary>Uzel FAM ve stromu, z něhož je tato rodina projekcí. Editace se přes něj promítají do exportu.</summary>
    public GedcomNode? SourceNode { get; set; }
}
