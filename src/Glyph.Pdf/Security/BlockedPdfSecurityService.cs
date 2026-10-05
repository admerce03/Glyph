using Glyph.Core.Pdf;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Security;

/// <summary>
/// ADR-015 blocked write-encrypt implementation — surfaces structured failures until an encrypt stack is approved.
/// </summary>
public sealed class BlockedPdfSecurityService : IPdfSecurityService
{
    public bool WriteProtectSupported => PdfPasswordWriteBlockedPolicy.CreatePasswordProtectedSupported;

    public string UnavailableReason => PdfPasswordWriteBlockedPolicy.Reason;

    public string BlockingAdr => PdfPasswordWriteBlockedPolicy.Adr;

    public Task<PdfSecurityWriteResult> SetOpenPasswordAsync(
        IPdfDocument document,
        string userPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
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
