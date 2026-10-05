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

    public Task<PdfAnnotationInfo> AddStickyNoteAsync(
        IPdfDocument document,
        int pageIndex,
        double xPoints,
        double yPoints,
        string contents,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        contents ??= string.Empty;

        const double iconSize = 20;

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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for sticky note.");
                    }

                    try
                    {
                        if (fpdf_annot.FPDFAnnotIsSupportedSubtype(PdfiumAnnotSubtypes.Text) == 0)
                        {
                            throw new NotSupportedException("PDFium does not support sticky-note (Text) annotations.");
                        }

                        var annot = fpdf_annot.FPDFPageCreateAnnot(page, PdfiumAnnotSubtypes.Text);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed for sticky note.");
                        }

                        try
                        {
                            var bounds = new PdfRect(xPoints, yPoints, xPoints + iconSize, yPoints + iconSize);
                            using var rect = new FS_RECTF_();
                            rect.Left = (float)bounds.Left;
                            rect.Bottom = (float)bounds.Bottom;
                            rect.Right = (float)bounds.Right;
                            rect.Top = (float)bounds.Top;
                            if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed for sticky note.");
                            }

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    color.R,
                                    color.G,
                                    color.B,
                                    color.A) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetColor failed for sticky note.");
                            }

                            if (!PdfiumAnnotStrings.SetString(annot, "Contents", contents))
                            {
                                throw new InvalidOperationException("Failed to set sticky note Contents.");
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created sticky note has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(
                                pageIndex,
                                index,
                                TextMarkupKind: null,
                                bounds,
                                color,
                                contents,
                                IsStickyNote: true);
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

    public Task<PdfAnnotationInfo> AddInkAsync(
        IPdfDocument document,
        int pageIndex,
        IReadOnlyList<PdfPagePoint> strokePoints,
        PdfAnnotationColor color,
        float borderWidthPoints = 2f,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        ArgumentNullException.ThrowIfNull(strokePoints);
        if (strokePoints.Count < 2)
        {
            throw new ArgumentException("Ink stroke requires at least two points.", nameof(strokePoints));
        }

        if (borderWidthPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(borderWidthPoints));
        }

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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for ink.");
                    }

                    try
                    {
                        if (fpdf_annot.FPDFAnnotIsSupportedSubtype(PdfiumAnnotSubtypes.Ink) == 0)
                        {
                            throw new NotSupportedException("PDFium does not support ink annotations.");
                        }

                        var annot = fpdf_annot.FPDFPageCreateAnnot(page, PdfiumAnnotSubtypes.Ink);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed for ink.");
                        }

                        try
                        {
                            var minX = strokePoints.Min(p => p.X);
                            var minY = strokePoints.Min(p => p.Y);
                            var maxX = strokePoints.Max(p => p.X);
                            var maxY = strokePoints.Max(p => p.Y);
                            var pad = borderWidthPoints;
                            var bounds = new PdfRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
                            using var rect = new FS_RECTF_();
                            rect.Left = (float)bounds.Left;
                            rect.Bottom = (float)bounds.Bottom;
                            rect.Right = (float)bounds.Right;
                            rect.Top = (float)bounds.Top;
                            if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed for ink.");
                            }

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    color.R,
                                    color.G,
                                    color.B,
                                    color.A) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetColor failed for ink.");
                            }

                            if (PdfiumNative.AnnotSetBorder(annot.__Instance, 0, 0, borderWidthPoints) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetBorder failed for ink.");
                            }

                            var points = strokePoints
                                .Select(p => new PdfiumNative.FsPointF { X = (float)p.X, Y = (float)p.Y })
                                .ToArray();
                            if (PdfiumNative.AnnotAddInkStroke(annot.__Instance, points, (ulong)points.Length) < 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_AddInkStroke failed.");
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created ink annotation has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(
                                pageIndex,
                                index,
                                TextMarkupKind: null,
                                bounds,
                                color,
                                Contents: null,
                                IsStickyNote: false,
                                IsInk: true);
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

    public Task<PdfAnnotationInfo> AddShapeAsync(
        IPdfDocument document,
        int pageIndex,
        PdfShapeKind kind,
        PdfRect bounds,
        PdfAnnotationColor borderColor,
        PdfAnnotationColor? fillColor = null,
        float borderWidthPoints = 1.5f,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        if (borderWidthPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(borderWidthPoints));
        }

        // PDFium exposes GetLine but not SetLine; straight lines are stored as 2-point ink strokes.
        if (kind == PdfShapeKind.Line)
        {
            var dx = Math.Abs(bounds.Right - bounds.Left);
            var dy = Math.Abs(bounds.Top - bounds.Bottom);
            if (dx < 1 && dy < 1)
            {
                throw new ArgumentException("Line endpoints must be distinct.", nameof(bounds));
            }

            return AddLineAsInkAsync(document, pageIndex, bounds, borderColor, borderWidthPoints, cancellationToken);
        }

        if (bounds.Width < 1 || bounds.Height < 1)
        {
            throw new ArgumentException("Shape bounds must have positive width and height.", nameof(bounds));
        }

        var subtype = kind switch
        {
            PdfShapeKind.Rectangle => PdfiumAnnotSubtypes.Square,
            PdfShapeKind.Ellipse => PdfiumAnnotSubtypes.Circle,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for shape.");
                    }

                    try
                    {
                        if (fpdf_annot.FPDFAnnotIsSupportedSubtype(subtype) == 0)
                        {
                            throw new NotSupportedException($"PDFium does not support shape subtype {subtype}.");
                        }

                        var annot = fpdf_annot.FPDFPageCreateAnnot(page, subtype);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed for shape.");
                        }

                        try
                        {
                            using var rect = new FS_RECTF_();
                            rect.Left = (float)bounds.Left;
                            rect.Bottom = (float)bounds.Bottom;
                            rect.Right = (float)bounds.Right;
                            rect.Top = (float)bounds.Top;
                            if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed for shape.");
                            }

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    borderColor.R,
                                    borderColor.G,
                                    borderColor.B,
                                    borderColor.A) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetColor failed for shape border.");
                            }

                            if (fillColor is { } fill)
                            {
                                if (fpdf_annot.FPDFAnnotSetColor(
                                        annot,
                                        FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_InteriorColor,
                                        fill.R,
                                        fill.G,
                                        fill.B,
                                        fill.A) == 0)
                                {
                                    throw new InvalidOperationException("FPDFAnnot_SetColor failed for shape fill.");
                                }
                            }

                            if (PdfiumNative.AnnotSetBorder(annot.__Instance, 0, 0, borderWidthPoints) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetBorder failed for shape.");
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created shape annotation has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(
                                pageIndex,
                                index,
                                TextMarkupKind: null,
                                bounds,
                                borderColor,
                                Contents: null,
                                IsStickyNote: false,
                                IsInk: false,
                                ShapeKind: kind);
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

    public Task SetContentsAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        string contents,
        CancellationToken cancellationToken = default)
    {
        return MutateAnnotAsync(
            document,
            pageIndex,
            annotIndex,
            cancellationToken,
            annot =>
            {
                if (!PdfiumAnnotStrings.SetString(annot, "Contents", contents ?? string.Empty))
                {
                    throw new InvalidOperationException("Failed to set annotation Contents.");
                }
            });
    }

    public Task SetColorAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default)
    {
        return MutateAnnotAsync(
            document,
            pageIndex,
            annotIndex,
            cancellationToken,
            annot =>
            {
                if (fpdf_annot.FPDFAnnotSetColor(
                        annot,
                        FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                        color.R,
                        color.G,
                        color.B,
                        color.A) == 0)
                {
                    throw new InvalidOperationException("Failed to set annotation color.");
                }
            });
    }

    public Task MoveAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfRect bounds,
        CancellationToken cancellationToken = default)
    {
        return MutateAnnotAsync(
            document,
            pageIndex,
            annotIndex,
            cancellationToken,
            annot =>
            {
                using var rect = new FS_RECTF_();
                rect.Left = (float)bounds.Left;
                rect.Bottom = (float)bounds.Bottom;
                rect.Right = (float)bounds.Right;
                rect.Top = (float)bounds.Top;
                if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
                {
                    throw new InvalidOperationException("Failed to move annotation.");
                }
            });
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

                    var contents = PdfiumAnnotStrings.GetString(annot, "Contents");
                    var isSticky = subtype == PdfiumAnnotSubtypes.Text;
                    var isInk = subtype == PdfiumAnnotSubtypes.Ink;
                    var shapeKind = FromShapeSubtype(subtype);
                    results.Add(new PdfAnnotationInfo(
                        pageIndex,
                        i,
                        kind,
                        bounds,
                        color,
                        contents,
                        isSticky,
                        isInk,
                        shapeKind));
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

    private Task MutateAnnotAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken,
        Action<FpdfAnnotationT> mutate)
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
                        var annot = fpdf_annot.FPDFPageGetAnnot(page, annotIndex);
                        if (annot is null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(annotIndex));
                        }

                        try
                        {
                            mutate(annot);
                            pdfium.NotifyAnnotationsChanged();
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

    private async Task<PdfAnnotationInfo> AddLineAsInkAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        PdfAnnotationColor borderColor,
        float borderWidthPoints,
        CancellationToken cancellationToken)
    {
        var created = await AddInkAsync(
            document,
            pageIndex,
            [
                new PdfPagePoint(bounds.Left, bounds.Bottom),
                new PdfPagePoint(bounds.Right, bounds.Top),
            ],
            borderColor,
            borderWidthPoints,
            cancellationToken);
        return created with { ShapeKind = PdfShapeKind.Line };
    }

    private static PdfShapeKind? FromShapeSubtype(int subtype) =>
        subtype switch
        {
            PdfiumAnnotSubtypes.Square => PdfShapeKind.Rectangle,
            PdfiumAnnotSubtypes.Circle => PdfShapeKind.Ellipse,
            PdfiumAnnotSubtypes.Line => PdfShapeKind.Line,
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
