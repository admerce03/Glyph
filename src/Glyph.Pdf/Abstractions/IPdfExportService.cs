namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Export PDF pages as raster images (F01-18 / F45 / F62).
/// Implementation lives in the app host so Magick/WIC stay out of Glyph.Pdf.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Render one page and write an image file at <paramref name="path"/>.
    /// </summary>
    Task ExportPageAsImageAsync(
        IPdfDocument document,
        int pageIndex,
        string path,
        PdfPageImageExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Render each page index into <paramref name="folderPath"/> as
    /// <c>{baseFileName}-p{n}{extension}</c>. Returns the number of files written.
    /// </summary>
    Task<int> ExportPagesAsImagesAsync(
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        string folderPath,
        string baseFileName,
        PdfPageImageExportOptions? options = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
