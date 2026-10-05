namespace Glyph.Ocr.Abstractions;

public sealed record OcrResult(string Text, IReadOnlyList<OcrLine> Lines, IReadOnlyList<OcrEntity> Entities);

public sealed record OcrLine(string Text, IReadOnlyList<OcrWord> Words);

public sealed record OcrWord(string Text, double X, double Y, double Width, double Height);

public enum OcrEntityKind
{
    Url,
    Email,
    Phone,
    Date,
}

public sealed record OcrEntity(OcrEntityKind Kind, string Value, int StartIndex, int Length);
