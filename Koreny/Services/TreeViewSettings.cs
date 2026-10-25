using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koreny.Services;

/// <summary>
/// Jak se má kreslit rodokmen v overlayi, při tisku i při stažení.
///
/// Výchozí je <see cref="TreeChartType.Hourglass"/>, ne původní <see cref="TreeChartType.Relatives"/>:
/// ten skládá každého předka i s jeho vlastním potomkovským podstromem a bloky staví nad sebe,
/// takže u reálného rodokmenu vyjde strom vysoký na několik obrazovek. Kdo chce i rodiny
/// sourozenců předků, přepne si to v dialogu — ale platit za ně má ten, kdo je chce vidět.
///
/// Volby se dělí na dvě skupiny, které jdou do Topoly jinou cestou:
/// - graf, směr a barvy jsou obyčejné parametry <c>topola.createChart</c> (odsud přes
///   <see cref="ToJson"/> do js/topola-view.js);
/// - co stojí na kartách (<see cref="ShowOccupation"/> a místa) se řeší v datech:
///   <see cref="TopolaDataMapper"/> vypnutý údaj do datasetu vůbec nedá, a Topola kreslí,
///   co dostane. Filtrovat až v rendereru by znamenalo přepsat <c>getIndiDetails</c>
///   celý — takhle se její vlastní vykreslování míst nechává být.
///
/// Ani jedna cesta nesahá do vendorovaného bundlu (princip 2), takže upgrade Topoly nemá
/// co rozbít. Co Topola nenabízí, tu proto není: rozestupy (<c>V_SPACING</c>, <c>H_SPACING</c>)
/// jsou konstanty modulu a žádná optimalizace rozložení v knihovně neexistuje.
/// </summary>
public sealed class TreeViewSettings
{
    public TreeChartType ChartType { get; set; } = TreeChartType.Hourglass;

    public TreeOrientation Orientation { get; set; } = TreeOrientation.Vertical;

    public TreeColoring Coloring { get; set; } = TreeColoring.BySex;

    /// <summary>
    /// Povolání na kartě osoby. Výchozí vypnuté: Topola si velikost karty počítá z počtu
    /// řádků, takže každý údaj navíc zvětší kartu a s ní celý strom — platit za to má ten,
    /// kdo ho chce vidět. Topola povolání sama nezná, kreslí ho patch v js/topola-view.js.
    /// </summary>
    public bool ShowOccupation { get; set; }

    /// <summary>Místo narození na kartě osoby; vypnuté ze stejného důvodu jako povolání.</summary>
    public bool ShowBirthPlace { get; set; }

    /// <summary>Místo úmrtí na kartě osoby.</summary>
    public bool ShowDeathPlace { get; set; }

    /// <summary>Místo sňatku — stojí na uzlu rodiny vedle data sňatku, ne na kartě osoby.</summary>
    public bool ShowMarriagePlace { get; set; }

    /// <summary>Pracovní kopie pro dialog — zrušení pak nemá co vracet zpátky.</summary>
    public TreeViewSettings Copy() => new()
    {
        ChartType = ChartType,
        Orientation = Orientation,
        Coloring = Coloring,
        ShowOccupation = ShowOccupation,
        ShowBirthPlace = ShowBirthPlace,
        ShowDeathPlace = ShowDeathPlace,
        ShowMarriagePlace = ShowMarriagePlace,
    };

    /// <summary>
    /// Volby pro js/topola-view.js. Stejná konvence jako <see cref="TopolaDataMapper"/>: řetězce, ne čísla.
    /// Údaje na kartách tu nejsou — ty jdou do Topoly datasetem (viz dokumentace třídy).
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(new TreeViewJson
    {
        ChartType = ChartType switch
        {
            TreeChartType.Hourglass => "hourglass",
            TreeChartType.Ancestors => "ancestors",
            TreeChartType.Descendants => "descendants",
            _ => "relatives",
        },
        Horizontal = Orientation == TreeOrientation.Horizontal,
        Colors = Coloring switch
        {
            TreeColoring.ByGeneration => "byGeneration",
            TreeColoring.None => "none",
            _ => "bySex",
        },
    });
}

/// <summary>
/// Který z grafů Topoly se kreslí. Rozdíl není kosmetický: <see cref="Relatives"/> skládá každého
/// předka i s jeho vlastním potomkovským podstromem a bloky staví nad sebe, takže strom roste
/// do výšky; ostatní tři pouštějí předky přes společný layout s generačními patry.
/// </summary>
public enum TreeChartType
{
    /// <summary>Příbuzní (<c>RelativesChart</c>) — nejvíc dat, ale roste do výšky.</summary>
    Relatives,

    /// <summary>Přesýpací hodiny (<c>HourglassChart</c>) — přímá linie předků + potomci; výchozí.</summary>
    Hourglass,

    /// <summary>Jen předci (<c>AncestorChart</c>).</summary>
    Ancestors,

    /// <summary>Jen potomci (<c>DescendantChart</c>).</summary>
    Descendants,
}

public enum TreeOrientation
{
    /// <summary>Generace shora dolů; výchozí.</summary>
    Vertical,

    /// <summary>Generace zleva doprava; jen prohození os, rozložení zůstává stejné.</summary>
    Horizontal,
}

public enum TreeColoring
{
    /// <summary>Modrá/růžová podle pohlaví; výchozí.</summary>
    BySex,

    /// <summary>Odstín podle generace.</summary>
    ByGeneration,

    /// <summary>Bez výplně — čitelné i na černobílé tiskárně.</summary>
    None,
}

/// <summary>Tvar, ve kterém volby přebírá <c>js/topola-view.js</c>.</summary>
internal sealed class TreeViewJson
{
    [JsonPropertyName("chartType")]
    public required string ChartType { get; init; }

    [JsonPropertyName("horizontal")]
    public required bool Horizontal { get; init; }

    [JsonPropertyName("colors")]
    public required string Colors { get; init; }
}
