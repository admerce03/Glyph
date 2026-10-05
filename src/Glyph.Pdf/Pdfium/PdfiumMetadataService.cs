using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumMetadataService : IPdfMetadataService
{
    public Task<PdfDocumentMetadata> GetAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                string? title;
                string? author;
                string? subject;
                string? keywords;
                string? creator;
                string? producer;
                string? creationDate;
                string? modDate;
                ulong permissions;
                double? width = null;
                double? height = null;
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    permissions = unchecked((ulong)fpdfview.FPDF_GetDocPermissions(pdfium.Handle));
                    title = GetMeta(pdfium.Handle, "Title");
                    author = GetMeta(pdfium.Handle, "Author");
                    subject = GetMeta(pdfium.Handle, "Subject");
                    keywords = GetMeta(pdfium.Handle, "Keywords");
                    creator = GetMeta(pdfium.Handle, "Creator");
                    producer = GetMeta(pdfium.Handle, "Producer");
                    creationDate = GetMeta(pdfium.Handle, "CreationDate");
                    modDate = GetMeta(pdfium.Handle, "ModDate");
                    if (pdfium.PageCount > 0)
                    {
                        width = pdfium.GetPage(0).WidthPoints;
                        height = pdfium.GetPage(0).HeightPoints;
                    }
                }

                MergeSidecar(pdfium.Path, ref title, ref author, ref subject, ref keywords);

                long? fileSize = null;
                if (!string.IsNullOrWhiteSpace(pdfium.Path) && File.Exists(pdfium.Path))
                {
                    fileSize = new FileInfo(pdfium.Path).Length;
                }

                return new PdfDocumentMetadata(
                    title,
                    author,
                    subject,
                    keywords,
                    creator,
                    producer,
                    creationDate,
                    modDate,
                    pdfium.PageCount,
                    PdfVersion: null,
                    pdfium.IsEncrypted,
                    permissions,
                    fileSize,
                    width,
                    height);
            },
            cancellationToken);
    }

    public Task SetAsync(
        IPdfDocument document,
        string? title,
        string? author,
        string? subject,
        string? keywords,
        CancellationToken cancellationToken = default)
    {
        // PDFium public API lacks SetMetaText; persist editable fields via a sidecar JSON next to the file
        // and surface them preferentially on Get when present.
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = pdfium.Path;
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException("Document must have a path to persist metadata edits.");
                }

                var sidecar = SidecarPath(path);
                var payload = JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["title"] = title,
                    ["author"] = author,
                    ["subject"] = subject,
                    ["keywords"] = keywords,
                });
                File.WriteAllText(sidecar, payload, Encoding.UTF8);
            },
            cancellationToken);
    }

    internal static string SidecarPath(string documentPath) => documentPath + ".glyph-meta.json";

    private static void MergeSidecar(
        string? path,
        ref string? title,
        ref string? author,
        ref string? subject,
        ref string? keywords)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var sidecar = SidecarPath(path);
        if (!File.Exists(sidecar))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(sidecar);
            var data = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
            if (data is null)
            {
                return;
            }

            if (data.TryGetValue("title", out var t))
            {
                title = t;
            }

            if (data.TryGetValue("author", out var a))
            {
                author = a;
            }

            if (data.TryGetValue("subject", out var s))
            {
                subject = s;
            }

            if (data.TryGetValue("keywords", out var k))
            {
                keywords = k;
            }
        }
        catch (JsonException)
        {
            // Ignore corrupt sidecars; native metadata still returned.
        }
    }

    private static string? GetMeta(FpdfDocumentT handle, string tag)
    {
        var needed = fpdf_doc.FPDF_GetMetaText(handle, tag, IntPtr.Zero, 0);
        if (needed <= 2)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            fpdf_doc.FPDF_GetMetaText(handle, tag, buffer, needed);
            return Marshal.PtrToStringUni(buffer)?.TrimEnd('\0');
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
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

public sealed class PdfiumSecurityInfoService : IPdfSecurityInfoService
{
    public Task<PdfSecurityInfo> GetAsync(IPdfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var permissions = unchecked((ulong)fpdfview.FPDF_GetDocPermissions(pdfium.Handle));
                    var revision = fpdfview.FPDF_GetSecurityHandlerRevision(pdfium.Handle);
                    // PDF permission bits (when encrypted); unrestricted docs report all bits set.
                    return new PdfSecurityInfo(
                        pdfium.IsEncrypted,
                        permissions,
                        revision,
                        CanPrint: (permissions & 4) != 0,
                        CanModify: (permissions & 8) != 0,
                        CanCopy: (permissions & 16) != 0,
                        CanAnnotate: (permissions & 32) != 0);
                }
            },
            cancellationToken);
    }
}
