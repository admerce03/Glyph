using System.Text;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;

namespace Glyph.Pdf.Tests;

public class PdfiumFormStoreTests
{
    [Fact]
    public async Task Set_text_enables_auto_font_size_in_da()
    {
        var path = CreateAcroFormPdf();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-form-auto-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var fields = await forms.ListFieldsAsync(document);
                var name = fields.Should().ContainSingle(f => f.Name == "Name").Subject;
                name.UsesAutoFontSize.Should().BeFalse();
                PdfFormDefaultAppearance.TryGetFontSize(name.DefaultAppearance).Should().Be(12f);

                await forms.SetTextValueAsync(document, name.PageIndex, name.AnnotIndex, "Very Long Name That Should Shrink");
                var after = await forms.ListFieldsAsync(document);
                var updated = after.Should().ContainSingle(f => f.Name == "Name").Subject;
                updated.Value.Should().Be("Very Long Name That Should Shrink");
                updated.UsesAutoFontSize.Should().BeTrue();
                PdfFormDefaultAppearance.TryGetFontSize(updated.DefaultAppearance).Should().Be(0f);
                updated.DefaultAppearance.Should().Contain("/Helv");

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var fields = await forms.ListFieldsAsync(reopened);
                var name = fields.Should().ContainSingle(f => f.Name == "Name").Subject;
                name.UsesAutoFontSize.Should().BeTrue();
                name.Value.Should().Be("Very Long Name That Should Shrink");
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Set_text_can_skip_auto_font_size()
    {
        var path = CreateAcroFormPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();

            await using var document = await factory.OpenAsync(path);
            var fields = await forms.ListFieldsAsync(document);
            var city = fields.Should().ContainSingle(f => f.Name == "City").Subject;

            await forms.SetTextValueAsync(
                document,
                city.PageIndex,
                city.AnnotIndex,
                "Paris",
                autoFontSize: false);

            var after = await forms.ListFieldsAsync(document);
            var updated = after.Should().ContainSingle(f => f.Name == "City").Subject;
            updated.Value.Should().Be("Paris");
            updated.UsesAutoFontSize.Should().BeFalse();
            PdfFormDefaultAppearance.TryGetFontSize(updated.DefaultAppearance).Should().Be(12f);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Default_appearance_helpers_rewrite_and_fit()
    {
        PdfFormDefaultAppearance.WithAutoFontSize("/Helv 12 Tf 0 g")
            .Should().Be("/Helv 0 Tf 0 g");
        PdfFormDefaultAppearance.WithFontSize("/TiRo 18 Tf 0.1 0.2 0.3 rg", 9)
            .Should().Be("/TiRo 9 Tf 0.1 0.2 0.3 rg");
        PdfFormDefaultAppearance.WithAutoFontSize(null)
            .Should().Be("/Helv 0 Tf 0 g");
        PdfFormDefaultAppearance.UsesAutoFontSize("/Helv 0 Tf 0 g").Should().BeTrue();
        PdfFormDefaultAppearance.UsesAutoFontSize("/Helv 12 Tf 0 g").Should().BeFalse();

        var bounds = new PdfRect(0, 0, 100, 20);
        var fit = PdfFormDefaultAppearance.ComputeFitSize(bounds, "Hi", multiline: false);
        fit.Should().BeInRange(4f, 20f);
    }

    [Fact]
    public async Task List_and_set_text_field_round_trips_and_tab_order()
    {
        var path = CreateAcroFormPdf();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-form-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                (await forms.HasFormAsync(document)).Should().BeTrue();
                var fields = await forms.ListFieldsAsync(document);
                fields.Should().HaveCount(2);
                fields[0].Name.Should().Be("Name");
                fields[0].Kind.Should().Be(PdfFormFieldKind.TextField);
                fields[0].TabOrder.Should().Be(0);
                fields[1].Name.Should().Be("City");
                fields[1].TabOrder.Should().Be(1);

                await forms.SetTextValueAsync(document, fields[0].PageIndex, fields[0].AnnotIndex, "Ada Lovelace");
                var next = await forms.FocusAdjacentAsync(
                    document,
                    fields[0].PageIndex,
                    fields[0].AnnotIndex,
                    forward: true);
                next.Should().NotBeNull();
                next!.Name.Should().Be("City");

                await forms.SetTextValueAsync(document, fields[1].PageIndex, fields[1].AnnotIndex, "London");
                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var fields = await forms.ListFieldsAsync(reopened);
                fields.Should().Contain(f => f.Name == "Name" && f.Value == "Ada Lovelace");
                fields.Should().Contain(f => f.Name == "City" && f.Value == "London");
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Checkbox_toggle_sets_v_and_as_and_survives_save()
    {
        var path = CreateAcroFormPdf(includeCheckBox: true);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-check-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var fields = await forms.ListFieldsAsync(document);
                var check = fields.Should().ContainSingle(f => f.Name == "Agree").Subject;
                check.Kind.Should().Be(PdfFormFieldKind.CheckBox);

                await forms.SetCheckBoxAsync(document, check.PageIndex, check.AnnotIndex, isChecked: true);
                var listed = await forms.ListFieldsAsync(document);
                listed.Should().Contain(f => f.Name == "Agree" && f.Value == "Yes");

                await forms.SetCheckBoxAsync(document, check.PageIndex, check.AnnotIndex, isChecked: false);
                listed = await forms.ListFieldsAsync(document);
                listed.Should().Contain(f => f.Name == "Agree" && f.Value == "Off");

                await forms.SetCheckBoxAsync(document, check.PageIndex, check.AnnotIndex, isChecked: true);
                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var fields = await forms.ListFieldsAsync(reopened);
                fields.Should().Contain(f => f.Name == "Agree" && f.Value == "Yes");
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Radio_select_is_mutual_exclusive_and_survives_save()
    {
        var path = CreateAcroFormPdf(includeRadio: true);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-radio-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var fields = await forms.ListFieldsAsync(document);
                var radios = fields.Where(f => f.Name == "Color").OrderBy(f => f.TabOrder).ToList();
                radios.Should().HaveCount(2);
                radios.Should().OnlyContain(f => f.Kind == PdfFormFieldKind.RadioButton);

                var first = radios[0];
                var second = radios[1];

                await forms.SetRadioButtonAsync(document, first.PageIndex, first.AnnotIndex);
                var listed = await forms.ListFieldsAsync(document);
                var afterFirst = listed.Where(f => f.Name == "Color").OrderBy(f => f.TabOrder).ToList();
                afterFirst[0].Value.Should().Be("Red");
                afterFirst[1].Value.Should().Be("Off");

                await forms.SetRadioButtonAsync(document, second.PageIndex, second.AnnotIndex);
                listed = await forms.ListFieldsAsync(document);
                var afterSecond = listed.Where(f => f.Name == "Color").OrderBy(f => f.TabOrder).ToList();
                afterSecond[0].Value.Should().Be("Off");
                afterSecond[1].Value.Should().Be("Blue");

                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var fields = await forms.ListFieldsAsync(reopened);
                var radios = fields.Where(f => f.Name == "Color").OrderBy(f => f.TabOrder).ToList();
                radios.Should().HaveCount(2);
                radios[0].Value.Should().Be("Off");
                radios[1].Value.Should().Be("Blue");
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Choice_fields_expose_options_and_set_value()
    {
        var path = CreateAcroFormPdf(includeChoice: true);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-choice-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var fields = await forms.ListFieldsAsync(document);
                var combo = fields.Should().ContainSingle(f => f.Name == "Flavor").Subject;
                combo.Kind.Should().Be(PdfFormFieldKind.ComboBox);
                combo.ChoiceOptions.Should().BeEquivalentTo("Vanilla", "Chocolate", "Strawberry");

                var listBox = fields.Should().ContainSingle(f => f.Name == "Size").Subject;
                listBox.Kind.Should().Be(PdfFormFieldKind.ListBox);
                listBox.ChoiceOptions.Should().BeEquivalentTo("Small", "Medium", "Large");

                await forms.SetTextValueAsync(document, combo.PageIndex, combo.AnnotIndex, "Chocolate");
                await forms.SetTextValueAsync(document, listBox.PageIndex, listBox.AnnotIndex, "Large");
                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var fields = await forms.ListFieldsAsync(reopened);
                fields.Should().Contain(f => f.Name == "Flavor" && f.Value == "Chocolate");
                fields.Should().Contain(f => f.Name == "Size" && f.Value == "Large");
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Signature_field_accepts_stamp_in_field_bounds()
    {
        var path = CreateAcroFormPdf(includeSignature: true);
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-sigfield-out-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();
            var annots = new PdfiumAnnotationService();
            var editor = new PdfiumPageEditor();

            await using (var document = await factory.OpenAsync(path))
            {
                var fields = await forms.ListFieldsAsync(document);
                var sig = fields.Should().ContainSingle(f => f.Kind == PdfFormFieldKind.Signature).Subject;

                // 2×2 opaque black BGRA stamp fitted into the field rect.
                var pixels = new byte[]
                {
                    0, 0, 0, 255, 0, 0, 0, 255,
                    0, 0, 0, 255, 0, 0, 0, 255,
                };
                await annots.AddStampAsync(
                    document,
                    sig.PageIndex,
                    sig.Bounds,
                    pixels,
                    pixelWidth: 2,
                    pixelHeight: 2);

                var listed = await annots.ListAsync(document, sig.PageIndex);
                listed.Should().Contain(a => a.IsStamp);
                await editor.SaveAsync(document, outPath);
            }

            await using (var reopened = await factory.OpenAsync(outPath))
            {
                var listed = await annots.ListAsync(reopened, 0);
                listed.Should().Contain(a => a.IsStamp);
                var formsListed = await forms.ListFieldsAsync(reopened);
                formsListed.Should().Contain(f => f.Name == "Signer" && f.Kind == PdfFormFieldKind.Signature);
            }
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Signature_field_is_listed_as_signature_kind()
    {
        var path = CreateAcroFormPdf(includeSignature: true);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();

            await using var document = await factory.OpenAsync(path);
            var fields = await forms.ListFieldsAsync(document);
            var sig = fields.Should().ContainSingle(f => f.Name == "Signer").Subject;
            sig.Kind.Should().Be(PdfFormFieldKind.Signature);
            sig.Bounds.Width.Should().BeGreaterThan(10);
            sig.Bounds.Height.Should().BeGreaterThan(10);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Push_button_exposes_uri_action()
    {
        var path = CreateAcroFormPdf(includePushButton: true);
        try
        {
            var factory = new PdfiumDocumentFactory();
            var forms = new PdfiumFormStore();

            await using var document = await factory.OpenAsync(path);
            var fields = await forms.ListFieldsAsync(document);
            var button = fields.Should().ContainSingle(f => f.Name == "Website").Subject;
            button.Kind.Should().Be(PdfFormFieldKind.PushButton);
            button.Value.Should().Be("Open site");
            button.ButtonAction.Should().NotBeNull();
            button.ButtonAction!.Kind.Should().Be(PdfFormButtonActionKind.Uri);
            button.ButtonAction.Uri.Should().Be("https://example.com/glyph");
            button.ButtonAction.Caption.Should().Be("Open site");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Minimal AcroForm (letter page) written with a correct xref.
    /// </summary>
    private static string CreateAcroFormPdf(
        bool includeCheckBox = false,
        bool includeRadio = false,
        bool includeChoice = false,
        bool includePushButton = false,
        bool includeSignature = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-acroform-" + Guid.NewGuid().ToString("N") + ".pdf");
        var annotRefs = new List<string> { "7 0 R", "8 0 R" };
        var nextObj = 9;
        if (includeCheckBox)
        {
            annotRefs.Add($"{nextObj} 0 R");
            nextObj++;
        }

        if (includeRadio)
        {
            annotRefs.Add($"{nextObj} 0 R");
            annotRefs.Add($"{nextObj + 1} 0 R");
            nextObj += 2;
        }

        if (includeChoice)
        {
            annotRefs.Add($"{nextObj} 0 R");
            annotRefs.Add($"{nextObj + 1} 0 R");
            nextObj += 2;
        }

        if (includePushButton)
        {
            annotRefs.Add($"{nextObj} 0 R");
            nextObj++;
        }

        if (includeSignature)
        {
            annotRefs.Add($"{nextObj} 0 R");
        }

        var annots = "[" + string.Join(" ", annotRefs) + "]";
        var fields = annots;
        var objects = new List<string>
        {
            // 1 Catalog
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
            // 2 Pages
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            // 3 Page
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 6 0 R >> >> /Annots {annots} >>",
            // 4 Contents
            "<< /Length 68 >>\nstream\nBT /F1 12 Tf 72 720 Td (Name:) Tj 0 -40 Td (City:) Tj ET\nendstream",
            // 5 AcroForm
            $"<< /Fields {fields} /DR << /Font << /Helv 6 0 R >> >> /DA (/Helv 0 Tf 0 g) /NeedAppearances true >>",
            // 6 Font
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            // 7 Name widget
            "<< /Type /Annot /Subtype /Widget /Rect [120 710 320 735] /F 4 /P 3 0 R /FT /Tx /T (Name) /V () /DV () /DA (/Helv 12 Tf 0 g) /MK << >> >>",
            // 8 City widget
            "<< /Type /Annot /Subtype /Widget /Rect [120 670 320 695] /F 4 /P 3 0 R /FT /Tx /T (City) /V () /DV () /DA (/Helv 12 Tf 0 g) /MK << >> >>",
        };

        if (includeCheckBox)
        {
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [120 630 140 650] /F 4 /P 3 0 R /FT /Btn /T (Agree) /V /Off /AS /Off /Ff 0 /MK << >> >>");
        }

        if (includeRadio)
        {
            // Ff bit 15 (32768) = radio. /DV holds export name while /AS is Off.
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [120 590 140 610] /F 4 /P 3 0 R /FT /Btn /T (Color) /V /Off /AS /Off /DV /Red /Ff 32768 /MK << >> >>");
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [160 590 180 610] /F 4 /P 3 0 R /FT /Btn /T (Color) /V /Off /AS /Off /DV /Blue /Ff 32768 /MK << >> >>");
        }

        if (includeChoice)
        {
            // Ff bit 18 (131072) = combo. List box has Ff without combo bit.
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [120 550 280 575] /F 4 /P 3 0 R /FT /Ch /T (Flavor) /V (Vanilla) /DV (Vanilla) /Opt [(Vanilla)(Chocolate)(Strawberry)] /Ff 131072 /DA (/Helv 12 Tf 0 g) /MK << >> >>");
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [120 500 280 545] /F 4 /P 3 0 R /FT /Ch /T (Size) /V (Small) /DV (Small) /Opt [(Small)(Medium)(Large)] /Ff 0 /DA (/Helv 12 Tf 0 g) /MK << >> >>");
        }

        if (includePushButton)
        {
            // Ff bit 17 (65536) = pushbutton. URI action + caption in /MK /CA.
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [120 450 220 480] /F 4 /P 3 0 R /FT /Btn /T (Website) /Ff 65536 /TU (Open site) /MK << /CA (Open site) >> /A << /S /URI /URI (https://example.com/glyph) >> >>");
        }

        if (includeSignature)
        {
            objects.Add(
                "<< /Type /Annot /Subtype /Widget /Rect [120 380 320 430] /F 4 /P 3 0 R /FT /Sig /T (Signer) /V null /MK << >> >>");
        }

        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true))
        {
            writer.Write("%PDF-1.4\n");
            writer.Flush();
            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(ms.Position);
                writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
                writer.Flush();
            }

            var xref = ms.Position;
            writer.Write($"xref\n0 {objects.Count + 1}\n");
            writer.Write("0000000000 65535 f \n");
            for (var i = 1; i <= objects.Count; i++)
            {
                writer.Write($"{offsets[i]:D10} 00000 n \n");
            }

            writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            writer.Write($"startxref\n{xref}\n%%EOF\n");
            writer.Flush();
        }

        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }
}
