namespace Glyph.Pdf.Abstractions;

public sealed record PdfFormFieldInfo(
    int PageIndex,
    int AnnotIndex,
    string Name,
    PdfFormFieldKind Kind,
    string Value,
    PdfRect Bounds,
    int TabOrder,
    IReadOnlyList<string>? Options = null)
{
    public IReadOnlyList<string> ChoiceOptions => Options ?? Array.Empty<string>();
}
