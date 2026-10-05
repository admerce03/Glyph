namespace Glyph.Core.Text;

/// <summary>
/// Display placeholders for optional document/info fields in status and sidebars.
/// </summary>
public static class DisplayValue
{
    public const string EmDash = "—";

    public static string OrEmDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? EmDash : value;
}
