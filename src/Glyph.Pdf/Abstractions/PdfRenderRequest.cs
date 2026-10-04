namespace Glyph.Pdf.Abstractions;

public sealed record PdfRenderRequest(
    double Scale,
    int? MaxWidthPixels = null,
    int? MaxHeightPixels = null);
