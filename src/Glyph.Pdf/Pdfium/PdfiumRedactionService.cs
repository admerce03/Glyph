using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Redaction;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumRedactionService : IPdfRedactionService
{
    // PDFium FPDF_PAGEOBJ_* / FPDF_FILLMODE_* (not always exported as named constants).
    private const int PageObjUnknown = 0;
    private const int PageObjText = 1;
    private const int PageObjPath = 2;
    private const int PageObjImage = 3;
    private const int FillModeWinding = 2;
    // PDFium FPDF_NO_INCREMENTAL — full rewrite before Info patch (stable trailer).
    private const uint SaveNoIncremental = 2;

    private readonly PdfRedactionPendingStore _store = new();

    public IReadOnlyList<PdfPendingRedaction> GetPending(IPdfDocument document)
        => _store.Get(RequirePdfium(document));

    public PdfPendingRedaction MarkRectangle(IPdfDocument document, int pageIndex, PdfRect bounds, string? label = null)
    {
        var pdfium = RequirePdfium(document);
        ValidatePage(pdfium, pageIndex);
        ValidateBounds(bounds);
        return _store.Add(
            pdfium,
            new PdfPendingRedaction(Guid.NewGuid(), pageIndex, PdfRedactionKind.Rectangle, Normalize(bounds), label));
    }

    public PdfPendingRedaction MarkTextRegion(IPdfDocument document, int pageIndex, PdfRect bounds, string? label = null)
    {
        var pdfium = RequirePdfium(document);
        ValidatePage(pdfium, pageIndex);
        ValidateBounds(bounds);
        return _store.Add(
            pdfium,
            new PdfPendingRedaction(Guid.NewGuid(), pageIndex, PdfRedactionKind.Text, Normalize(bounds), label));
    }

    public bool RemovePending(IPdfDocument document, Guid redactionId)
        => _store.Remove(RequirePdfium(document), redactionId);

    public PdfPendingRedaction? UndoLastPending(IPdfDocument document)
        => _store.RemoveLast(RequirePdfium(document));

    public void ClearPending(IPdfDocument document)
        => _store.Clear(RequirePdfium(document));

    public Task<PdfRedactionApplyResult> ApplyAsync(
        IPdfDocument document,
        PdfRedactionApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        var opts = options ?? new PdfRedactionApplyOptions();
        var pending = _store.Get(pdfium);
        if (pending.Count == 0)
        {
            return Task.FromResult(new PdfRedactionApplyResult(0, 0, 0, 0, 0));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var byPage = pending.GroupBy(p => p.PageIndex).OrderBy(g => g.Key);
                    var pagesChanged = 0;
                    var textRemoved = 0;
                    var imagesRemoved = 0;
                    var annotationsRemoved = 0;
                    var attachmentsRemoved = 0;
                    var marks = 0;
                    var metadataCleared = false;

                    foreach (var group in byPage)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var pageIndex = group.Key;
                        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                        if (page is null)
                        {
                            continue;
                        }

                        try
                        {
                            var marksOnPage = group.ToList();
                            var boundsList = marksOnPage.Select(m => m.Bounds).ToList();
                            foreach (var mark in marksOnPage)
                            {
                                InsertBlackRect(page, mark.Bounds);
                                marks++;
                            }

                            if (opts.RemoveIntersectingTextObjects || opts.RemoveIntersectingImageObjects)
                            {
                                var (text, images) = RemoveIntersectingObjects(
                                    page,
                                    boundsList,
                                    opts.RemoveIntersectingTextObjects,
                                    opts.RemoveIntersectingImageObjects);
                                textRemoved += text;
                                imagesRemoved += images;
                            }

                            if (opts.RemoveIntersectingAnnotations)
                            {
                                annotationsRemoved += RemoveIntersectingAnnotations(page, boundsList);
                            }

                            if (fpdf_edit.FPDFPageGenerateContent(page) == 0)
                            {
                                throw new InvalidOperationException(
                                    $"Failed to generate page content after redaction on page {pageIndex + 1}.");
                            }

                            pagesChanged++;
                        }
                        finally
                        {
                            fpdfview.FPDF_ClosePage(page);
                        }
                    }

                    if (opts.RemoveEmbeddedAttachments && marks > 0)
                    {
                        attachmentsRemoved = RemoveAllEmbeddedAttachments(pdfium.Handle);
                    }

                    if (opts.RemoveMetadata && marks > 0)
                    {
                        var cleared = PdfInfoDictionaryPatcher.Apply(
                            PdfiumDocumentSaver.SaveToBytes(pdfium.Handle, SaveNoIncremental),
                            new PdfInfoFields(
                                Title: string.Empty,
                                Author: string.Empty,
                                Subject: string.Empty,
                                Keywords: string.Empty,
                                Creator: string.Empty,
                                Producer: string.Empty,
                                CreationDate: string.Empty,
                                ModDate: string.Empty));
                        pdfium.ReplaceFromBytes(cleared);
                        metadataCleared = true;
                    }

                    _store.Clear(pdfium);
                    if (pagesChanged > 0 || metadataCleared)
                    {
                        pdfium.NotifyAnnotationsChanged();
                    }

                    return new PdfRedactionApplyResult(
                        marks,
                        pagesChanged,
                        textRemoved,
                        imagesRemoved,
                        annotationsRemoved,
                        attachmentsRemoved,
                        metadataCleared);
                }
            },
            cancellationToken);
    }

    private static int RemoveAllEmbeddedAttachments(FpdfDocumentT handle)
    {
        var removed = 0;
        // High→low so deletions do not shift earlier indices.
        for (var i = fpdf_attachment.FPDFDocGetAttachmentCount(handle) - 1; i >= 0; i--)
        {
            if (fpdf_attachment.FPDFDocDeleteAttachment(handle, i) != 0)
            {
                removed++;
            }
        }

        return removed;
    }

    private static void InsertBlackRect(FpdfPageT page, PdfRect bounds)
    {
        var rect = fpdf_edit.FPDFPageObjCreateNewRect(
            (float)bounds.Left,
            (float)bounds.Bottom,
            (float)bounds.Width,
            (float)bounds.Height);
        if (rect is null)
        {
            throw new InvalidOperationException("Failed to create redaction rectangle page object.");
        }

        fpdf_edit.FPDFPageObjSetFillColor(rect, 0, 0, 0, 255);
        fpdf_edit.FPDFPathSetDrawMode(rect, FillModeWinding, 0);
        fpdf_edit.FPDFPageInsertObject(page, rect);
    }

    private static (int TextRemoved, int ImagesRemoved) RemoveIntersectingObjects(
        FpdfPageT page,
        IReadOnlyList<PdfRect> redactionBounds,
        bool removeText,
        bool removeImages)
    {
        var textRemoved = 0;
        var imagesRemoved = 0;
        // Walk high→low so removals don't shift earlier indices.
        var count = fpdf_edit.FPDFPageCountObjects(page);
        for (var i = count - 1; i >= 0; i--)
        {
            var obj = fpdf_edit.FPDFPageGetObject(page, i);
            if (obj is null)
            {
                continue;
            }

            var type = fpdf_edit.FPDFPageObjGetType(obj);
            var isText = type == PageObjText;
            var isImage = type == PageObjImage;
            if ((!isText || !removeText) && (!isImage || !removeImages))
            {
                continue;
            }

            float left = 0, bottom = 0, right = 0, top = 0;
            if (fpdf_edit.FPDFPageObjGetBounds(obj, ref left, ref bottom, ref right, ref top) == 0)
            {
                continue;
            }

            var objBounds = new PdfRect(left, bottom, right, top);
            if (!redactionBounds.Any(r => r.Intersects(objBounds) && CoverageRatio(objBounds, r) >= 0.35))
            {
                continue;
            }

            if (fpdf_edit.FPDFPageRemoveObject(page, obj) != 0)
            {
                if (isText)
                {
                    textRemoved++;
                }
                else if (isImage)
                {
                    imagesRemoved++;
                }
            }
        }

        return (textRemoved, imagesRemoved);
    }

    private static int RemoveIntersectingAnnotations(FpdfPageT page, IReadOnlyList<PdfRect> redactionBounds)
    {
        var removed = 0;
        // High→low so removals don't shift earlier indices.
        var count = fpdf_annot.FPDFPageGetAnnotCount(page);
        for (var i = count - 1; i >= 0; i--)
        {
            var annot = fpdf_annot.FPDFPageGetAnnot(page, i);
            if (annot is null)
            {
                continue;
            }

            try
            {
                using var rect = new FS_RECTF_();
                if (fpdf_annot.FPDFAnnotGetRect(annot, rect) == 0)
                {
                    continue;
                }

                var bounds = new PdfRect(rect.Left, rect.Bottom, rect.Right, rect.Top);
                if (!redactionBounds.Any(r => r.Intersects(bounds) && CoverageRatio(bounds, r) >= 0.35))
                {
                    continue;
                }
            }
            finally
            {
                fpdf_annot.FPDFPageCloseAnnot(annot);
            }

            if (fpdf_annot.FPDFPageRemoveAnnot(page, i) != 0)
            {
                removed++;
            }
        }

        return removed;
    }

    private static double CoverageRatio(PdfRect obj, PdfRect redaction)
    {
        var left = Math.Max(obj.Left, redaction.Left);
        var bottom = Math.Max(obj.Bottom, redaction.Bottom);
        var right = Math.Min(obj.Right, redaction.Right);
        var top = Math.Min(obj.Top, redaction.Top);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, top - bottom);
        var intersection = width * height;
        var area = Math.Max(1e-6, obj.Width * obj.Height);
        return intersection / area;
    }

    private static PdfRect Normalize(PdfRect bounds)
    {
        var left = Math.Min(bounds.Left, bounds.Right);
        var right = Math.Max(bounds.Left, bounds.Right);
        var bottom = Math.Min(bounds.Bottom, bounds.Top);
        var top = Math.Max(bounds.Bottom, bounds.Top);
        return new PdfRect(left, bottom, right, top);
    }

    private static void ValidateBounds(PdfRect bounds)
    {
        // Use absolute extents so inverted drag corners can still normalize.
        var width = Math.Abs(bounds.Right - bounds.Left);
        var height = Math.Abs(bounds.Top - bounds.Bottom);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Redaction bounds must have positive width and height.", nameof(bounds));
        }
    }

    private static void ValidatePage(PdfiumDocument pdfium, int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be a PDFium-backed instance.", nameof(document));
        }

        return pdfium;
    }
}
