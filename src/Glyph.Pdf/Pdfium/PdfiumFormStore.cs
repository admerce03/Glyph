using Glyph.Pdf.Abstractions;
using PDFiumCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;

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
                                    if (kind is PdfFormFieldKind.CheckBox or PdfFormFieldKind.RadioButton)
                                    {
                                        // Widget appearance is authoritative for button state.
                                        var appearance = PdfiumAnnotStrings.GetString(annot, "AS");
                                        if (!string.IsNullOrEmpty(appearance))
                                        {
                                            value = appearance;
                                        }
                                        else if (string.IsNullOrEmpty(value))
                                        {
                                            value = "Off";
                                        }
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

                    EnrichChoiceOptions(pdfium.Path, fields);
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
                            if (kind is not (PdfFormFieldKind.TextField
                                or PdfFormFieldKind.ComboBox
                                or PdfFormFieldKind.ListBox))
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

                            var onState = ResolveButtonOnState(annot);
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

    public Task SetRadioButtonAsync(
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

                    string groupName;
                    string onState;
                    var selectedPage = fpdfview.FPDF_LoadPage(pdfium.Handle, pageIndex);
                    if (selectedPage is null)
                    {
                        throw new InvalidOperationException($"Failed to load page {pageIndex} for radio.");
                    }

                    try
                    {
                        var selectedAnnot = fpdf_annot.FPDFPageGetAnnot(selectedPage, annotIndex);
                        if (selectedAnnot is null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(annotIndex), "Annotation not found.");
                        }

                        try
                        {
                            if (fpdf_annot.FPDFAnnotGetSubtype(selectedAnnot) != PdfiumAnnotSubtypes.Widget)
                            {
                                throw new InvalidOperationException("Target annotation is not a form widget.");
                            }

                            if (MapKind(selectedAnnot) != PdfFormFieldKind.RadioButton)
                            {
                                throw new NotSupportedException("Target annotation is not a radio button.");
                            }

                            groupName = PdfiumAnnotStrings.GetString(selectedAnnot, "T");
                            onState = ResolveButtonOnState(selectedAnnot);
                        }
                        finally
                        {
                            fpdf_annot.FPDFPageCloseAnnot(selectedAnnot);
                        }
                    }
                    finally
                    {
                        fpdfview.FPDF_ClosePage(selectedPage);
                    }

                    // Mutual exclusion: select this widget; Off siblings with the same /T.
                    for (var p = 0; p < pdfium.PageCount; p++)
                    {
                        var page = fpdfview.FPDF_LoadPage(pdfium.Handle, p);
                        if (page is null)
                        {
                            continue;
                        }

                        try
                        {
                            var count = fpdf_annot.FPDFPageGetAnnotCount(page);
                            for (var a = 0; a < count; a++)
                            {
                                var annot = fpdf_annot.FPDFPageGetAnnot(page, a);
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

                                    if (MapKind(annot) != PdfFormFieldKind.RadioButton)
                                    {
                                        continue;
                                    }

                                    var name = PdfiumAnnotStrings.GetString(annot, "T");
                                    if (!string.Equals(name, groupName, StringComparison.Ordinal))
                                    {
                                        continue;
                                    }

                                    var isSelected = p == pageIndex && a == annotIndex;
                                    if (isSelected)
                                    {
                                        if (!PdfiumAnnotStrings.SetString(annot, "V", onState) ||
                                            !PdfiumAnnotStrings.SetString(annot, "AS", onState))
                                        {
                                            throw new InvalidOperationException(
                                                "Failed to set radio button /V and /AS.");
                                        }
                                    }
                                    else
                                    {
                                        if (!PdfiumAnnotStrings.SetString(annot, "AS", "Off") ||
                                            !PdfiumAnnotStrings.SetString(annot, "V", "Off"))
                                        {
                                            throw new InvalidOperationException(
                                                "Failed to clear sibling radio /V and /AS.");
                                        }
                                    }
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

                    pdfium.NotifyAnnotationsChanged();
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

    private static string ResolveButtonOnState(FpdfAnnotationT annot)
    {
        // Prefer this widget's export name (/DV, then non-Off /AS) over /V, which may
        // reflect a previously selected sibling in the same radio group.
        var defaults = PdfiumAnnotStrings.GetString(annot, "DV");
        if (!string.IsNullOrEmpty(defaults) &&
            !string.Equals(defaults, "Off", StringComparison.Ordinal))
        {
            return defaults;
        }

        var appearance = PdfiumAnnotStrings.GetString(annot, "AS");
        if (!string.IsNullOrEmpty(appearance) &&
            !string.Equals(appearance, "Off", StringComparison.Ordinal))
        {
            return appearance;
        }

        var current = PdfiumAnnotStrings.GetString(annot, "V");
        if (!string.IsNullOrEmpty(current) &&
            !string.Equals(current, "Off", StringComparison.Ordinal))
        {
            return current;
        }

        return "Yes";
    }

    private static void EnrichChoiceOptions(string? path, List<PdfFormFieldInfo> fields)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || fields.Count == 0)
        {
            return;
        }

        try
        {
            using var pig = PdfDocument.Open(path);
            if (!pig.TryGetForm(out var form) || form is null)
            {
                return;
            }

            var optionsByName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var field in form.GetFields())
            {
                IReadOnlyList<AcroChoiceOption>? options = field switch
                {
                    AcroComboBoxField combo => combo.Options,
                    AcroListBoxField list => list.Options,
                    _ => null,
                };
                if (options is null || options.Count == 0)
                {
                    continue;
                }

                var name = field.Information.PartialName
                    ?? field.Information.AlternateName
                    ?? field.Information.MappingName
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                optionsByName[name] = options
                    .Select(o => !string.IsNullOrEmpty(o.Name) ? o.Name : o.ExportValue)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            if (optionsByName.Count == 0)
            {
                return;
            }

            for (var i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f.Kind is not (PdfFormFieldKind.ComboBox or PdfFormFieldKind.ListBox))
                {
                    continue;
                }

                if (!optionsByName.TryGetValue(f.Name, out var opts) || opts.Count == 0)
                {
                    continue;
                }

                fields[i] = f with { Options = opts };
            }
        }
        catch
        {
            // Options are enrichment only; listing must still succeed without PdfPig form parse.
        }
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
