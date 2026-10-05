namespace Glyph.Pdf.Abstractions;

/// <summary>
/// In-place PDF size / image optimization (PDFium-backed).
/// </summary>
public interface IPdfOptimizeService
{
    /// <summary>
    /// Estimate output size and how many images would be touched without mutating the document.
    /// </summary>
    PdfOptimizeEstimate Estimate(IPdfDocument document, PdfOptimizeOptions? options = null);

    /// <summary>
    /// Apply optimization in memory. Caller is responsible for saving.
    /// </summary>
    Task<PdfOptimizeResult> OptimizeAsync(
        IPdfDocument document,
        PdfOptimizeOptions? options = null,
        CancellationToken cancellationToken = default);
}
