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
    // PDF Reference §7.6.3.2 user-access permission bits.
    private const uint PermPrint = 1u << 2;
    private const uint PermModify = 1u << 3;
    private const uint PermCopy = 1u << 4;
    private const uint PermAnnotate = 1u << 5;
    private const uint PermFillForms = 1u << 8;
    private const uint PermAssemble = 1u << 10;
    private const uint PermPrintHighQuality = 1u << 11;

    /// <summary>F23-09 — PDF permission flags are not a security boundary.</summary>
    public const string AdvisoryNotice =
        "Permissions (PDF flags — enforcement is advisory)";

    /// <summary>Status-line form of <see cref="AdvisoryNotice"/> when the document is encrypted.</summary>
    public const string EncryptedAdvisoryStatus =
        "Document is encrypted — permissions shown are advisory.";

    /// <summary>Compact status-bar marker when <c>IsEncrypted</c> (F23-08).</summary>
    public const string StatusBarEncryptedMarker = "    Encrypted";

    /// <summary>Sidebar properties suffix when encrypted (F23-08).</summary>
    public const string PropertiesEncryptedSuffix = " · Encrypted";

    public static string StatusBarEncryptedSuffix(bool isEncrypted) =>
        isEncrypted ? StatusBarEncryptedMarker : string.Empty;

    public static string PropertiesEncryptedMarker(bool isEncrypted) =>
        isEncrypted ? PropertiesEncryptedSuffix : string.Empty;

    public static string InfoEncryptedLine(bool isEncrypted) =>
        $"Encrypted: {(isEncrypted ? "yes" : "no")}";

    /// <summary>Decode standard security handler <c>/P</c> flags.</summary>
    public static PdfDocumentPermissions FromFlags(uint flags) =>
        new(
            CanPrint: (flags & PermPrint) != 0,
            CanModify: (flags & PermModify) != 0,
            CanCopy: (flags & PermCopy) != 0,
            CanAnnotate: (flags & PermAnnotate) != 0,
            CanFillForms: (flags & PermFillForms) != 0,
            CanAssemble: (flags & PermAssemble) != 0,
            CanPrintHighQuality: (flags & PermPrintHighQuality) != 0);

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
