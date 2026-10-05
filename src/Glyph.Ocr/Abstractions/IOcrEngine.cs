namespace Glyph.Ocr.Abstractions;

public interface IOcrEngine
{
    string EngineName { get; }

    bool IsAvailable { get; }

    Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default);
}
