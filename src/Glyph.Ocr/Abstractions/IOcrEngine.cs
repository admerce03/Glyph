namespace Glyph.Ocr.Abstractions;

public interface IOcrEngine
{
    Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default);
}
