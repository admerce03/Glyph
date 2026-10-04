namespace Glyph.Ocr.Abstractions;

public sealed record OcrRequest(
    int PixelWidth,
    int PixelHeight,
    byte[] BgraPixels,
    string? LanguageTag = null);
