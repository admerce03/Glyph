using System.Text;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Annotations;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumAnnotationStore : IPdfAnnotationStore
{
    public Task<IReadOnlyList<PdfAnnotation>> ListAsync(
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
                    var results = new List<PdfAnnotation>();
                    for (var pageIndex = 0; pageIndex < pdfium.PageCount; pageIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        results.AddRange(ReadPageAnnotations(pdfium, pageIndex));
                    }

                    return (IReadOnlyList<PdfAnnotation>)results;
                }
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PdfAnnotation>> ListPageAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default)
    {
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
                    return (IReadOnlyList<PdfAnnotation>)ReadPageAnnotations(pdfium, pageIndex);
                }
            },
            cancellationToken);
    }

    public Task<PdfAnnotation> AddTextMarkupAsync(
        IPdfDocument document,
        PdfTextMarkupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind is not (PdfAnnotationKind.Highlight or PdfAnnotationKind.Underline or PdfAnnotationKind.StrikeOut or PdfAnnotationKind.Squiggly))
        {
            throw new ArgumentException("Kind must be a text-markup subtype.", nameof(request));
        }

        if (request.Quads.Count == 0)
        {
            throw new ArgumentException("At least one quad is required.", nameof(request));
        }

        var pdfium = RequirePdfium(document);
        ValidatePage(pdfium, request.PageIndex);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, request.PageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {request.PageIndex}.");
                    }

                    FpdfAnnotationT? annot = null;
                    try
                    {
                        annot = fpdf_annot.FPDFPageCreateAnnot(page, (int)request.Kind);
                        if (annot is null)
                        {
                            throw new InvalidOperationException($"Failed to create {request.Kind} annotation.");
                        }

                        var bounds = PdfAnnotationQuads.BoundsFromQuads(request.Quads);
                        SetRect(annot, bounds);
                        SetColor(annot, request.Color);

                        // SetAttachmentPoints only updates existing indices; new quads must be Append'ed.
                        for (var i = 0; i < request.Quads.Count; i++)
                        {
                            using var quad = ToNativeQuad(request.Quads[i]);
                            if (fpdf_annot.FPDFAnnotAppendAttachmentPoints(annot, quad) == 0)
                            {
                                throw new InvalidOperationException("Failed to append annotation attachment points.");
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(request.SelectedText))
                        {
                            SetString(annot, "Contents", request.SelectedText);
                        }

                        if (!string.IsNullOrWhiteSpace(request.Author))
                        {
                            SetString(annot, "T", request.Author);
                        }

                        var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                        return ReadAnnotation(pdfium, request.PageIndex, page, index, annot);
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

    public Task<PdfAnnotation> AddStickyNoteAsync(
        IPdfDocument document,
        PdfStickyNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pdfium = RequirePdfium(document);
        ValidatePage(pdfium, request.PageIndex);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, request.PageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {request.PageIndex}.");
                    }

                    FpdfAnnotationT? annot = null;
                    try
                    {
                        annot = fpdf_annot.FPDFPageCreateAnnot(page, (int)PdfAnnotationKind.Text);
                        if (annot is null)
                        {
                            throw new InvalidOperationException("Failed to create sticky note annotation.");
                        }

                        // Icon-sized rect anchored at the click point.
                        var bounds = new PdfRect(request.X, request.Y - 20, request.X + 20, request.Y);
                        SetRect(annot, bounds);
                        SetColor(annot, request.Color);
                        SetString(annot, "Contents", request.Contents ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(request.Author))
                        {
                            SetString(annot, "T", request.Author);
                        }

                        var index = fpdf_annot.FPDFPageGetAnnotIndex(page, annot);
                        return ReadAnnotation(pdfium, request.PageIndex, page, index, annot);
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

    public Task SetColorAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        PdfAnnotationColor color,
        CancellationToken cancellationToken = default)
    {
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
                    WithAnnot(pdfium, pageIndex, annotIndex, annot => SetColor(annot, color));
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
        ArgumentNullException.ThrowIfNull(contents);
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
                    WithAnnot(pdfium, pageIndex, annotIndex, annot => SetString(annot, "Contents", contents));
                }
            },
            cancellationToken);
    }

    public Task DeleteAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        CancellationToken cancellationToken = default)
    {
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

                    try
                    {
                        var count = fpdf_annot.FPDFPageGetAnnotCount(page);
                        if (annotIndex < 0 || annotIndex >= count)
                        {
                            throw new ArgumentOutOfRangeException(nameof(annotIndex));
                        }

                        if (fpdf_annot.FPDFPageRemoveAnnot(page, annotIndex) == 0)
                        {
                            throw new InvalidOperationException($"Failed to remove annotation {annotIndex}.");
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

    private static List<PdfAnnotation> ReadPageAnnotations(PdfiumDocument pdfium, int pageIndex)
    {
        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
        if (page is null)
        {
            return [];
        }

        try
        {
            var count = fpdf_annot.FPDFPageGetAnnotCount(page);
            var list = new List<PdfAnnotation>(count);
            for (var i = 0; i < count; i++)
            {
                var annot = fpdf_annot.FPDFPageGetAnnot(page, i);
                if (annot is null)
                {
                    continue;
                }

                try
                {
                    list.Add(ReadAnnotation(pdfium, pageIndex, page, i, annot));
                }
                finally
                {
                    fpdf_annot.FPDFPageCloseAnnot(annot);
                }
            }

            return list;
        }
        finally
        {
            fpdfview.FPDF_ClosePage(page);
        }
    }

    private static PdfAnnotation ReadAnnotation(
        PdfiumDocument pdfium,
        int pageIndex,
        FpdfPageT page,
        int annotIndex,
        FpdfAnnotationT annot)
    {
        var subtype = fpdf_annot.FPDFAnnotGetSubtype(annot);
        var kind = Enum.IsDefined(typeof(PdfAnnotationKind), subtype)
            ? (PdfAnnotationKind)subtype
            : PdfAnnotationKind.Unknown;

        using var rect = new FS_RECTF_();
        fpdf_annot.FPDFAnnotGetRect(annot, rect);
        // PDFium FS_RECTF uses top/bottom in PDF space (top >= bottom for upright pages).
        var bounds = new PdfRect(rect.Left, Math.Min(rect.Bottom, rect.Top), rect.Right, Math.Max(rect.Bottom, rect.Top));

        uint r = 0, g = 0, b = 0, a = 255;
        fpdf_annot.FPDFAnnotGetColor(annot, FPDFANNOT_COLORTYPE.FPDFANNOT_COLORTYPE_Color, ref r, ref g, ref b, ref a);
        var color = new PdfAnnotationColor((byte)r, (byte)g, (byte)b, (byte)a);

        var contents = GetString(annot, "Contents");
        var author = GetString(annot, "T");
        var quads = ReadQuads(annot);
        var id = $"{pdfium.Path ?? "mem"}:{pageIndex}:{annotIndex}:{kind}";

        return new PdfAnnotation(
            id,
            pageIndex,
            annotIndex,
            kind,
            bounds,
            color,
            string.IsNullOrEmpty(contents) ? null : contents,
            string.IsNullOrEmpty(author) ? null : author,
            quads,
            SelectedText: string.IsNullOrEmpty(contents) ? null : contents);
    }

    private static IReadOnlyList<PdfQuad> ReadQuads(FpdfAnnotationT annot)
    {
        var count = fpdf_annot.FPDFAnnotCountAttachmentPoints(annot);
        if (count <= 0)
        {
            return [];
        }

        var quads = new List<PdfQuad>((int)count);
        for (ulong i = 0; i < count; i++)
        {
            using var quad = new FS_QUADPOINTSF();
            if (fpdf_annot.FPDFAnnotGetAttachmentPoints(annot, i, quad) == 0)
            {
                continue;
            }

            quads.Add(new PdfQuad(quad.X1, quad.Y1, quad.X2, quad.Y2, quad.X3, quad.Y3, quad.X4, quad.Y4));
        }

        return quads;
    }

    private static void WithAnnot(PdfiumDocument pdfium, int pageIndex, int annotIndex, Action<FpdfAnnotationT> action)
    {
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
                action(annot);
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

    private static void SetRect(FpdfAnnotationT annot, PdfRect bounds)
    {
        using var rect = new FS_RECTF_();
        rect.Left = (float)bounds.Left;
        rect.Bottom = (float)bounds.Bottom;
        rect.Right = (float)bounds.Right;
        rect.Top = (float)bounds.Top;
        if (fpdf_annot.FPDFAnnotSetRect(annot, rect) == 0)
        {
            throw new InvalidOperationException("Failed to set annotation rectangle.");
        }
    }

    private static void SetColor(FpdfAnnotationT annot, PdfAnnotationColor color)
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
    }

    private static FS_QUADPOINTSF ToNativeQuad(PdfQuad quad)
    {
        var native = new FS_QUADPOINTSF
        {
            X1 = (float)quad.X1,
            Y1 = (float)quad.Y1,
            X2 = (float)quad.X2,
            Y2 = (float)quad.Y2,
            X3 = (float)quad.X3,
            Y3 = (float)quad.Y3,
            X4 = (float)quad.X4,
            Y4 = (float)quad.Y4,
        };
        return native;
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

    private static string GetString(FpdfAnnotationT annot, string key)
    {
        var needed = fpdf_annot.FPDFAnnotGetStringValue(annot, key, ref UnsafeDummy, 0);
        if (needed <= 2)
        {
            return string.Empty;
        }

        var buffer = new ushort[needed / 2];
        fpdf_annot.FPDFAnnotGetStringValue(annot, key, ref buffer[0], needed);
        return ushortsToString(buffer);
    }

    private static ushort UnsafeDummy;

    private static string ushortsToString(ushort[] buffer)
    {
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

        return new string(chars, 0, len);
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

    private static void ValidatePage(PdfiumDocument document, int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= document.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }
    }
}
