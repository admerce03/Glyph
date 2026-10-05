using System.Text;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;

namespace Glyph.Pdf.Tests;

public class PdfiumFormStoreTests
{
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

    /// <summary>
    /// Minimal AcroForm (letter page) written with a correct xref.
    /// </summary>
    private static string CreateAcroFormPdf(bool includeCheckBox = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-acroform-" + Guid.NewGuid().ToString("N") + ".pdf");
        var annots = includeCheckBox ? "[7 0 R 8 0 R 9 0 R]" : "[7 0 R 8 0 R]";
        var fields = includeCheckBox ? "[7 0 R 8 0 R 9 0 R]" : "[7 0 R 8 0 R]";
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
