namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Action associated with an AcroForm push button (<c>/A</c>).
/// </summary>
public enum PdfFormButtonActionKind
{
    None = 0,
    Uri = 1,
    GoTo = 2,
    Other = 3,
}

/// <summary>
/// Resolved push-button action for activation in the UI.
/// </summary>
public sealed record PdfFormButtonAction(
    PdfFormButtonActionKind Kind,
    string? Uri = null,
    int? DestPageIndex = null,
    string? Caption = null);
