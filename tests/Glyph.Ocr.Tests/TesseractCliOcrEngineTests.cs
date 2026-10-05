using FluentAssertions;
using Glyph.Ocr.Abstractions;
using Glyph.Ocr.Tesseract;
using ImageMagick;

namespace Glyph.Ocr.Tests;

public class TesseractCliOcrEngineTests
{
    [Fact]
    public async Task Recognizes_printed_text_offline()
    {
        var engine = new TesseractCliOcrEngine();
        if (!engine.IsAvailable)
        {
            return; // Environment without tesseract (skip rather than fail local sparse setups).
        }

        var (width, height, bgra) = RenderTextBitmap("Glyph OCR 123");
        var progressValues = new List<double>();
        var progress = new Progress<OcrProgress>(p => progressValues.Add(p.Fraction));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await engine.RecognizeAsync(
            new OcrRequest(width, height, bgra, LanguageTag: "eng", Progress: progress),
            cts.Token);

        result.Text.Should().ContainEquivalentOf("Glyph");
        result.Text.Should().Match(t => t.Contains("OCR", StringComparison.OrdinalIgnoreCase) || t.Contains("123"));
        progressValues.Should().NotBeEmpty();
        progressValues.Last().Should().BeApproximately(1.0, 0.01);
    }

    private static (int Width, int Height, byte[] Bgra) RenderTextBitmap(string text)
    {
        var settings = new MagickReadSettings
        {
            Width = 640,
            Height = 160,
            BackgroundColor = MagickColors.White,
            FillColor = MagickColors.Black,
            FontPointsize = 48,
            TextGravity = Gravity.Center,
        };
        using var image = new MagickImage($"caption:{text}", settings);
        image.Depth = 8;
        image.ColorType = ColorType.TrueColorAlpha;
        var bgra = image.ToByteArray(MagickFormat.Bgra);
        return (checked((int)image.Width), checked((int)image.Height), bgra);
    }
}
