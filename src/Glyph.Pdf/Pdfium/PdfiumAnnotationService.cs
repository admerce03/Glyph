using System.Runtime.InteropServices;
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
        string? author = null,
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

                            var resolvedAuthor = string.IsNullOrWhiteSpace(author) ? null : author.Trim();
                            if (resolvedAuthor is not null &&
                                !PdfiumAnnotStrings.SetString(annot, "T", resolvedAuthor))
                            {
                                throw new InvalidOperationException("Failed to set sticky note author (/T).");
                            }

                            var now = FormatPdfDate(DateTimeOffset.Now);
                            _ = PdfiumAnnotStrings.SetString(annot, "CreationDate", now);
                            _ = PdfiumAnnotStrings.SetString(annot, "M", now);

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
                                IsStickyNote: true,
                                Author: resolvedAuthor);
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

    public async Task<PdfAnnotationInfo> AddFreeformAsync(
        IPdfDocument document,
        int pageIndex,
        IReadOnlyList<PdfPagePoint> strokePoints,
        PdfAnnotationColor color,
        float borderWidthPoints = 2f,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strokePoints);
        if (strokePoints.Count < 3)
        {
            throw new ArgumentException("Freeform shape requires at least three points.", nameof(strokePoints));
        }

        var closed = strokePoints.ToList();
        var first = closed[0];
        var last = closed[^1];
        if (Math.Abs(first.X - last.X) > 0.5 || Math.Abs(first.Y - last.Y) > 0.5)
        {
            closed.Add(first);
        }

        var created = await AddLabeledInkAsync(
            document,
            pageIndex,
            [closed],
            color,
            borderWidthPoints,
            contents: "Freeform",
            cancellationToken);
        return created with { ShapeKind = PdfShapeKind.Freeform, IsInk = true };
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

        // PDFium exposes GetLine but not SetLine; straight lines/arrows/stars are ink strokes.
        if (kind is PdfShapeKind.Line or PdfShapeKind.Arrow)
        {
            var dx = Math.Abs(bounds.Right - bounds.Left);
            var dy = Math.Abs(bounds.Top - bounds.Bottom);
            if (dx < 1 && dy < 1)
            {
                throw new ArgumentException("Line endpoints must be distinct.", nameof(bounds));
            }

            return kind == PdfShapeKind.Arrow
                ? AddArrowAsInkAsync(document, pageIndex, bounds, borderColor, borderWidthPoints, cancellationToken)
                : AddLineAsInkAsync(document, pageIndex, bounds, borderColor, borderWidthPoints, cancellationToken);
        }

        if (kind == PdfShapeKind.Star)
        {
            if (bounds.Width < 1 || bounds.Height < 1)
            {
                throw new ArgumentException("Star bounds must have positive width and height.", nameof(bounds));
            }

            return AddStarAsInkAsync(document, pageIndex, bounds, borderColor, borderWidthPoints, cancellationToken);
        }

        if (bounds.Width < 1 || bounds.Height < 1)
        {
            throw new ArgumentException("Shape bounds must have positive width and height.", nameof(bounds));
        }

        var subtype = kind switch
        {
            PdfShapeKind.Rectangle or PdfShapeKind.RoundedRectangle or PdfShapeKind.HighlightRectangle
                => PdfiumAnnotSubtypes.Square,
            PdfShapeKind.Ellipse => PdfiumAnnotSubtypes.Circle,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Highlight rectangles always get a translucent yellow-style fill when none provided.
        if (kind == PdfShapeKind.HighlightRectangle && fillColor is null)
        {
            fillColor = new PdfAnnotationColor(
                borderColor.R,
                borderColor.G,
                borderColor.B,
                A: 70);
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

                            // Highlight areas use a nearly transparent border so the fill dominates.
                            var stroke = kind == PdfShapeKind.HighlightRectangle
                                ? new PdfAnnotationColor(borderColor.R, borderColor.G, borderColor.B, A: 40)
                                : borderColor;

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    stroke.R,
                                    stroke.G,
                                    stroke.B,
                                    stroke.A) == 0)
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

                            var radius = 0f;
                            string? contentsLabel = null;
                            if (kind == PdfShapeKind.RoundedRectangle)
                            {
                                radius = (float)Math.Clamp(
                                    Math.Min(bounds.Width, bounds.Height) * 0.2,
                                    4.0,
                                    36.0);
                                contentsLabel = "RoundedRect";
                            }
                            else if (kind == PdfShapeKind.HighlightRectangle)
                            {
                                contentsLabel = "HighlightRect";
                            }

                            if (contentsLabel is not null &&
                                !PdfiumAnnotStrings.SetString(annot, "Contents", contentsLabel))
                            {
                                throw new InvalidOperationException($"Failed to label {kind} shape.");
                            }

                            var width = kind == PdfShapeKind.HighlightRectangle ? 0.5f : borderWidthPoints;
                            if (PdfiumNative.AnnotSetBorder(annot.__Instance, radius, radius, width) == 0)
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
                                Contents: contentsLabel,
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

    public Task<PdfAnnotationInfo> AddTextBoxAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        string contents,
        PdfAnnotationColor textColor,
        PdfAnnotationColor? borderColor = null,
        PdfAnnotationColor? fillColor = null,
        float fontSizePoints = 12f,
        string fontResourceName = "Helv",
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        contents ??= string.Empty;
        if (bounds.Width < 8 || bounds.Height < 8)
        {
            throw new ArgumentException("Text box bounds must be at least 8×8 points.", nameof(bounds));
        }

        if (fontSizePoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSizePoints));
        }

        fontResourceName = string.IsNullOrWhiteSpace(fontResourceName)
            ? "Helv"
            : fontResourceName.Trim().TrimStart('/');
        borderColor ??= new PdfAnnotationColor(40, 40, 40);

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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for text box.");
                    }

                    try
                    {
                        if (fpdf_annot.FPDFAnnotIsSupportedSubtype(PdfiumAnnotSubtypes.FreeText) == 0)
                        {
                            throw new NotSupportedException("PDFium does not support FreeText annotations.");
                        }

                        var annot = fpdf_annot.FPDFPageCreateAnnot(page, PdfiumAnnotSubtypes.FreeText);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed for text box.");
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
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed for text box.");
                            }

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    borderColor.Value.R,
                                    borderColor.Value.G,
                                    borderColor.Value.B,
                                    borderColor.Value.A) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetColor failed for text box border.");
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
                                    throw new InvalidOperationException("FPDFAnnot_SetColor failed for text box fill.");
                                }
                            }

                            if (PdfiumNative.AnnotSetBorder(annot.__Instance, 0, 0, 1f) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetBorder failed for text box.");
                            }

                            if (!PdfiumAnnotStrings.SetString(annot, "Contents", contents))
                            {
                                throw new InvalidOperationException("Failed to set text box Contents.");
                            }

                            // Default appearance: standard font at fontSize in RGB text color.
                            var r = textColor.R / 255.0;
                            var g = textColor.G / 255.0;
                            var b = textColor.B / 255.0;
                            var da = $"/{fontResourceName} {fontSizePoints:0.##} Tf {r:0.###} {g:0.###} {b:0.###} rg";
                            if (!PdfiumAnnotStrings.SetString(annot, "DA", da))
                            {
                                throw new InvalidOperationException("Failed to set text box DA.");
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created text box has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(
                                pageIndex,
                                index,
                                TextMarkupKind: null,
                                bounds,
                                textColor,
                                contents,
                                IsStickyNote: false,
                                IsInk: false,
                                ShapeKind: null,
                                IsTextBox: true);
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

    public async Task<PdfAnnotationInfo> AddCalloutAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect textBounds,
        PdfPagePoint tip,
        string contents,
        PdfAnnotationColor textColor,
        PdfAnnotationColor? borderColor = null,
        PdfAnnotationColor? fillColor = null,
        float fontSizePoints = 12f,
        string fontResourceName = "Helv",
        float pointerWidthPoints = 1.5f,
        CancellationToken cancellationToken = default)
    {
        borderColor ??= new PdfAnnotationColor(40, 40, 40);
        fillColor ??= new PdfAnnotationColor(255, 255, 230);

        // Anchor the pointer on the nearest edge midpoint of the text box.
        var cx = (textBounds.Left + textBounds.Right) / 2;
        var cy = (textBounds.Bottom + textBounds.Top) / 2;
        var candidates = new[]
        {
            new PdfPagePoint(cx, textBounds.Bottom),
            new PdfPagePoint(cx, textBounds.Top),
            new PdfPagePoint(textBounds.Left, cy),
            new PdfPagePoint(textBounds.Right, cy),
        };
        var anchor = candidates
            .OrderBy(p => ((p.X - tip.X) * (p.X - tip.X)) + ((p.Y - tip.Y) * (p.Y - tip.Y)))
            .First();

        var box = await AddTextBoxAsync(
            document,
            pageIndex,
            textBounds,
            contents,
            textColor,
            borderColor,
            fillColor,
            fontSizePoints,
            fontResourceName,
            cancellationToken);

        // Mark as callout via Subj so list/reload can recognize it.
        await MutateAnnotAsync(
            document,
            pageIndex,
            box.AnnotIndex,
            cancellationToken,
            annot =>
            {
                if (!PdfiumAnnotStrings.SetString(annot, "Subj", "Callout"))
                {
                    throw new InvalidOperationException("Failed to set callout Subj.");
                }
            });

        await AddLabeledInkAsync(
            document,
            pageIndex,
            [[tip, anchor]],
            borderColor.Value,
            pointerWidthPoints,
            contents: "CalloutPointer",
            cancellationToken);

        return box with { IsCallout = true, IsTextBox = true };
    }

    public Task<PdfAnnotationInfo> AddStampAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        ReadOnlyMemory<byte> bgraPixels,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            throw new ArgumentException("Stamp bounds must have positive size.", nameof(bounds));
        }

        var expected = checked(pixelWidth * pixelHeight * 4);
        if (bgraPixels.Length < expected)
        {
            throw new ArgumentException(
                $"BGRA buffer length {bgraPixels.Length} is shorter than {expected} bytes.",
                nameof(bgraPixels));
        }

        // Copy so the Task.Run closure owns a stable buffer for pinning.
        var pixels = bgraPixels.Slice(0, expected).ToArray();

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    if (fpdf_annot.FPDFAnnotIsSupportedSubtype(PdfiumAnnotSubtypes.Stamp) == 0)
                    {
                        throw new NotSupportedException("PDFium does not support stamp annotations.");
                    }

                    if (fpdf_annot.FPDFAnnotIsObjectSupportedSubtype(PdfiumAnnotSubtypes.Stamp) == 0)
                    {
                        throw new NotSupportedException("PDFium does not support objects on stamp annotations.");
                    }

                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for stamp.");
                    }

                    try
                    {
                        var annot = fpdf_annot.FPDFPageCreateAnnot(page, PdfiumAnnotSubtypes.Stamp);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed for stamp.");
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
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed for stamp.");
                            }

                            var image = fpdf_edit.FPDFPageObjNewImageObj(pdfium.Handle);
                            if (image is null)
                            {
                                throw new InvalidOperationException("FPDFPageObj_NewImageObj failed.");
                            }

                            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            FpdfBitmapT? bitmap = null;
                            try
                            {
                                var stride = pixelWidth * 4;
                                bitmap = fpdfview.FPDFBitmapCreateEx(
                                    pixelWidth,
                                    pixelHeight,
                                    PdfiumBitmapFormats.Bgra,
                                    handle.AddrOfPinnedObject(),
                                    stride);
                                if (bitmap is null)
                                {
                                    throw new InvalidOperationException("FPDFBitmap_CreateEx failed.");
                                }

                                if (fpdf_edit.FPDFImageObjSetBitmap(page, 1, image, bitmap) == 0)
                                {
                                    throw new InvalidOperationException("FPDFImageObj_SetBitmap failed.");
                                }

                                if (fpdf_edit.FPDFImageObjSetMatrix(
                                        image,
                                        bounds.Width,
                                        0,
                                        0,
                                        bounds.Height,
                                        bounds.Left,
                                        bounds.Bottom) == 0)
                                {
                                    throw new InvalidOperationException("FPDFImageObj_SetMatrix failed.");
                                }

                                if (fpdf_annot.FPDFAnnotAppendObject(annot, image) == 0)
                                {
                                    throw new InvalidOperationException("FPDFAnnot_AppendObject failed for stamp image.");
                                }
                            }
                            finally
                            {
                                if (bitmap is not null)
                                {
                                    fpdfview.FPDFBitmapDestroy(bitmap);
                                }

                                if (handle.IsAllocated)
                                {
                                    handle.Free();
                                }
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created stamp has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(
                                pageIndex,
                                index,
                                TextMarkupKind: null,
                                bounds,
                                Color: null,
                                Contents: null,
                                IsStickyNote: false,
                                IsInk: false,
                                ShapeKind: null,
                                IsTextBox: false,
                                IsStamp: true);
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

    public Task SetOpacityAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        float opacity,
        CancellationToken cancellationToken = default)
    {
        if (opacity is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        return MutateAnnotAsync(
            document,
            pageIndex,
            annotIndex,
            cancellationToken,
            annot =>
            {
                uint r = 0, g = 0, b = 0, a = 255;
                if (fpdf_annot.FPDFAnnotGetColor(
                        annot,
                        FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                        ref r,
                        ref g,
                        ref b,
                        ref a) == 0)
                {
                    r = 30;
                    g = 144;
                    b = 255;
                }

                var alpha = (uint)Math.Clamp((int)Math.Round(opacity * 255f), 0, 255);
                if (fpdf_annot.FPDFAnnotSetColor(
                        annot,
                        FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                        r,
                        g,
                        b,
                        alpha) == 0)
                {
                    throw new InvalidOperationException("Failed to set annotation opacity.");
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

    public async Task<PdfAnnotationInfo> DuplicateAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(annotIndex);
        const double offset = 12.0;

        cancellationToken.ThrowIfCancellationRequested();
        PdfiumLibrary.EnsureInitialized();

        int subtype;
        PdfRect bounds;
        PdfAnnotationColor color = new(0, 0, 0);
        string contents = string.Empty;
        string? author = null;
        PdfTextMarkupKind? markupKind = null;
        PdfShapeKind? shapeKind = null;
        List<PdfQuad> quads = [];
        List<List<PdfPagePoint>> inkStrokes = [];
        byte[]? stampPixels = null;
        var stampPixelWidth = 0;
        var stampPixelHeight = 0;

        lock (PdfiumSync.Gate)
        {
            pdfium.ThrowIfDisposed();
            var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
            if (page is null)
            {
                throw new InvalidOperationException($"Failed to load page {pageIndex} for duplicate.");
            }

            try
            {
                var annot = fpdf_annot.FPDFPageGetAnnot(page, annotIndex);
                if (annot is null)
                {
                    throw new ArgumentOutOfRangeException(nameof(annotIndex), "Annotation not found.");
                }

                try
                {
                    subtype = fpdf_annot.FPDFAnnotGetSubtype(annot);

                    using var rect = new FS_RECTF_();
                    if (fpdf_annot.FPDFAnnotGetRect(annot, rect) == 0)
                    {
                        throw new InvalidOperationException("Failed to read annotation bounds.");
                    }

                    bounds = new PdfRect(
                        rect.Left + offset,
                        rect.Bottom - offset,
                        rect.Right + offset,
                        rect.Top - offset);

                    if (subtype == PdfiumAnnotSubtypes.Stamp)
                    {
                        stampPixels = TryExtractStampBgra(annot, out stampPixelWidth, out stampPixelHeight);
                        if (stampPixels is null || stampPixelWidth <= 0 || stampPixelHeight <= 0)
                        {
                            throw new InvalidOperationException(
                                "Failed to extract stamp image pixels for duplicate.");
                        }
                    }
                    else
                    {
                    uint r = 0, g = 0, b = 0, a = 255;
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

                    contents = PdfiumAnnotStrings.GetString(annot, "Contents");
                    author = PdfiumAnnotStrings.GetString(annot, "T");
                    if (string.IsNullOrWhiteSpace(author))
                    {
                        author = null;
                    }

                    markupKind = FromSubtype(subtype);
                    shapeKind = FromShapeSubtype(subtype);
                    if (shapeKind == PdfShapeKind.Rectangle)
                    {
                        if (string.Equals(contents, "HighlightRect", StringComparison.Ordinal))
                        {
                            shapeKind = PdfShapeKind.HighlightRectangle;
                        }
                        else if (string.Equals(contents, "RoundedRect", StringComparison.Ordinal)
                                 || (PdfiumNative.AnnotGetBorder(
                                         annot.__Instance,
                                         out var hr,
                                         out var vr,
                                         out _) != 0
                                     && (hr > 0.5f || vr > 0.5f)))
                        {
                            shapeKind = PdfShapeKind.RoundedRectangle;
                        }
                    }

                    if (shapeKind is null && subtype == PdfiumAnnotSubtypes.Ink)
                    {
                        shapeKind = FromInkShapeContents(contents);
                    }

                    if (markupKind is not null)
                    {
                        var count = fpdf_annot.FPDFAnnotCountAttachmentPoints(annot);
                        for (ulong i = 0; i < count; i++)
                        {
                            using var quad = new FS_QUADPOINTSF();
                            if (fpdf_annot.FPDFAnnotGetAttachmentPoints(annot, i, quad) == 0)
                            {
                                continue;
                            }

                            quads.Add(new PdfQuad(
                                quad.X1 + offset,
                                quad.Y1 - offset,
                                quad.X2 + offset,
                                quad.Y2 - offset,
                                quad.X3 + offset,
                                quad.Y3 - offset,
                                quad.X4 + offset,
                                quad.Y4 - offset));
                        }
                    }

                    if (subtype == PdfiumAnnotSubtypes.Ink)
                    {
                        var strokeCount = (uint)PdfiumNative.AnnotGetInkListCount(annot.__Instance);
                        for (uint s = 0; s < strokeCount; s++)
                        {
                            var needed = PdfiumNative.AnnotGetInkListPath(annot.__Instance, s, IntPtr.Zero, 0);
                            if (needed == 0)
                            {
                                continue;
                            }

                            var buffer = new PdfiumNative.FsPointF[needed];
                            var handle = System.Runtime.InteropServices.GCHandle.Alloc(
                                buffer,
                                System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                var written = PdfiumNative.AnnotGetInkListPath(
                                    annot.__Instance,
                                    s,
                                    handle.AddrOfPinnedObject(),
                                    needed);
                                if (written == 0)
                                {
                                    continue;
                                }

                                var stroke = new List<PdfPagePoint>((int)written);
                                for (var i = 0; i < (int)written; i++)
                                {
                                    stroke.Add(new PdfPagePoint(
                                        buffer[i].X + offset,
                                        buffer[i].Y - offset));
                                }

                                if (stroke.Count >= 2)
                                {
                                    inkStrokes.Add(stroke);
                                }
                            }
                            finally
                            {
                                handle.Free();
                            }
                        }
                    }
                    } // end non-stamp clone extract
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

        if (stampPixels is not null)
        {
            return await AddStampAsync(
                document,
                pageIndex,
                bounds,
                stampPixels,
                stampPixelWidth,
                stampPixelHeight,
                cancellationToken);
        }

        if (markupKind is { } mk)
        {
            if (quads.Count == 0)
            {
                quads.Add(PdfQuad.FromRect(bounds));
            }

            return await AddTextMarkupAsync(document, pageIndex, mk, quads, color, cancellationToken);
        }

        if (subtype == PdfiumAnnotSubtypes.Text)
        {
            return await AddStickyNoteAsync(
                document,
                pageIndex,
                bounds.Left,
                bounds.Bottom,
                contents,
                color,
                author: author,
                cancellationToken: cancellationToken);
        }

        if (subtype == PdfiumAnnotSubtypes.FreeText)
        {
            return await AddTextBoxAsync(
                document,
                pageIndex,
                bounds,
                contents,
                color,
                borderColor: color,
                cancellationToken: cancellationToken);
        }

        if (subtype is PdfiumAnnotSubtypes.Square or PdfiumAnnotSubtypes.Circle)
        {
            var kind = shapeKind
                ?? (subtype == PdfiumAnnotSubtypes.Square
                    ? PdfShapeKind.Rectangle
                    : PdfShapeKind.Ellipse);
            return await AddShapeAsync(
                document,
                pageIndex,
                kind,
                bounds,
                color,
                fillColor: null,
                cancellationToken: cancellationToken);
        }

        if (subtype == PdfiumAnnotSubtypes.Ink)
        {
            if (inkStrokes.Count == 0)
            {
                throw new InvalidOperationException("Ink annotation has no strokes to duplicate.");
            }

            if (shapeKind is PdfShapeKind.Line or PdfShapeKind.Arrow)
            {
                return await AddShapeAsync(
                    document,
                    pageIndex,
                    shapeKind.Value,
                    bounds,
                    color,
                    cancellationToken: cancellationToken);
            }

            var flat = inkStrokes.SelectMany(s => s).ToList();
            if (flat.Count < 2)
            {
                flat = inkStrokes[0];
            }

            return await AddInkAsync(document, pageIndex, flat, color, cancellationToken: cancellationToken);
        }

        throw new NotSupportedException($"Duplicating annotation subtype {subtype} is not supported yet.");
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

    public Task<PdfFlattenResult> FlattenAsync(
        IPdfDocument document,
        IReadOnlyList<int>? pageIndexes = null,
        bool forPrint = false,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        var indexes = pageIndexes is null
            ? Enumerable.Range(0, pdfium.PageCount).ToList()
            : pageIndexes.Distinct().OrderBy(i => i).ToList();

        foreach (var index in indexes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, pdfium.PageCount);
        }

        var flag = forPrint ? PdfiumFlattenFlags.FlatPrint : PdfiumFlattenFlags.FlatNormalDisplay;

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var processed = 0;
                    var changed = 0;
                    var failed = 0;

                    foreach (var pageIndex in indexes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                        if (page is null)
                        {
                            failed++;
                            continue;
                        }

                        try
                        {
                            processed++;
                            var result = fpdf_flatten.FPDFPageFlatten(page, flag);
                            if (result == PdfiumFlattenFlags.FlattenFail)
                            {
                                failed++;
                            }
                            else if (result == PdfiumFlattenFlags.FlattenSuccess)
                            {
                                changed++;
                            }
                        }
                        finally
                        {
                            fpdfview.FPDF_ClosePage(page);
                        }
                    }

                    if (changed > 0 || failed > 0)
                    {
                        pdfium.NotifyAnnotationsChanged();
                    }

                    return new PdfFlattenResult(processed, changed, failed);
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
                    var author = PdfiumAnnotStrings.GetString(annot, "T");
                    if (string.IsNullOrWhiteSpace(author))
                    {
                        author = null;
                    }

                    var isSticky = subtype == PdfiumAnnotSubtypes.Text;
                    var isInk = subtype == PdfiumAnnotSubtypes.Ink;
                    var shapeKind = FromShapeSubtype(subtype);
                    if (shapeKind == PdfShapeKind.Rectangle)
                    {
                        if (string.Equals(contents, "HighlightRect", StringComparison.Ordinal))
                        {
                            shapeKind = PdfShapeKind.HighlightRectangle;
                        }
                        else if (string.Equals(contents, "RoundedRect", StringComparison.Ordinal))
                        {
                            shapeKind = PdfShapeKind.RoundedRectangle;
                        }
                        else if (PdfiumNative.AnnotGetBorder(
                                     annot.__Instance,
                                     out var hr,
                                     out var vr,
                                     out _) != 0
                                 && (hr > 0.5f || vr > 0.5f))
                        {
                            shapeKind = PdfShapeKind.RoundedRectangle;
                        }
                    }

                    if (shapeKind is null && isInk)
                    {
                        shapeKind = FromInkShapeContents(contents);
                    }

                    var isTextBox = subtype == PdfiumAnnotSubtypes.FreeText;
                    var isStamp = subtype == PdfiumAnnotSubtypes.Stamp;
                    var isCallout = isTextBox
                        && string.Equals(
                            PdfiumAnnotStrings.GetString(annot, "Subj"),
                            "Callout",
                            StringComparison.Ordinal);
                    results.Add(new PdfAnnotationInfo(
                        pageIndex,
                        i,
                        kind,
                        bounds,
                        color,
                        contents,
                        isSticky,
                        isInk,
                        shapeKind,
                        isTextBox,
                        isStamp,
                        isCallout,
                        author));
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
        var created = await AddLabeledInkAsync(
            document,
            pageIndex,
            [
                [new PdfPagePoint(bounds.Left, bounds.Bottom), new PdfPagePoint(bounds.Right, bounds.Top)],
            ],
            borderColor,
            borderWidthPoints,
            contents: "Line",
            cancellationToken);
        return created with { ShapeKind = PdfShapeKind.Line, IsInk = true };
    }

    private async Task<PdfAnnotationInfo> AddStarAsInkAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        PdfAnnotationColor borderColor,
        float borderWidthPoints,
        CancellationToken cancellationToken)
    {
        var points = PdfStarGeometry.BuildPoints(bounds);
        var created = await AddLabeledInkAsync(
            document,
            pageIndex,
            [points],
            borderColor,
            borderWidthPoints,
            contents: "Star",
            cancellationToken);
        return created with { ShapeKind = PdfShapeKind.Star, IsInk = true };
    }

    private async Task<PdfAnnotationInfo> AddArrowAsInkAsync(
        IPdfDocument document,
        int pageIndex,
        PdfRect bounds,
        PdfAnnotationColor borderColor,
        float borderWidthPoints,
        CancellationToken cancellationToken)
    {
        var start = new PdfPagePoint(bounds.Left, bounds.Bottom);
        var end = new PdfPagePoint(bounds.Right, bounds.Top);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var ux = dx / length;
        var uy = dy / length;
        var head = Math.Clamp(length * 0.22, 8.0, 28.0);
        const double wingRadians = Math.PI / 7; // ~25.7°
        var cos = Math.Cos(wingRadians);
        var sin = Math.Sin(wingRadians);
        // Wing tips: from tip back along shaft, rotated ±wing.
        var backX = -ux * head;
        var backY = -uy * head;
        var wing1 = new PdfPagePoint(
            end.X + (backX * cos) - (backY * sin),
            end.Y + (backX * sin) + (backY * cos));
        var wing2 = new PdfPagePoint(
            end.X + (backX * cos) + (backY * sin),
            end.Y + (-backX * sin) + (backY * cos));

        var created = await AddLabeledInkAsync(
            document,
            pageIndex,
            [
                [start, end],
                [end, wing1],
                [end, wing2],
            ],
            borderColor,
            borderWidthPoints,
            contents: "Arrow",
            cancellationToken);
        return created with { ShapeKind = PdfShapeKind.Arrow, IsInk = true };
    }

    private Task<PdfAnnotationInfo> AddLabeledInkAsync(
        IPdfDocument document,
        int pageIndex,
        IReadOnlyList<IReadOnlyList<PdfPagePoint>> strokes,
        PdfAnnotationColor color,
        float borderWidthPoints,
        string contents,
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
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for ink shape.");
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
                            throw new InvalidOperationException("FPDFPage_CreateAnnot failed for ink shape.");
                        }

                        try
                        {
                            var allPoints = strokes.SelectMany(s => s).ToList();
                            var minX = allPoints.Min(p => p.X);
                            var minY = allPoints.Min(p => p.Y);
                            var maxX = allPoints.Max(p => p.X);
                            var maxY = allPoints.Max(p => p.Y);
                            var pad = Math.Max(borderWidthPoints, 4f);
                            var bounds = new PdfRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
                            using var rect = new FS_RECTF_();
                            rect.Left = (float)bounds.Left;
                            rect.Bottom = (float)bounds.Bottom;
                            rect.Right = (float)bounds.Right;
                            rect.Top = (float)bounds.Top;
                            if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetRect failed for ink shape.");
                            }

                            if (fpdf_annot.FPDFAnnotSetColor(
                                    annot,
                                    FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color,
                                    color.R,
                                    color.G,
                                    color.B,
                                    color.A) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetColor failed for ink shape.");
                            }

                            if (PdfiumNative.AnnotSetBorder(annot.__Instance, 0, 0, borderWidthPoints) == 0)
                            {
                                throw new InvalidOperationException("FPDFAnnot_SetBorder failed for ink shape.");
                            }

                            if (!PdfiumAnnotStrings.SetString(annot, "Contents", contents))
                            {
                                throw new InvalidOperationException("Failed to set ink shape Contents.");
                            }

                            foreach (var stroke in strokes)
                            {
                                var points = stroke
                                    .Select(p => new PdfiumNative.FsPointF { X = (float)p.X, Y = (float)p.Y })
                                    .ToArray();
                                if (PdfiumNative.AnnotAddInkStroke(annot.__Instance, points, (ulong)points.Length) < 0)
                                {
                                    throw new InvalidOperationException("FPDFAnnot_AddInkStroke failed for ink shape.");
                                }
                            }

                            var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                            if (index < 0)
                            {
                                throw new InvalidOperationException("Created ink shape has no page index.");
                            }

                            pdfium.NotifyAnnotationsChanged();
                            return new PdfAnnotationInfo(
                                pageIndex,
                                index,
                                TextMarkupKind: null,
                                bounds,
                                color,
                                Contents: contents,
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

    private const int PageObjImage = 3;

    private static byte[]? TryExtractStampBgra(
        FpdfAnnotationT annot,
        out int pixelWidth,
        out int pixelHeight)
    {
        pixelWidth = 0;
        pixelHeight = 0;
        var count = fpdf_annot.FPDFAnnotGetObjectCount(annot);
        for (var i = 0; i < count; i++)
        {
            var obj = fpdf_annot.FPDFAnnotGetObject(annot, i);
            if (obj is null || fpdf_edit.FPDFPageObjGetType(obj) != PageObjImage)
            {
                continue;
            }

            var bmp = fpdf_edit.FPDFImageObjGetBitmap(obj);
            if (bmp is null)
            {
                continue;
            }

            try
            {
                var width = fpdfview.FPDFBitmapGetWidth(bmp);
                var height = fpdfview.FPDFBitmapGetHeight(bmp);
                var stride = fpdfview.FPDFBitmapGetStride(bmp);
                var format = fpdfview.FPDFBitmapGetFormat(bmp);
                var buffer = fpdfview.FPDFBitmapGetBuffer(bmp);
                if (buffer == IntPtr.Zero || width <= 0 || height <= 0 || stride <= 0)
                {
                    continue;
                }

                var src = new byte[stride * height];
                Marshal.Copy(buffer, src, 0, src.Length);
                pixelWidth = width;
                pixelHeight = height;
                return StampBitmapToBgra(src, width, height, stride, format);
            }
            finally
            {
                fpdfview.FPDFBitmapDestroy(bmp);
            }
        }

        return null;
    }

    private static byte[] StampBitmapToBgra(byte[] src, int width, int height, int stride, int format)
    {
        var dst = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var srcRow = y * stride;
            var dstRow = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var di = dstRow + (x * 4);
                switch (format)
                {
                    case PdfiumBitmapFormats.Bgra:
                    case PdfiumBitmapFormats.Bgrx:
                        {
                            var si = srcRow + (x * 4);
                            dst[di] = src[si];
                            dst[di + 1] = src[si + 1];
                            dst[di + 2] = src[si + 2];
                            dst[di + 3] = format == PdfiumBitmapFormats.Bgra ? src[si + 3] : (byte)255;
                            break;
                        }

                    case PdfiumBitmapFormats.Bgr:
                        {
                            var si = srcRow + (x * 3);
                            dst[di] = src[si];
                            dst[di + 1] = src[si + 1];
                            dst[di + 2] = src[si + 2];
                            dst[di + 3] = 255;
                            break;
                        }

                    case PdfiumBitmapFormats.Gray:
                        {
                            var g = src[srcRow + x];
                            dst[di] = g;
                            dst[di + 1] = g;
                            dst[di + 2] = g;
                            dst[di + 3] = 255;
                            break;
                        }

                    default:
                        dst[di] = dst[di + 1] = dst[di + 2] = 0;
                        dst[di + 3] = 255;
                        break;
                }
            }
        }

        return dst;
    }

    private static PdfShapeKind? FromShapeSubtype(int subtype) =>
        subtype switch
        {
            PdfiumAnnotSubtypes.Square => PdfShapeKind.Rectangle,
            PdfiumAnnotSubtypes.Circle => PdfShapeKind.Ellipse,
            PdfiumAnnotSubtypes.Line => PdfShapeKind.Line,
            _ => null,
        };

    private static PdfShapeKind? FromInkShapeContents(string? contents) =>
        contents switch
        {
            "Line" => PdfShapeKind.Line,
            "Arrow" => PdfShapeKind.Arrow,
            "Freeform" => PdfShapeKind.Freeform,
            "Star" => PdfShapeKind.Star,
            _ => null,
        };

    private static string FormatPdfDate(DateTimeOffset value) =>
        "D:" + value.ToString("yyyyMMddHHmmss");

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
