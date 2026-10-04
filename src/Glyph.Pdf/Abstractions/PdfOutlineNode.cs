namespace Glyph.Pdf.Abstractions;

public sealed record PdfOutlineNode(
    string Title,
    int? DestinationPageIndex,
    IReadOnlyList<PdfOutlineNode> Children);
