using Glyph.Pdf.Abstractions;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Text;

/// <summary>
/// Builds searchable PDFs: full-bleed image under an invisible OCR text layer.
/// </summary>
public static class OcrSearchablePdfWriter
{
    public static byte[] BuildFromPng(
        byte[] pngBytes,
        int pixelWidth,
        int pixelHeight,
        IEnumerable<SearchablePdfWord> words)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        return BuildPages([(pngBytes, false, pixelWidth, pixelHeight, words)]);
    }

    public static byte[] BuildFromJpeg(
        byte[] jpegBytes,
        int pixelWidth,
        int pixelHeight,
        IEnumerable<SearchablePdfWord> words)
    {
        ArgumentNullException.ThrowIfNull(jpegBytes);
        return BuildPages([(jpegBytes, true, pixelWidth, pixelHeight, words)]);
    }

    public static byte[] BuildPages(
        IReadOnlyList<(byte[] ImageBytes, bool IsJpeg, int PixelWidth, int PixelHeight, IEnumerable<SearchablePdfWord> Words)> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var pageSpec in pages)
        {
            ArgumentNullException.ThrowIfNull(pageSpec.ImageBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSpec.PixelWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSpec.PixelHeight);
            ArgumentNullException.ThrowIfNull(pageSpec.Words);

            // 1 image pixel = 1 PDF point keeps OCR boxes aligned without DPI guesswork.
            var page = builder.AddPage(pageSpec.PixelWidth, pageSpec.PixelHeight);
            var pageSize = page.PageSize;
            if (pageSpec.IsJpeg)
            {
                page.AddJpeg(pageSpec.ImageBytes, pageSize);
            }
            else
            {
                page.AddPng(pageSpec.ImageBytes, pageSize);
            }

            page.SetTextRenderingMode(TextRenderingMode.Neither);
            foreach (var word in pageSpec.Words)
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }

                var fontSize = Math.Max(4.0, word.Height);
                // OCR Y is top-down; PDF Y is bottom-up. Baseline ≈ bottom of word box.
                var pdfX = word.X;
                var pdfY = pageSpec.PixelHeight - (word.Y + word.Height);
                page.AddText(word.Text, fontSize, new PdfPoint(pdfX, pdfY), font);
            }
        }

        return builder.Build();
    }
}
