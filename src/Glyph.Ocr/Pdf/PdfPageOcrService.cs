using Glyph.Ocr.Abstractions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Ocr.Pdf;

/// <summary>
/// Renders a PDF page and runs offline OCR on the bitmap.
/// </summary>
public sealed class PdfPageOcrService
{
    private readonly IPdfRenderer _renderer;
    private readonly IOcrEngine _ocr;

    public PdfPageOcrService(IPdfRenderer renderer, IOcrEngine ocr)
    {
        _renderer = renderer;
        _ocr = ocr;
    }

    public async Task<OcrResult> RecognizePageAsync(
        IPdfDocument document,
        int pageIndex,
        double scale = 2.0,
        string? languageTag = null,
        IProgress<OcrProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new OcrProgress(0.02, "Rendering page…"));
        var rendered = await _renderer.RenderPageAsync(
            document,
            pageIndex,
            new PdfRenderRequest(scale),
            cancellationToken);
        var pixels = rendered.Pixels.ToArray();
        return await _ocr.RecognizeAsync(
            new OcrRequest(rendered.Width, rendered.Height, pixels, languageTag, progress),
            cancellationToken);
    }
}
