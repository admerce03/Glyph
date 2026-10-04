namespace Glyph.Ocr.Abstractions;

public sealed record OcrResult(string Text, IReadOnlyList<OcrLine> Lines);

public sealed record OcrLine(string Text, IReadOnlyList<OcrWord> Words);

public sealed record OcrWord(string Text, double X, double Y, double Width, double Height);
