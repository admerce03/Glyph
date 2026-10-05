using Glyph.Core.Documents;
using Glyph.Imaging.Abstractions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;

namespace Glyph.App.Pdf;

/// <summary>
/// Renders PDF pages via <see cref="IPdfRenderer"/> and writes images via <see cref="IImageEncoder"/>.
/// </summary>
internal sealed class PdfPageImageExportService : IPdfExportService
{
    private readonly IPdfRenderer _renderer;
    private readonly IImageEncoder _encoder;

    public PdfPageImageExportService(IPdfRenderer renderer, IImageEncoder encoder)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
    }

    public async Task ExportPageAsImageAsync(
        IPdfDocument document,
        int pageIndex,
        string path,
        PdfPageImageExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var opts = options ?? new PdfPageImageExportOptions();
        var format = ImageEncodeFormatResolver.FromExtension(opts.Extension);
        var encode = ToEncodeOptions(opts, format);

        using var rendered = await _renderer.RenderPageAsync(
            document,
            pageIndex,
            new PdfRenderRequest(opts.Scale),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await _encoder.WriteBgraAsync(
            rendered.Pixels.ToArray(),
            rendered.Width,
            rendered.Height,
            path,
            format,
            encode,
            cancellationToken);
    }

    public async Task<int> ExportPagesAsImagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        string folderPath,
        string baseFileName,
        PdfPageImageExportOptions? options = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pageIndexes);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFileName);
        var opts = options ?? new PdfPageImageExportOptions();
        Directory.CreateDirectory(folderPath);

        var written = 0;
        var total = pageIndexes.Count;
        foreach (var pageIndex in pageIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = DocumentExportFileNames.PageImage(baseFileName, pageIndex + 1, opts.Extension);
            var path = Path.Combine(folderPath, name);
            await ExportPageAsImageAsync(document, pageIndex, path, opts, cancellationToken);
            written++;
            if (total > 0)
            {
                progress?.Report(100.0 * written / total);
            }
        }

        return written;
    }

    private static ImageEncodeOptions ToEncodeOptions(PdfPageImageExportOptions opts, ImageEncodeFormat format)
    {
        var qualityFormats = format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Webp or ImageEncodeFormat.Avif;
        return new ImageEncodeOptions(
            Quality: qualityFormats ? opts.Quality : null,
            Title: opts.Title,
            Author: opts.Author,
            EmbedSrgbProfile: opts.EmbedSrgbProfile);
    }
}
