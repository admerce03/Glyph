namespace Glyph.Pdf.Abstractions;

/// <summary>
/// PDF password / permission write path (F23-02…07, F45-09).
/// Write-encrypt uses PdfSharp (ADR-015 Accept A); open + Info remain on <see cref="IPdfDocumentInfoService"/>.
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
    /// Set (or replace) the user (open) password. When <paramref name="ownerPassword"/> is null,
    /// the owner password defaults to the user password. Permission flags default to allow-all.
    /// </summary>
    Task<PdfSecurityWriteResult> SetOpenPasswordAsync(
        IPdfDocument document,
        string userPassword,
        string? ownerPassword = null,
        PdfSecurityPermissions? permissions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Change owner password and/or permission flags on an existing document (F23-04…06).
    /// Re-applies the current open-password requirement when <paramref name="userPassword"/> is set;
    /// otherwise writes owner/permission-only protection.
    /// </summary>
    Task<PdfSecurityWriteResult> SetPermissionsAsync(
        IPdfDocument document,
        string ownerPassword,
        PdfSecurityPermissions permissions,
        string? userPassword = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove password protection. Uses an in-memory PDFium page import (no password required while open).
    /// <paramref name="ownerPassword"/> is reserved for path-only callers and ignored for open documents.
    /// </summary>
    Task<PdfSecurityWriteResult> RemoveProtectionAsync(
        IPdfDocument document,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default);
}
