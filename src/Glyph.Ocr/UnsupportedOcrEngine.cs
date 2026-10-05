using Glyph.Ocr.Abstractions;

namespace Glyph.Ocr;

/// <summary>
/// Placeholder engine used on non-Windows targets and in tests.
/// </summary>
public sealed class UnsupportedOcrEngine : IOcrEngine
{
    public Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new PlatformNotSupportedException(
            "OCR requires Windows.Media.Ocr (or a Tesseract adapter).");
    }
}
