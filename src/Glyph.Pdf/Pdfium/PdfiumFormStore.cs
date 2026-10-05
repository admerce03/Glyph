using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumFormStore : IPdfFormStore
{
    public Task<bool> HasFormAsync(IPdfDocument document, CancellationToken cancellationToken = default)
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
                    var type = fpdf_formfill.FPDF_GetFormType(pdfium.Handle);
                    return type is PdfiumFormTypes.AcroForm
                        or PdfiumFormTypes.XfaFull
                        or PdfiumFormTypes.XfaForeground;
                }
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PdfFormFieldInfo>> ListFieldsAsync(
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
                    // Prefer annotation dictionary reads over FPDFDOC_InitFormFillEnvironment:
                    // ExitFormFillEnvironment SEGV'd on Windows CI with stub FPDF_FORMFILLINFO callbacks.
                    var fields = new List<PdfFormFieldInfo>();
                    var tab = 0;
                    for (var pageIndex = 0; pageIndex < pdfium.PageCount; pageIndex++)
                    {
                        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                        if (page is null)
                        {
                            continue;
                        }

                        try
                        {
                            var count = fpdf_annot.FPDFPageGetAnnotCount(page);
                            for (var annotIndex = 0; annotIndex < count; annotIndex++)
                            {
                                var annot = fpdf_annot.FPDFPageGetAnnot(page, annotIndex);
                                if (annot is null)
                                {
                                    continue;
                                }

                                try
                                {
                                    if (fpdf_annot.FPDFAnnotGetSubtype(annot) != PdfiumAnnotSubtypes.Widget)
                                    {
                                        continue;
                                    }

                                    var kind = MapKindFromFt(PdfiumAnnotStrings.GetString(annot, "FT"));
                                    var name = PdfiumAnnotStrings.GetString(annot, "T");
                                    var value = PdfiumAnnotStrings.GetString(annot, "V");
                                    var bounds = ReadRect(annot);
                                    fields.Add(new PdfFormFieldInfo(
                                        pageIndex,
                                        annotIndex,
                                        string.IsNullOrWhiteSpace(name) ? $"Field{tab + 1}" : name,
                                        kind,
                                        value,
                                        bounds,
                                        tab));
                                    tab++;
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

                    return (IReadOnlyList<PdfFormFieldInfo>)fields;
                }
            },
            cancellationToken);
    }

    public Task SetTextValueAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        string value,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, pdfium.PageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(annotIndex);
        value ??= string.Empty;

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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for form fill.");
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
                            if (fpdf_annot.FPDFAnnotGetSubtype(annot) != PdfiumAnnotSubtypes.Widget)
                            {
                                throw new InvalidOperationException("Target annotation is not a form widget.");
                            }

                            var kind = MapKindFromFt(PdfiumAnnotStrings.GetString(annot, "FT"));
                            if (kind is not (PdfFormFieldKind.TextField or PdfFormFieldKind.ComboBox))
                            {
                                throw new NotSupportedException(
                                    $"Setting values for form field kind {kind} is not supported yet.");
                            }

                            // Durable fill path: write /V (NeedAppearances regenerates appearance in viewers).
                            if (!PdfiumAnnotStrings.SetString(annot, "V", value))
                            {
                                throw new InvalidOperationException("Failed to set form field /V value.");
                            }

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

    public async Task<PdfFormFieldInfo?> FocusAdjacentAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        bool forward,
        CancellationToken cancellationToken = default)
    {
        var fields = await ListFieldsAsync(document, cancellationToken);
        if (fields.Count == 0)
        {
            return null;
        }

        var current = -1;
        for (var i = 0; i < fields.Count; i++)
        {
            if (fields[i].PageIndex == pageIndex && fields[i].AnnotIndex == annotIndex)
            {
                current = i;
                break;
            }
        }

        if (current < 0)
        {
            return forward ? fields[0] : fields[^1];
        }

        var next = forward
            ? (current + 1) % fields.Count
            : (current - 1 + fields.Count) % fields.Count;
        return fields[next];
    }

    private static PdfFormFieldKind MapKindFromFt(string ft) => ft switch
    {
        "Tx" => PdfFormFieldKind.TextField,
        "Btn" => PdfFormFieldKind.CheckBox, // radio/push distinguished later via Ff
        "Ch" => PdfFormFieldKind.ComboBox,  // list vs combo distinguished later via Ff
        "Sig" => PdfFormFieldKind.Signature,
        _ => PdfFormFieldKind.Unknown,
    };

    private static PdfRect ReadRect(FpdfAnnotationT annot)
    {
        using var rect = new FS_RECTF_();
        if (fpdf_annot.FPDFAnnotGetRect(annot, rect) == 0)
        {
            return new PdfRect(0, 0, 0, 0);
        }

        return new PdfRect(rect.Left, rect.Bottom, rect.Right, rect.Top);
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
