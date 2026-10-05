namespace Glyph.Ocr.Abstractions;

public sealed record OcrRequest(
    int PixelWidth,
    int PixelHeight,
    byte[] BgraPixels,
    string? LanguageTag = null,
    IProgress<OcrProgress>? Progress = null);

public sealed record OcrProgress(double Fraction, string Status);
