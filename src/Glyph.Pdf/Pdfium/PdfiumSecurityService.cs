using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Security;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumSecurityService : IPdfSecurityService
{
    private readonly IPdfSecurityInfoService _info;

    public PdfiumSecurityService(IPdfSecurityInfoService info)
    {
        _info = info;
    }

    public Task<PdfSecurityInfo> GetInfoAsync(IPdfDocument document, CancellationToken cancellationToken = default) =>
        _info.GetAsync(document, cancellationToken);

    public Task ProtectAsync(
        IPdfDocument document,
        string outputPath,
        string userPassword,
        string? ownerPassword = null,
        PdfPermissionFlags deny = PdfPermissionFlags.None,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(userPassword);
        var pdfium = RequirePdfium(document);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                byte[] clearBytes;
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    clearBytes = PdfiumDocumentSaver.SaveToBytes(pdfium.Handle);
                }

                var permissions = PdfRc4StandardSecurity.BuildPermissions(
                    allowPrint: (deny & PdfPermissionFlags.DenyPrint) == 0,
                    allowModify: (deny & PdfPermissionFlags.DenyModify) == 0,
                    allowCopy: (deny & PdfPermissionFlags.DenyCopy) == 0,
                    allowAnnotate: (deny & PdfPermissionFlags.DenyAnnotate) == 0);

                var encrypted = PdfRc4StandardSecurity.Encrypt(
                    clearBytes,
                    userPassword,
                    ownerPassword ?? userPassword,
                    permissions);

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(outputPath, encrypted);
            },
            cancellationToken);
    }

    public Task RemoveProtectionAsync(
        IPdfDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var pdfium = RequirePdfium(document);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    // SaveAsCopy of an encrypted doc often preserves /Encrypt. Rebuild into a new
                    // unencrypted document by importing pages, then save.
                    var dest = fpdf_edit.FPDF_CreateNewDocument();
                    if (dest is null)
                    {
                        throw new InvalidOperationException("Failed to create decrypted PDF document.");
                    }

                    try
                    {
                        var order = Enumerable.Range(0, pdfium.PageCount).ToArray();
                        var range = PdfiumPageCatalog.ToPageRange(order);
                        if (fpdf_ppo.FPDF_ImportPages(dest, pdfium.Handle, range, 0) == 0)
                        {
                            throw new InvalidOperationException("Failed to import pages while removing protection.");
                        }

                        PdfiumDocumentSaver.SaveToPath(dest, outputPath);
                    }
                    finally
                    {
                        fpdfview.FPDF_CloseDocument(dest);
                    }
                }
            },
            cancellationToken);
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return pdfium;
    }
}
