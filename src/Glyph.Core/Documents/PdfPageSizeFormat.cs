namespace Glyph.Core.Documents;

/// <summary>
/// Formats PDF page dimensions (points) for info dialogs and properties sidebar.
/// </summary>
public static class PdfPageSizeFormat
{
    public static string FormatPoints(
        double? widthPoints,
        double? heightPoints,
        string separator = " × ",
        string nullLabel = "—")
    {
        if (widthPoints is null || heightPoints is null)
        {
            return nullLabel;
        }

        return $"{widthPoints:0.#}{separator}{heightPoints:0.#} pt";
    }
}
