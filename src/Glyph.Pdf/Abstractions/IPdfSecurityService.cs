namespace Glyph.Pdf.Abstractions;

/// <summary>
/// PDF password / permission write path (F23-02…07, F45-09).
/// Write-encrypt is blocked pending ADR-015; open + Info remain on <see cref="IPdfDocumentInfoService"/>.
/// </summary>
public interface IPdfSecurityService
{
    /// <summary>True when create/change/remove password or permission write is available.</summary>
    bool WriteProtectSupported { get; }

    /// <summary>Human-readable reason when <see cref="WriteProtectSupported"/> is false (includes ADR id).</summary>
    string UnavailableReason { get; }

    /// <summary>ADR id gating write-protect, or empty when supported.</summary>
    string BlockingAdr { get; }

    /// <summary>
    /// Attempt to set a user (open) password. Returns a structured failure while ADR-015 is open.
    /// </summary>
    Task<PdfSecurityWriteResult> SetOpenPasswordAsync(
        IPdfDocument document,
        string userPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempt to remove password protection. Returns a structured failure while ADR-015 is open.
    /// </summary>
    Task<PdfSecurityWriteResult> RemoveProtectionAsync(
        IPdfDocument document,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default);
}
