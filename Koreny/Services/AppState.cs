using Koreny.Models;

namespace Koreny.Services;

/// <summary>Singleton document for the single-page editor; not persisted.</summary>
public class AppState
{
    public GedcomDocument? Document { get; set; }

    /// <summary>
    /// Jak kreslit rodokmen. Drží se v paměti stejně jako dokument — s obnovením stránky
    /// se vrátí výchozí hodnoty. Perzistovat ji by znamenalo založit druhý, skrytý stav
    /// v prohlížeči; to si aplikace zakazuje i u samotných dat (princip 4).
    /// </summary>
    public TreeViewSettings TreeView { get; set; } = new();
}
