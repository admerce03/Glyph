namespace Glyph.Ocr.Abstractions;

public enum OcrEntityKind
{
    Url,
    Email,
    Phone,
    Address,
    Date,
    Time,
}

public sealed record OcrEntity(OcrEntityKind Kind, string Value, int StartIndex, int Length);
