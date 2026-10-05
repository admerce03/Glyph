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

                                    var kind = MapKind(annot);
                                    var name = PdfiumAnnotStrings.GetString(annot, "T");
                                    var value = PdfiumAnnotStrings.GetString(annot, "V");
                                    if (string.IsNullOrEmpty(value) && kind == PdfFormFieldKind.CheckBox)
                                    {
                                        value = PdfiumAnnotStrings.GetString(annot, "AS");
                                    }
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

                            var kind = MapKind(annot);
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

    public Task SetCheckBoxAsync(
        IPdfDocument document,
        int pageIndex,
        int annotIndex,
        bool isChecked,
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
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for checkbox.");
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

                            if (MapKind(annot) != PdfFormFieldKind.CheckBox)
                            {
                                throw new NotSupportedException("Target annotation is not a checkbox.");
                            }

                            var onState = ResolveCheckBoxOnState(annot);
                            var state = isChecked ? onState : "Off";
                            if (!PdfiumAnnotStrings.SetString(annot, "V", state) ||
                                !PdfiumAnnotStrings.SetString(annot, "AS", state))
                            {
                                throw new InvalidOperationException("Failed to set checkbox /V and /AS.");
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

    private static PdfFormFieldKind MapKind(FpdfAnnotationT annot)
    {
        var ft = PdfiumAnnotStrings.GetString(annot, "FT");
        if (ft == "Tx")
        {
            return PdfFormFieldKind.TextField;
        }

        if (ft == "Sig")
        {
            return PdfFormFieldKind.Signature;
        }

        if (ft == "Ch")
        {
            // Bit 17 (131072) = combo; otherwise list box.
            return (ReadFf(annot) & 131072) != 0
                ? PdfFormFieldKind.ComboBox
                : PdfFormFieldKind.ListBox;
        }

        if (ft == "Btn")
        {
            var ff = ReadFf(annot);
            if ((ff & 65536) != 0) // pushbutton
            {
                return PdfFormFieldKind.PushButton;
            }

            if ((ff & 32768) != 0) // radio
            {
                return PdfFormFieldKind.RadioButton;
            }

            return PdfFormFieldKind.CheckBox;
        }

        return PdfFormFieldKind.Unknown;
    }

    private static int ReadFf(FpdfAnnotationT annot)
    {
        if (fpdf_annot.FPDFAnnotHasKey(annot, "Ff") == 0)
        {
            return 0;
        }

        float value = 0;
        return fpdf_annot.FPDFAnnotGetNumberValue(annot, "Ff", ref value) == 0
            ? 0
            : (int)value;
    }

    private static string ResolveCheckBoxOnState(FpdfAnnotationT annot)
    {
        var current = PdfiumAnnotStrings.GetString(annot, "V");
        if (!string.IsNullOrEmpty(current) &&
            !string.Equals(current, "Off", StringComparison.Ordinal))
        {
            return current;
        }

        var appearance = PdfiumAnnotStrings.GetString(annot, "AS");
        if (!string.IsNullOrEmpty(appearance) &&
            !string.Equals(appearance, "Off", StringComparison.Ordinal))
        {
            return appearance;
        }

        return "Yes";
    }

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
