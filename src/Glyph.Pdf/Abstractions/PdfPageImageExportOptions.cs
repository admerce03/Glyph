namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Options for rasterizing a PDF page to an image file (F45 / F62 page Export).
/// </summary>
public sealed record PdfPageImageExportOptions(
    string Extension = ".png",
    double Dpi = 144,
    int? Quality = null,
    string? Title = null,
    string? Author = null,
    bool EmbedSrgbProfile = true)
{
    public double Scale => Dpi / 72.0;
}
