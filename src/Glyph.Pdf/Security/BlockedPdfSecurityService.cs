using Glyph.Core.Pdf;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Security;

/// <summary>
/// Explicit blocked write-encrypt stub (kept for regression tests). Production DI uses
/// <see cref="PdfSharpSecurityService"/> after ADR-015 Accept A.
/// </summary>
public sealed class BlockedPdfSecurityService : IPdfSecurityService
{
    public bool WriteProtectSupported => false;

    public string UnavailableReason =>
        "PDFium has no write-encrypt / permission-write API; needs ADR-015 approval for an alternate path.";

    public string BlockingAdr => PdfPasswordWriteBlockedPolicy.Adr;

    public Task<PdfSecurityWriteResult> SetOpenPasswordAsync(
        IPdfDocument document,
        string userPassword,
        string? ownerPassword = null,
        PdfSecurityPermissions? permissions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = userPassword;
        _ = ownerPassword;
        _ = permissions;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PdfSecurityWriteResult.Failed(UnavailableReason));
    }

    public Task<PdfSecurityWriteResult> SetPermissionsAsync(
        IPdfDocument document,
        string ownerPassword,
        PdfSecurityPermissions permissions,
        string? userPassword = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = ownerPassword;
        _ = permissions;
        _ = userPassword;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PdfSecurityWriteResult.Failed(UnavailableReason));
    }

    public Task<PdfSecurityWriteResult> RemoveProtectionAsync(
        IPdfDocument document,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = ownerPassword;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PdfSecurityWriteResult.Failed(UnavailableReason));
    }
}
