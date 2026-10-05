namespace Glyph.Pdf.Abstractions;

public sealed record PdfFormFieldInfo(
    int PageIndex,
    int AnnotIndex,
    string Name,
    PdfFormFieldKind Kind,
    string Value,
    PdfRect Bounds,
    int TabOrder,
    IReadOnlyList<string>? Options = null,
    string? DefaultAppearance = null,
    PdfFormButtonAction? ButtonAction = null)
{
    public IReadOnlyList<string> ChoiceOptions => Options ?? Array.Empty<string>();

    /// <summary>
    /// True when the widget <c>/DA</c> uses font size 0 (automatic sizing).
    /// </summary>
    public bool UsesAutoFontSize => PdfFormDefaultAppearance.UsesAutoFontSize(DefaultAppearance);
}
