namespace Koreny.Services;

/// <summary>
/// Drobnosti české gramatiky pro UI. Vzniklo vydělením z Index.razor při jeho rozřezání —
/// pluralizaci potřebuje víc komponent a mít ji v každé zvlášť by znamenalo, že se jednou
/// rozejdou.
/// </summary>
public static class Czech
{
    /// <summary>Česká číslovka: 1 osoba, 2–4 osoby, 0 a 5+ osob.</summary>
    public static string Plural(int count, string one, string few, string many) =>
        count == 1 ? one : (count >= 2 && count <= 4 ? few : many);
}
