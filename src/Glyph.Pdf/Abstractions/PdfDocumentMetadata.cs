namespace Glyph.Pdf.Abstractions;

public sealed record PdfDocumentMetadata(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    string? CreationDate,
    string? ModificationDate,
    int PageCount,
    string? PdfVersion,
    bool IsEncrypted,
    ulong Permissions,
    long? FileSizeBytes = null,
    double? FirstPageWidthPoints = null,
    double? FirstPageHeightPoints = null);

public interface IPdfMetadataService
{
    Task<PdfDocumentMetadata> GetAsync(IPdfDocument document, CancellationToken cancellationToken = default);

    Task SetAsync(
        IPdfDocument document,
        string? title,
        string? author,
        string? subject,
        string? keywords,
        CancellationToken cancellationToken = default);
}

public interface IPdfSecurityInfoService
{
    Task<PdfSecurityInfo> GetAsync(IPdfDocument document, CancellationToken cancellationToken = default);
}

public sealed record PdfSecurityInfo(
    bool IsEncrypted,
    ulong Permissions,
    int SecurityHandlerRevision,
    bool CanPrint,
    bool CanModify,
    bool CanCopy,
    bool CanAnnotate)
{
    public const string PermissionEnforcementWarning =
        "PDF permission flags are advisory. Many readers ignore them once a document is open; " +
        "do not rely on permissions alone for confidentiality.";
}

public interface IPdfSecurityService
{
    Task<PdfSecurityInfo> GetInfoAsync(IPdfDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a password-protected copy of the document using the PDF Standard Security Handler (RC4).
    /// </summary>
    Task ProtectAsync(
        IPdfDocument document,
        string outputPath,
        string userPassword,
        string? ownerPassword = null,
        PdfPermissionFlags deny = PdfPermissionFlags.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an unencrypted copy. The document must already be open (password supplied at open if needed).
    /// </summary>
    Task RemoveProtectionAsync(
        IPdfDocument document,
        string outputPath,
        CancellationToken cancellationToken = default);
}

[Flags]
public enum PdfPermissionFlags
{
    None = 0,
    DenyPrint = 1 << 2,
    DenyModify = 1 << 3,
    DenyCopy = 1 << 4,
    DenyAnnotate = 1 << 5,
    DenyExtract = 1 << 4,
    DenyAll = DenyPrint | DenyModify | DenyCopy | DenyAnnotate,
}

public interface IPdfRedactionService
{
    Task<IReadOnlyList<PdfRedactionMark>> ListAsync(IPdfDocument document, CancellationToken cancellationToken = default);

    Task<PdfRedactionMark> MarkRectAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default);

    Task<PdfRedactionMark> MarkTextAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(IPdfDocument document, string markId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently applies pending redactionsctions: removes intersecting text/image page objects,
    /// paints opaque black content, and deletes the pending marks.
    /// </summary>
    Task ApplyAsync(IPdfDocument document, CancellationToken cancellationToken = default);
}

public sealed record PdfRedactionMark(string Id, int PageIndex, PdfRect Bounds);

public enum PdfOptimizationPreset
{
    Lossless,
    HighQuality,
    Balanced,
    SmallFile,
    Custom,
}

public sealed record PdfOptimizationOptions(
    PdfOptimizationPreset Preset,
    bool RemoveMetadata = false,
    int? TargetDpi = null,
    int JpegQuality = 75);

public sealed record PdfOptimizationEstimate(long SourceBytes, long EstimatedBytes, string Detail);

public interface IPdfOptimizationService
{
    Task<PdfOptimizationEstimate> EstimateAsync(
        IPdfDocument document,
        PdfOptimizationOptions options,
        CancellationToken cancellationToken = default);

    Task OptimizeAsync(
        IPdfDocument document,
        PdfOptimizationOptions options,
        string outputPath,
        CancellationToken cancellationToken = default);
}
