using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Process-wide clipboard for PDF page copy/paste between documents, tabs, and windows (F10-24).
/// </summary>
public static class PdfPageClipboard
{
    public const string TempFilePrefix = "glyph-page-clip-";

    private static byte[]? _pdfBytes;
    private static int _pageCount;

    public static bool HasPages => _pdfBytes is { Length: > 0 };

    public static int PageCount => _pageCount;

    public static async Task SetFromDocumentAsync(
        IPdfPageEditor editor,
        IPdfDocument document,
        IReadOnlyList<int> pageIndexes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pageIndexes);
        if (pageIndexes.Count == 0)
        {
            Clear();
            return;
        }

        await using var extracted = await editor.ExtractPagesAsync(document, pageIndexes, cancellationToken);
        _pdfBytes = await editor.SaveToBytesAsync(extracted, cancellationToken);
        _pageCount = extracted.PageCount;
    }

    /// <summary>
    /// Opens a temporary copy of the clipboard PDF. Caller must dispose the document
    /// and delete <paramref name="tempPath"/> when finished.
    /// </summary>
    public static async Task<(IPdfDocument? Document, string? TempPath)> OpenCopyAsync(
        IPdfDocumentFactory factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_pdfBytes is null || _pdfBytes.Length == 0)
        {
            return (null, null);
        }

        var path = Path.Combine(Path.GetTempPath(), TempFilePrefix + Guid.NewGuid().ToString("N") + ".pdf");
        await File.WriteAllBytesAsync(path, _pdfBytes, cancellationToken);
        try
        {
            var document = await factory.OpenAsync(path, password: null, cancellationToken);
            return (document, path);
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }
    }

    public static void Clear()
    {
        _pdfBytes = null;
        _pageCount = 0;
    }
}
