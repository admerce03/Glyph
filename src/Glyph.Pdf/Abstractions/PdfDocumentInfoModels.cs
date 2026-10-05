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
    string? PdfVersion,
    double? PageWidthPoints,
    double? PageHeightPoints,
    IReadOnlyList<string> Fonts,
    int EmbeddedAttachmentCount,
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
    bool CanPrintHighQuality)
{
    /// <summary>F23-09 — PDF permission flags are not a security boundary.</summary>
    public const string AdvisoryNotice =
        "Permissions (PDF flags — enforcement is advisory)";

    /// <summary>Status-line form of <see cref="AdvisoryNotice"/> when the document is encrypted.</summary>
    public const string EncryptedAdvisoryStatus =
        "Document is encrypted — permissions shown are advisory.";

    public string FormatLines() =>
        $"Print: {(CanPrint ? "yes" : "no")}\n"
        + $"Modify: {(CanModify ? "yes" : "no")}\n"
        + $"Copy: {(CanCopy ? "yes" : "no")}\n"
        + $"Annotate: {(CanAnnotate ? "yes" : "no")}\n"
        + $"Fill forms: {(CanFillForms ? "yes" : "no")}\n"
        + $"Assemble: {(CanAssemble ? "yes" : "no")}\n"
        + $"High-quality print: {(CanPrintHighQuality ? "yes" : "no")}";

    public string FormatSection() => AdvisoryNotice + ":\n" + FormatLines();
}
