namespace Glyph.Core.Documents;

/// <summary>
/// Supported save/export output format families (F62).
/// </summary>
public static class OutputFormatSupport
{
    public static IReadOnlyList<string> PdfOutputPaths { get; } =
    [
        "Save",
        "Extract pages",
        "OCR → PDF",
        "Cropped export",
    ];

    public static bool SupportsPdfOutput => true;
}
