namespace Glyph.Pdf.Abstractions;

/// <summary>
/// List and fill AcroForm fields (text first; tab order navigation).
/// </summary>
public interface IPdfFormStore
{
    /// <summary>
    /// Returns whether the document has an AcroForm (or XFA) dictionary.
    /// </summary>
    Task<bool> HasFormAsync(IPdfDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// List widget fields in document tab order (page order, then annotation z-order).
    /// </summary>
    Task<IReadOnlyList<PdfFormFieldInfo>> ListFieldsAsync(
        IPdfDocument document,
        CancellationToken cancellationToken = default);

    Task SetTextValueAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle a checkbox widget. Sets <c>/V</c> and <c>/AS</c> to the on-state name or <c>Off</c>.
    /// </summary>
    Task SetCheckBoxAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        bool isChecked,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Select a radio button widget. Sets its <c>/V</c> and <c>/AS</c> to the on-state name,
    /// and turns off sibling radios that share the same field name.
    /// </summary>
    Task SetRadioButtonAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Focus the next (or previous) field in tab order. Returns the focused field, or null if none.
    /// </summary>
    Task<PdfFormFieldInfo?> FocusAdjacentAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        bool forward,
        CancellationToken cancellationToken = default);
}
