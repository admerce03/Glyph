namespace Glyph.Pdf.Abstractions;

public enum PdfRedactionKind
{
    Rectangle,
    Text,
}

/// <summary>
/// A pending (not yet applied) redaction mark in PDF user space.
/// </summary>
public sealed record PdfPendingRedaction(
    Guid Id,
    int PageIndex,
    PdfRedactionKind Kind,
    PdfRect Bounds,
    string? Label = null);

public sealed record PdfRedactionApplyOptions(
    bool RemoveIntersectingTextObjects = true,
    bool RemoveIntersectingImageObjects = false);

public sealed record PdfRedactionApplyResult(
    int MarksApplied,
    int PagesChanged,
    int TextObjectsRemoved,
    int ImageObjectsRemoved);
