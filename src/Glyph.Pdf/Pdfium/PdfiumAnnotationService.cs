using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumAnnotationService : IPdfAnnotationService
{
    public Task<PdfAnnotationInfo> AddTextMarkupAsync(
        IPdfDocument document,
        int pageIndex,
        PdfTextMarkupKind kind,
        IReadOnlyList<PdfQuad> quads,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        ArgumentNullException.ThrowIfNull(quads);
        if (quads.Count == 0)
        {
            throw new ArgumentException("At least one quad is required for text markup.", nameof(quads));
        }

        var subtype = ToSubtype(kind);

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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for annotation.");
                    }

                    try
                    {
                        if (fpdf_annot.FPDFAnnotIsSupportedSubtype(subtype) == 0)
                        {
                            throw new NotSupportedException($"PDFium does not support annotation subtype {subtype}.");
                        }

                        var annot = fpdf_annot.FPDFPageCreateAnnot(page, subtype);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed.");
                        }

                        try
                        {
                            var bounds = UnionBounds(quads);
                            using var rect = new FS_RECTF_();
                            // PDFium FS_RECTF_: Top is the greater Y in PDF space.
                            rect.Left = (float)bounds.Left;
                            rect.Bottom = (float)bounds.Bottom;
                            rect.Right = (float)bounds.Right;
                            rect.Top = (float)bounds.Top;
                            if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed.");
                            }

                            for (var i = 0; i < quads.Count; i++)
                            {
                                var q = quads[i];
                                using var quad = new FS_QUADPOINTSF();
                                quad.X1 = (float)q.X1;
                                quad.Y1 = (float)q.Y1;
                                quad.X2 = (float)q.X2;
                                quad.Y2 = (float)q.Y2;
                                quad.X3 = (float)q.X3;
                                quad.Y3 = (float)q.Y3;
                                quad.X4 = (float)q.X4;
                                quad.Y4 = (float)q.Y4;

                                // New markup annots start with zero quads; only Append is valid until count > 0.
                                if (fpdf_annot.FPDFAnnotAppendAttachmentPoints(annot, quad) == 0)
                                {
                                    throw new InvalidOperationException("Failed to append annotation QuadPoints.");
                                }
                            }

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    color.R,
                                    color.G,
                                    color.B,
                                    color.A) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetColor failed.");
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created annotation has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(pageIndex, index, kind, bounds, color);
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

    public Task<IReadOnlyList<PdfAnnotationInfo>> ListAsync(
        IPdfDocument document,
        int? pageIndex = null,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        if (pageIndex is int only)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(only);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(only, pdfium.PageCount);
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var results = new List<PdfAnnotationInfo>();
                    var start = pageIndex ?? 0;
                    var end = pageIndex ?? (pdfium.PageCount - 1);
                    for (var p = start; p <= end; p++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        CollectPageAnnotations(pdfium, p, results);
                    }

                    return (IReadOnlyList<PdfAnnotationInfo>)results;
                }
            },
            cancellationToken);
    }

    public Task RemoveAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(annotIndex);

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

                    try
                    {
                        var count = fpdf_annot.FPDFPageGetAnnotCount(page);
                        if (annotIndex >= count)
                        {
                            throw new ArgumentOutOfRangeException(nameof(annotIndex));
                        }

                        if (fpdf_annot.FPDFPageRemoveAnnot(page, annotIndex) == 0)
                        {
                            throw new InvalidOperationException("FPDFPage_RemoveAnnot failed.");
                        }

                        pdfium.NotifyAnnotationsChanged();
                    }
                    finally
                    {
                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }

    private static void CollectPageAnnotations(PdfiumDocument pdfium, int pageIndex, List<PdfAnnotationInfo> results)
    {
        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
        if (page is null)
        {
            throw new InvalidOperationException($"Failed to load page {pageIndex}.");
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
                    var subtype = fpdf_annot.FPDFAnnotGetSubtype(annot);
                    var kind = FromSubtype(subtype);
                    using var rect = new FS_RECTF_();
                    PdfRect bounds = default;
                    if (fpdf_annot.FPDFAnnotGetRect(annot, rect) != 0)
                    {
                        bounds = new PdfRect(rect.Left, rect.Bottom, rect.Right, rect.Top);
                    }

                    PdfAnnotationColor? color = null;
                    uint r = 0, g = 0, b = 0, a = 0;
                    if (fpdf_annot.FPDFAnnotGetColor(
                            annot,
                            FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                            ref r,
                            ref g,
                            ref b,
                            ref a) != 0)
                    {
                        color = new PdfAnnotationColor((byte)r, (byte)g, (byte)b, (byte)a);
                    }

                    results.Add(new PdfAnnotationInfo(pageIndex, i, kind, bounds, color));
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
    }

    private static PdfRect UnionBounds(IReadOnlyList<PdfQuad> quads)
    {
        var bounds = quads[0].Bounds;
        for (var i = 1; i < quads.Count; i++)
        {
            var b = quads[i].Bounds;
            bounds = new PdfRect(
                Math.Min(bounds.Left, b.Left),
                Math.Min(bounds.Bottom, b.Bottom),
                Math.Max(bounds.Right, b.Right),
                Math.Max(bounds.Top, b.Top));
        }

        return bounds;
    }

    private static int ToSubtype(PdfTextMarkupKind kind) =>
        kind switch
        {
            PdfTextMarkupKind.Highlight => PdfiumAnnotSubtypes.Highlight,
            PdfTextMarkupKind.Underline => PdfiumAnnotSubtypes.Underline,
            PdfTextMarkupKind.StrikeOut => PdfiumAnnotSubtypes.StrikeOut,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static PdfTextMarkupKind? FromSubtype(int subtype) =>
        subtype switch
        {
            PdfiumAnnotSubtypes.Highlight => PdfTextMarkupKind.Highlight,
            PdfiumAnnotSubtypes.Underline => PdfTextMarkupKind.Underline,
            PdfiumAnnotSubtypes.StrikeOut => PdfTextMarkupKind.StrikeOut,
            _ => null,
        };

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
