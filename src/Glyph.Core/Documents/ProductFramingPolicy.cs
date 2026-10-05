namespace Glyph.Core.Documents;

/// <summary>
/// Product framing — six integrated tools (FEATURES.md §64 / F64-00).
/// </summary>
public static class ProductFramingPolicy
{
    public static IReadOnlyList<string> IntegratedTools { get; } =
    [
        "PDF viewer",
        "PDF editor / markup",
        "Image viewer / editor",
        "OCR / Live Text",
        "Scanner / camera import",
        "Batch / print / export",
    ];

    public const string Charter =
        "Glyph is a native Windows document app combining PDF, image, OCR, and capture workflows with low friction.";

    public static bool CatalogContains(string tool) =>
        IntegratedTools.Any(t => t.Contains(tool, StringComparison.OrdinalIgnoreCase));
}
