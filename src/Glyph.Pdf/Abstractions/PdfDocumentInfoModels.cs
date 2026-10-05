namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Document information dictionary + security summary (read-only).
/// </summary>
public sealed record PdfDocumentInfo(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    string? CreationDate,
    string? ModificationDate,
    int PageCount,
    string? FilePath,
    long? FileSizeBytes,
    bool IsEncrypted,
    int SecurityHandlerRevision,
    uint PermissionFlags,
    PdfDocumentPermissions Permissions);

/// <summary>
/// Decoded PDF user-access permission bits (PDF Reference §7.6.3.2).
/// Enforcement is advisory for many viewers — Glyph surfaces them honestly.
/// </summary>
public sealed record PdfDocumentPermissions(
    bool CanPrint,
    bool CanModify,
    bool CanCopy,
    bool CanAnnotate,
    bool CanFillForms,
    bool CanAssemble,
    bool CanPrintHighQuality);
