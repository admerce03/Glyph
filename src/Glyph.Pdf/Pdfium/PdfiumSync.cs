namespace Glyph.Pdf.Pdfium;

/// <summary>
/// PDFium document operations are serialized. A single process-wide lock keeps the
/// initial adapter correct; finer-grained locking can come later if profiling demands it.
/// </summary>
internal static class PdfiumSync
{
    public static readonly object Gate = new();
}
