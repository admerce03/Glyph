namespace Glyph.Core.Printing;

/// <summary>
/// Features delegated to the Windows PrintTask system UI (F44-06…12 / F44-23).
/// </summary>
public static class PrintSystemCapabilities
{
    public static IReadOnlyList<string> SystemUiOptions { get; } =
    [
        "Copies",
        "Collate",
        "Duplex",
        "Printer selection",
        "Paper size",
        "Orientation",
        "Printer properties",
    ];

    public const bool UsesImageableRectMargins = true;
    public const bool AnnotationsIncludedInPageRender = true;

    public static bool CatalogContains(string option) =>
        SystemUiOptions.Any(o => string.Equals(o, option, StringComparison.OrdinalIgnoreCase));
}
