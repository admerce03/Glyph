namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Word box in image pixel space with origin at the top-left (same as Windows.Media.Ocr).
/// </summary>
public sealed record SearchablePdfWord(
    string Text,
    double X,
    double Y,
    double Width,
    double Height);
