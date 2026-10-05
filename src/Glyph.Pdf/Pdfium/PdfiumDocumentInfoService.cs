using System.Runtime.InteropServices;
using System.Text;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumDocumentInfoService : IPdfDocumentInfoService
{
    // PDF Reference user-access permission bits (1-based bit numbers in the spec).
    private const uint PermPrint = 1u << 2;
    private const uint PermModify = 1u << 3;
    private const uint PermCopy = 1u << 4;
    private const uint PermAnnotate = 1u << 5;
    private const uint PermFillForms = 1u << 8;
    private const uint PermAssemble = 1u << 10;
    private const uint PermPrintHighQuality = 1u << 11;

    public PdfDocumentInfo GetInfo(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be a PDFium-backed instance.", nameof(document));
        }

        PdfiumLibrary.EnsureInitialized();
        lock (PdfiumSync.Gate)
        {
            pdfium.ThrowIfDisposed();
            var handle = pdfium.Handle;
            var flags = unchecked((uint)fpdfview.FPDF_GetDocPermissions(handle));
            var revision = fpdfview.FPDF_GetSecurityHandlerRevision(handle);
            long? fileSize = null;
            string? pdfVersion = null;
            if (!string.IsNullOrWhiteSpace(pdfium.Path) && File.Exists(pdfium.Path))
            {
                var info = new FileInfo(pdfium.Path);
                fileSize = info.Length;
                pdfVersion = ReadPdfVersion(pdfium.Path);
            }

            double? pageWidth = null;
            double? pageHeight = null;
            if (pdfium.PageCount > 0)
            {
                var page = pdfium.GetPage(0);
                pageWidth = page.WidthPoints;
                pageHeight = page.HeightPoints;
            }

            var fonts = CollectFontNames(handle, pdfium.PageCount);
            var attachmentCount = Math.Max(0, fpdf_attachment.FPDFDocGetAttachmentCount(handle));

            return new PdfDocumentInfo(
                Title: ReadMeta(handle, "Title"),
                Author: ReadMeta(handle, "Author"),
                Subject: ReadMeta(handle, "Subject"),
                Keywords: ReadMeta(handle, "Keywords"),
                Creator: ReadMeta(handle, "Creator"),
                Producer: ReadMeta(handle, "Producer"),
                CreationDate: ReadMeta(handle, "CreationDate"),
                ModificationDate: ReadMeta(handle, "ModDate"),
                PageCount: pdfium.PageCount,
                FilePath: pdfium.Path,
                FileSizeBytes: fileSize,
                PdfVersion: pdfVersion,
                PageWidthPoints: pageWidth,
                PageHeightPoints: pageHeight,
                Fonts: fonts,
                EmbeddedAttachmentCount: attachmentCount,
                IsEncrypted: pdfium.IsEncrypted,
                SecurityHandlerRevision: revision,
                PermissionFlags: flags,
                Permissions: DecodePermissions(flags));
        }
    }

    private static IReadOnlyList<string> CollectFontNames(FpdfDocumentT handle, int pageCount)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var pagesToScan = Math.Min(pageCount, 32);
        for (var pageIndex = 0; pageIndex < pagesToScan; pageIndex++)
        {
            var page = fpdfview.FPDF_LoadPage(handle, pageIndex);
            if (page is null)
            {
                continue;
            }

            try
            {
                var objectCount = fpdf_edit.FPDFPageCountObjects(page);
                for (var i = 0; i < objectCount; i++)
                {
                    var obj = fpdf_edit.FPDFPageGetObject(page, i);
                    if (obj is null || fpdf_edit.FPDFPageObjGetType(obj) != 1 /* text */)
                    {
                        continue;
                    }

                    var font = fpdf_edit.FPDFTextObjGetFont(obj);
                    if (font is null)
                    {
                        continue;
                    }

                    var name = ReadFontName(font);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }

        return names.ToList();
    }

    private static unsafe string? ReadFontName(FpdfFontT font)
    {
        var needed = fpdf_edit.FPDFFontGetFontName(font, null, 0);
        if (needed <= 1)
        {
            return null;
        }

        var buffer = new byte[needed];
        fixed (byte* ptr = buffer)
        {
            var written = fpdf_edit.FPDFFontGetFontName(font, (sbyte*)ptr, (uint)buffer.Length);
            if (written <= 1)
            {
                return null;
            }

            var length = (int)Math.Min(written, (uint)buffer.Length);
            while (length > 0 && buffer[length - 1] == 0)
            {
                length--;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, length).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }

    private static string? ReadPdfVersion(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[16];
            var read = stream.Read(header);
            if (read < 8)
            {
                return null;
            }

            var text = Encoding.ASCII.GetString(header[..read]);
            if (!text.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                return null;
            }

            var end = 5;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
            {
                end++;
            }

            return end > 5 ? text[5..end] : null;
        }
        catch
        {
            return null;
        }
    }

    private static PdfDocumentPermissions DecodePermissions(uint flags) =>
        new(
            CanPrint: (flags & PermPrint) != 0,
            CanModify: (flags & PermModify) != 0,
            CanCopy: (flags & PermCopy) != 0,
            CanAnnotate: (flags & PermAnnotate) != 0,
            CanFillForms: (flags & PermFillForms) != 0,
            CanAssemble: (flags & PermAssemble) != 0,
            CanPrintHighQuality: (flags & PermPrintHighQuality) != 0);

    private static string? ReadMeta(FpdfDocumentT handle, string tag)
    {
        var needed = fpdf_doc.FPDF_GetMetaText(handle, tag, IntPtr.Zero, 0);
        if (needed <= 2)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            var written = fpdf_doc.FPDF_GetMetaText(handle, tag, buffer, needed);
            if (written <= 2)
            {
                return null;
            }

            var bytes = new byte[written];
            Marshal.Copy(buffer, bytes, 0, (int)written);
            var text = Encoding.Unicode.GetString(bytes).TrimEnd('\0').Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
