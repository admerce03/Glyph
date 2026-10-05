using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumOpenRenderTests
{
    [Fact]
    public async Task Open_and_render_sample_pdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-sample-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            CreateSamplePdf(path);

            var factory = new PdfiumDocumentFactory();
            var renderer = new PdfiumRenderer();

            await using var document = await factory.OpenAsync(path);
            document.PageCount.Should().Be(2);
            document.GetPage(0).WidthPoints.Should().BeGreaterThan(0);

            using var result = await renderer.RenderPageAsync(
                document,
                pageIndex: 0,
                new PdfRenderRequest(Scale: 1.0));

            result.Width.Should().BeGreaterThan(10);
            result.Height.Should().BeGreaterThan(10);
            result.Pixels.Length.Should().Be(result.Width * result.Height * 4);
            result.Pixels.ToArray().Any(b => b != 0 && b != 255).Should().BeTrue("rendered page should contain non-flat content");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Render_respects_max_edge_for_ocr_sized_bitmaps()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-ocr-render-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            CreateSamplePdf(path);

            var factory = new PdfiumDocumentFactory();
            var renderer = new PdfiumRenderer();
            await using var document = await factory.OpenAsync(path);

            const int maxEdge = 512;
            using var result = await renderer.RenderPageAsync(
                document,
                pageIndex: 0,
                new PdfRenderRequest(Scale: 8.0, MaxWidthPixels: maxEdge, MaxHeightPixels: maxEdge));

            result.Width.Should().BeLessThanOrEqualTo(maxEdge);
            result.Height.Should().BeLessThanOrEqualTo(maxEdge);
            result.Pixels.Length.Should().Be(result.Width * result.Height * 4);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Open_reads_page_rotation_and_swaps_display_size()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-rotated-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            // Letter MediaBox 612x792 with /Rotate 90 → displayed size 792x612.
            File.WriteAllBytes(path, CreateRotatedPagePdf(rotateDegrees: 90));

            var factory = new PdfiumDocumentFactory();
            await using var document = await factory.OpenAsync(path);
            var page = document.GetPage(0);
            page.RotationDegrees.Should().Be(90);
            page.WidthPoints.Should().BeApproximately(792, 0.5);
            page.HeightPoints.Should().BeApproximately(612, 0.5);

            var renderer = new PdfiumRenderer();
            using var result = await renderer.RenderPageAsync(
                document,
                pageIndex: 0,
                new PdfRenderRequest(Scale: 1.0));
            result.Width.Should().BeGreaterThan(result.Height);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Open_encrypted_pdf_requires_password_then_succeeds()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "encrypted.pdf");
        File.Exists(path).Should().BeTrue();

        var factory = new PdfiumDocumentFactory();

        var missing = async () => await factory.OpenAsync(path);
        await missing.Should().ThrowAsync<PdfPasswordRequiredException>()
            .Where(ex => !ex.PasswordWasProvided);

        var wrong = async () => await factory.OpenAsync(path, "wrong-password");
        await wrong.Should().ThrowAsync<PdfPasswordRequiredException>()
            .Where(ex => ex.PasswordWasProvided);

        await using var document = await factory.OpenAsync(path, "secret");
        document.PageCount.Should().Be(1);
        document.IsEncrypted.Should().BeTrue();
    }

    private static void CreateSamplePdf(string path)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var page1 = builder.AddPage(PageSize.A4);
        page1.AddText("Glyph sample page 1", 18, new PdfPoint(50, 750), font);

        var page2 = builder.AddPage(PageSize.A4);
        page2.AddText("Glyph sample page 2", 18, new PdfPoint(50, 750), font);

        File.WriteAllBytes(path, builder.Build());
    }

    private static byte[] CreateRotatedPagePdf(int rotateDegrees)
    {
        // Minimal single-page PDF with explicit /Rotate; offsets computed at write time.
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.NewLine = "\n";

        var offsets = new long[6];
        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        offsets[1] = ms.Position;
        writer.WriteLine("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj");
        writer.Flush();

        offsets[2] = ms.Position;
        writer.WriteLine("2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj");
        writer.Flush();

        offsets[3] = ms.Position;
        writer.WriteLine(
            $"3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Rotate {rotateDegrees} /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>endobj");
        writer.Flush();

        const string content = "BT /F1 24 Tf 72 72 Td (Rotated) Tj ET";
        offsets[4] = ms.Position;
        writer.WriteLine($"4 0 obj<< /Length {content.Length} >>stream");
        writer.WriteLine(content);
        writer.WriteLine("endstream");
        writer.WriteLine("endobj");
        writer.Flush();

        offsets[5] = ms.Position;
        writer.WriteLine("5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj");
        writer.Flush();

        var xref = ms.Position;
        writer.WriteLine("xref");
        writer.WriteLine("0 6");
        writer.WriteLine("0000000000 65535 f ");
        for (var i = 1; i <= 5; i++)
        {
            writer.WriteLine($"{offsets[i]:D10} 00000 n ");
        }

        writer.WriteLine("trailer<< /Size 6 /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xref.ToString());
        writer.WriteLine("%%EOF");
        writer.Flush();
        return ms.ToArray();
    }
}
