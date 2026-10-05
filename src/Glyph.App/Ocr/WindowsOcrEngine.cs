using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Ocr.Abstractions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using OcrLine = Glyph.Ocr.Abstractions.OcrLine;
using OcrResult = Glyph.Ocr.Abstractions.OcrResult;
using OcrWord = Glyph.Ocr.Abstractions.OcrWord;

namespace Glyph.App.Ocr;

/// <summary>
/// Offline OCR via Windows.Media.Ocr and installed language packs.
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PixelWidth <= 0 || request.PixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.BgraPixels);
        var expected = checked(request.PixelWidth * request.PixelHeight * 4);
        if (request.BgraPixels.Length < expected)
        {
            throw new ArgumentException("BGRA buffer is smaller than width×height×4.", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var engine = CreateEngine(request.LanguageTag)
            ?? throw new InvalidOperationException(
                "No OCR language pack is available. Install a Windows OCR language pack.");

        var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
            request.BgraPixels.AsBuffer(0, expected),
            BitmapPixelFormat.Bgra8,
            request.PixelWidth,
            request.PixelHeight,
            BitmapAlphaMode.Premultiplied);

        cancellationToken.ThrowIfCancellationRequested();
        var ocrResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);

        var lines = new List<OcrLine>(ocrResult.Lines.Count);
        foreach (var line in ocrResult.Lines)
        {
            var words = new List<OcrWord>(line.Words.Count);
            foreach (var word in line.Words)
            {
                words.Add(new OcrWord(
                    word.Text,
                    word.BoundingRect.X,
                    word.BoundingRect.Y,
                    word.BoundingRect.Width,
                    word.BoundingRect.Height));
            }

            lines.Add(new OcrLine(line.Text, words));
        }

        return new OcrResult(ocrResult.Text ?? string.Empty, lines);
    }

    private static OcrEngine? CreateEngine(string? languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            try
            {
                var language = new Language(languageTag);
                if (OcrEngine.IsLanguageSupported(language))
                {
                    return OcrEngine.TryCreateFromLanguage(language);
                }
            }
            catch (ArgumentException)
            {
                // Fall through to profile languages.
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages();
    }
}
