namespace Glyph.Pdf.Abstractions;

public sealed record PdfLink(
    PdfRect Bounds,
    int? DestinationPageIndex,
    string? Uri);
