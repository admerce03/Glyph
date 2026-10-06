using Glyph.Core.Pdf;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PDFiumCore;

namespace Glyph.Pdf.Security;

/// <summary>
/// ADR-015 Accept A — PdfSharp MIT write-encrypt adapter (F23-02…07, F45-09).
/// PDFium remains the renderer/open path; PdfSharp only applies Standard Security Handler.
/// </summary>
public sealed class PdfSharpSecurityService : IPdfSecurityService
{
    public bool WriteProtectSupported => PdfPasswordWriteBlockedPolicy.CreatePasswordProtectedSupported;

    public string UnavailableReason => string.Empty;

    public string BlockingAdr => string.Empty;

    public Task<PdfSecurityWriteResult> SetOpenPasswordAsync(
        IPdfDocument document,
        string userPassword,
        string? ownerPassword = null,
        PdfSecurityPermissions? permissions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrEmpty(userPassword))
        {
            return Task.FromResult(PdfSecurityWriteResult.Failed("Open password must not be empty."));
        }

        var owner = string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword;
        return ApplyProtectionAsync(document, userPassword, owner, permissions ?? PdfSecurityPermissions.AllowAll, cancellationToken);
    }

    public Task<PdfSecurityWriteResult> SetPermissionsAsync(
        IPdfDocument document,
        string ownerPassword,
        PdfSecurityPermissions permissions,
        string? userPassword = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(permissions);
        if (string.IsNullOrEmpty(ownerPassword))
        {
            return Task.FromResult(PdfSecurityWriteResult.Failed("Owner password must not be empty."));
        }

        // Owner-only protection still needs a non-empty password for PdfSharp encrypt.
        var user = string.IsNullOrEmpty(userPassword) ? ownerPassword : userPassword;
        return ApplyProtectionAsync(document, user, ownerPassword, permissions, cancellationToken);
    }

    public Task<PdfSecurityWriteResult> RemoveProtectionAsync(
        IPdfDocument document,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = ownerPassword;
        var pdfium = RequirePdfium(document);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    if (!pdfium.IsEncrypted)
                    {
                        return PdfSecurityWriteResult.Ok("Document is not password-protected.");
                    }

                    var plain = SaveUnencryptedCopy(pdfium);
                    pdfium.ReplaceFromBytes(plain);
                    PersistIfPathSet(pdfium, plain);
                    return PdfSecurityWriteResult.Ok(PdfSecurityWriteUiCopy.StatusRemoved);
                }
            },
            cancellationToken);
    }

    private static Task<PdfSecurityWriteResult> ApplyProtectionAsync(
        IPdfDocument document,
        string userPassword,
        string ownerPassword,
        PdfSecurityPermissions permissions,
        CancellationToken cancellationToken)
    {
        var pdfium = RequirePdfium(document);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    try
                    {
                        var plain = SaveUnencryptedCopy(pdfium);
                        var encrypted = EncryptWithPdfSharp(plain, userPassword, ownerPassword, permissions);
                        pdfium.ReplaceFromBytes(encrypted, userPassword);
                        PersistIfPathSet(pdfium, encrypted);
                        return PdfSecurityWriteResult.Ok(PdfSecurityWriteUiCopy.StatusApplied);
                    }
                    catch (Exception ex)
                    {
                        return PdfSecurityWriteResult.Failed(ex.Message);
                    }
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// PDFium <c>SaveAsCopy</c> preserves the Standard Security Handler. Import pages into a
    /// fresh document to produce a decrypted byte stream while the source is already open.
    /// </summary>
    private static byte[] SaveUnencryptedCopy(PdfiumDocument pdfium)
    {
        if (!pdfium.IsEncrypted)
        {
            return PdfiumDocumentSaver.SaveToBytes(pdfium.Handle);
        }

        var pageCount = pdfium.PageCount;
        if (pageCount <= 0)
        {
            throw new InvalidOperationException("Cannot strip protection from a document with no pages.");
        }

        var dest = fpdf_edit.FPDF_CreateNewDocument();
        if (dest is null)
        {
            throw new InvalidOperationException("Failed to create a temporary PDF for decryption.");
        }

        try
        {
            var order = Enumerable.Range(0, pageCount).ToArray();
            var range = PdfiumPageCatalog.ToPageRange(order);
            var ok = fpdf_ppo.FPDF_ImportPages(dest, pdfium.Handle, range, 0);
            if (ok == 0)
            {
                throw new InvalidOperationException("Failed to import pages while removing encryption.");
            }

            return PdfiumDocumentSaver.SaveToBytes(dest);
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(dest);
        }
    }

    private static byte[] EncryptWithPdfSharp(
        byte[] plainPdfBytes,
        string userPassword,
        string ownerPassword,
        PdfSecurityPermissions permissions)
    {
        using var input = new MemoryStream(plainPdfBytes, writable: false);
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var security = doc.SecuritySettings;
        security.UserPassword = userPassword;
        security.OwnerPassword = ownerPassword;
        security.PermitPrint = permissions.PermitPrint;
        security.PermitFullQualityPrint = permissions.PermitFullQualityPrint;
        security.PermitModifyDocument = permissions.PermitModifyDocument;
        security.PermitExtractContent = permissions.PermitExtractContent;
        security.PermitAnnotations = permissions.PermitAnnotations;
        security.PermitFormsFill = permissions.PermitFormsFill;
        security.PermitAssembleDocument = permissions.PermitAssembleDocument;

        using var output = new MemoryStream();
        doc.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void PersistIfPathSet(PdfiumDocument pdfium, byte[] bytes)
    {
        var path = pdfium.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        File.WriteAllBytes(path, bytes);
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return pdfium;
    }
}
