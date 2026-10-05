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
                    using var form = new PdfiumFormFillEnvironment(pdfium.Handle);
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
                            fpdf_formfill.FORM_OnAfterLoadPage(page, form.Handle);
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

                                        var kindCode = fpdf_annot.FPDFAnnotGetFormFieldType(form.Handle, annot);
                                        var kind = MapKind(kindCode);
                                        var name = PdfiumFormStrings.GetFormFieldName(form.Handle, annot);
                                        var value = PdfiumFormStrings.GetFormFieldValue(form.Handle, annot);
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
                                fpdf_formfill.FORM_OnBeforeClosePage(page, form.Handle);
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
                    using var form = new PdfiumFormFillEnvironment(pdfium.Handle);
                    var page = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                    if (page is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for form fill.");
                    }

                    try
                    {
                        fpdf_formfill.FORM_OnAfterLoadPage(page, form.Handle);
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

                                var kind = fpdf_annot.FPDFAnnotGetFormFieldType(form.Handle, annot);
                                if (kind != PdfiumFormFieldKinds.TextField
                                    && kind != PdfiumFormFieldKinds.ComboBox)
                                {
                                    throw new NotSupportedException(
                                        $"Setting values for form field kind {kind} is not supported yet.");
                                }

                                // Prefer writing /V directly. Interactive FORM_ReplaceSelection requires a
                                // richer FPDF_FORMFILLINFO host (caret/timer/page callbacks) and can SEGV
                                // with stub callbacks; /V + NeedAppearances is the durable fill path.
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
                            fpdf_formfill.FORM_OnBeforeClosePage(page, form.Handle);
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

    private static PdfFormFieldKind MapKind(int code) => code switch
    {
        PdfiumFormFieldKinds.PushButton => PdfFormFieldKind.PushButton,
        PdfiumFormFieldKinds.CheckBox => PdfFormFieldKind.CheckBox,
        PdfiumFormFieldKinds.RadioButton => PdfFormFieldKind.RadioButton,
        PdfiumFormFieldKinds.ComboBox => PdfFormFieldKind.ComboBox,
        PdfiumFormFieldKinds.ListBox => PdfFormFieldKind.ListBox,
        PdfiumFormFieldKinds.TextField => PdfFormFieldKind.TextField,
        PdfiumFormFieldKinds.Signature => PdfFormFieldKind.Signature,
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
