using System.Globalization;
using System.Text;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Pending rededctions are stored as Square annotations with Subject <see cref="PendingSubject"/>.
/// Apply removes intersecting text/image objects and paints opaque black page content.
/// </summary>
public sealed class PdfiumRedactionService : IPdfRedactionService
{
    internal const string PendingSubject = "Glyph.Redaction.Pending";
    private const int PageObjText = 1;
    private const int PageObjImage = 3;
    private const int FillModeWinding = 2;

    public Task<IReadOnlyList<PdfRedactionMark>> ListAsync(
        IPdfDocument document,
        CancellationToken cancellationToken = default)
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
                    var marks = new List<PdfRedactionMark>();
                    for (var pageIndex = 0; pageIndex < pdfium.PageCount; pageIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        marks.AddRange(ReadPendingMarks(pdfium, pageIndex));
                    }

                    return (IReadOnlyList<PdfRedactionMark>)marks;
                }
            },
            cancellationToken);
    }

    public Task<PdfRedactionMark> MarkRectAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default) =>
        MarkCoreAsync(document, pageIndex, bounds, cancellationToken);

    public Task<PdfRedactionMark> MarkTextAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default) =>
        MarkCoreAsync(document, pageIndex, bounds, cancellationToken);

    public Task RemoveAsync(IPdfDocument document, string markId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markId);
        var pdfium = RequirePdfium(document);
        if (!TryParseMarkId(markId, out var pageIndex, out var annotIndex))
        {
            throw new ArgumentException("Unrecognized redaction mark id.", nameof(markId));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    ValidatePage(pdfium, pageIndex);
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {pageIndex}.");
                    }

                    try
                    {
                        var annot = fpdf_annot.FPDFPageGetAnnot(page, annotIndex);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("Redaction mark not found.");
                        }

                        try
                        {
                            if (!IsPendingRedaction(annot))
                            {
                                throw new InvalidOperationException("Annotation is not a pending redaction mark.");
                            }

                            if (fpdf_annot.FPDFPageRemoveAnnot(page, annotIndex) == 0)
                            {
                                throw new InvalidOperationException("Failed to remove redaction mark.");
                            }
                        }
                        finally
                        {
                            fpdf_annot.FPDFPageCloseAnnot(annot);
                        }
                    }
                    finally
                    {
                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }

    public Task ApplyAsync(IPdfDocument document, CancellationToken cancellationToken = default)
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
                    var pending = new List<(int PageIndex, int AnnotIndex, PdfRect Bounds)>();
                    for (var pageIndex = 0; pageIndex < pdfium.PageCount; pageIndex++)
                    {
                        foreach (var mark in ReadPendingMarks(pdfium, pageIndex))
                        {
                            if (TryParseMarkId(mark.Id, out var p, out var a))
                            {
                                pending.Add((p, a, mark.Bounds));
                            }
                        }
                    }

                    foreach (var group in pending.GroupBy(p => p.PageIndex))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ApplyPage(pdfium, group.Key, group.Select(g => g.Bounds).ToList());
                    }

                    // Remove pending marks from highest annot index first.
                    foreach (var group in pending.GroupBy(p => p.PageIndex))
                    {
                        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, group.Key);
                        if (page is null)
                        {
                            continue;
                        }

                        try
                        {
                            foreach (var annotIndex in group.Select(g => g.AnnotIndex).Distinct().OrderByDescending(i => i))
                            {
                                fpdf_annot.FPDFPageRemoveAnnot(page, annotIndex);
                            }
                        }
                        finally
                        {
                            fpdfview.FPDF_ClosePage(page);
                        }
                    }

                    pdfium.RebuildPages();
                }
            },
            cancellationToken);
    }

    private Task<PdfRedactionMark> MarkCoreAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        CancellationToken cancellationToken)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentException("Redaction bounds must have positive size.", nameof(bounds));
        }

        var pdfium = RequirePdfium(document);
        ValidatePage(pdfium, pageIndex);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {pageIndex}.");
                    }

                    FpdfAnnotationT? annot = null;
                    try
                    {
                        annot = fpdf_annot.FPDFPageCreateAnnot(page, (int)PdfAnnotationKind.Square);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("Failed to create redaction mark annotation.");
                        }

                        SetRect(annot, bounds);
                        // Preview: solid black-ish fill (pending, not yet applied).
                        fpdf_annot.FPDFAnnotSetColor(
                            annot,
                            FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_InteriorColor,
                            0,
                            0,
                            0,
                            200);
                        fpdf_annot.FPDFAnnotSetColor(
                            annot,
                            FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                            0,
                            0,
                            0,
                            255);
                        SetString(annot, "Subj", PendingSubject);
                        SetString(annot, "Contents", "Pending redaction");
                        var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                        return new PdfRedactionMark(FormatMarkId(pageIndex, index), pageIndex, bounds);
                    }
                    finally
                    {
                        if (annot is not null)
                        {
                            fpdf_annot.FPDFPageCloseAnnot(annot);
                        }

                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }

    private static void ApplyPage(PdfiumDocument pdfium, int pageIndex, IReadOnlyList<PdfRect> redactions)
    {
        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
        if (page is null)
        {
            throw new InvalidOperationException($"Failed to load page {pageIndex} for redaction.");
        }

        try
        {
            var count = fpdf_edit.FPDFPageCountObjects(page);
            for (var i = count - 1; i >= 0; i--)
            {
                var obj = fpdf_edit.FPDFPageGetObject(page, i);
                if (obj is null)
                {
                    continue;
                }

                var type = fpdf_edit.FPDFPageObjGetType(obj);
                if (type is not (PageObjText or PageObjImage))
                {
                    continue;
                }

                float left = 0, bottom = 0, right = 0, top = 0;
                if (fpdf_edit.FPDFPageObjGetBounds(obj, ref left, ref bottom, ref right, ref top) == 0)
                {
                    continue;
                }

                var objRect = new PdfRect(left, bottom, right, top);
                if (redactions.Any(r => r.Intersects(objRect)))
                {
                    fpdf_edit.FPDFPageRemoveObject(page, obj);
                }
            }

            foreach (var bounds in redactions)
            {
                var rect = fpdf_edit.FPDFPageObjCreateNewRect(
                    (float)bounds.Left,
                    (float)bounds.Bottom,
                    (float)bounds.Width,
                    (float)bounds.Height);
                if (rect is null)
                {
                    throw new InvalidOperationException("Failed to create redaction rectangle.");
                }

                fpdf_edit.FPDFPageObjSetFillColor(rect, 0, 0, 0, 255);
                fpdf_edit.FPDFPathSetDrawMode(rect, FillModeWinding, 0);
                fpdf_edit.FPDFPageInsertObject(page, rect);
            }

            if (fpdf_edit.FPDFPageGenerateContent(page) == 0)
            {
                throw new InvalidOperationException("Failed to generate page content after redaction.");
            }
        }
        finally
        {
            fpdfview.FPDF_ClosePage(page);
        }
    }

    private static List<PdfRedactionMark> ReadPendingMarks(PdfiumDocument pdfium, int pageIndex)
    {
        var results = new List<PdfRedactionMark>();
        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
        if (page is null)
        {
            return results;
        }

        try
        {
            var count = fpdf_annot.FPDFPageGetAnnotCount(page);
            for (var i = 0; i < count; i++)
            {
                var annot = fpdf_annot.FPDFPageGetAnnot(page, i);
                if (annot is null)
                {
                    continue;
                }

                try
                {
                    if (!IsPendingRedaction(annot))
                    {
                        continue;
                    }

                    if (!TryGetRect(annot, out var bounds))
                    {
                        continue;
                    }

                    results.Add(new PdfRedactionMark(FormatMarkId(pageIndex, i), pageIndex, bounds));
                }
                finally
                {
                    fpdf_annot.FPDFPageCloseAnnot(annot);
                }
            }
        }
        finally
        {
            fpdfview.FPDF_ClosePage(page);
        }

        return results;
    }

    private static bool IsPendingRedaction(FpdfAnnotationT annot)
    {
        var subject = GetString(annot, "Subj");
        return string.Equals(subject, PendingSubject, StringComparison.Ordinal);
    }

    private static string FormatMarkId(int pageIndex, int annotIndex) =>
        string.Create(CultureInfo.InvariantCulture, $"p{pageIndex}:a{annotIndex}");

    private static bool TryParseMarkId(string id, out int pageIndex, out int annotIndex)
    {
        pageIndex = 0;
        annotIndex = 0;
        var parts = id.Split(':');
        if (parts.Length != 2 || parts[0].Length < 2 || parts[1].Length < 2)
        {
            return false;
        }

        return parts[0][0] == 'p'
            && parts[1][0] == 'a'
            && int.TryParse(parts[0].AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out pageIndex)
            && int.TryParse(parts[1].AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out annotIndex);
    }

    private static void SetRect(FpdfAnnotationT annot, PdfRect bounds)
    {
        using var rect = new FS_RECTF_();
        rect.Left = (float)bounds.Left;
        rect.Bottom = (float)bounds.Bottom;
        rect.Right = (float)bounds.Right;
        rect.Top = (float)bounds.Top;
        if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
        {
            throw new InvalidOperationException("Failed to set redaction rectangle.");
        }
    }

    private static bool TryGetRect(FpdfAnnotationT annot, out PdfRect bounds)
    {
        using var rect = new FS_RECTF_();
        if (fpdf_annot.FPDFAnnotGetRect(annot, rect) == 0)
        {
            bounds = default;
            return false;
        }

        bounds = new PdfRect(rect.Left, rect.Bottom, rect.Right, rect.Top);
        return true;
    }

    private static unsafe void SetString(FpdfAnnotationT annot, string key, string value)
    {
        var utf16 = Encoding.Unicode.GetBytes(value + "\0");
        fixed (byte* ptr = utf16)
        {
            if (fpdf_annot.FPDFAnnotSetStringValue(annot, key, ref *(ushort*)ptr) == 0)
            {
                throw new InvalidOperationException($"Failed to set annotation string '{key}'.");
            }
        }
    }

    private static string? GetString(FpdfAnnotationT annot, string key)
    {
        var needed = fpdf_annot.FPDFAnnotGetStringValue(annot, key, ref UnsafeDummy, 0);
        if (needed <= 2)
        {
            return null;
        }

        var buffer = new ushort[needed / 2];
        fpdf_annot.FPDFAnnotGetStringValue(annot, key, ref buffer[0], needed);
        var chars = new char[buffer.Length];
        var len = 0;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 0)
            {
                break;
            }

            chars[len++] = (char)buffer[i];
        }

        return len == 0 ? null : new string(chars, 0, len);
    }

    private static ushort UnsafeDummy;

    private static void ValidatePage(PdfiumDocument document, int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
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
