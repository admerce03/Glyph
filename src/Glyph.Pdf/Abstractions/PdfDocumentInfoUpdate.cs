namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Editable document Info fields (F25-16–19 plus Creator/Producer).
/// Null means leave unchanged; empty clears.
/// </summary>
public sealed record PdfDocumentInfoUpdate(
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Keywords = null,
    string? Creator = null,
    string? Producer = null,
    bool ClearAll = false);
