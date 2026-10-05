namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Editable document Info fields (F25-16–19). Null means leave unchanged; empty clears.
/// </summary>
public sealed record PdfDocumentInfoUpdate(
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Keywords = null,
    bool ClearAll = false);
