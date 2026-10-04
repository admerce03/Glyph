using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumDocumentFactory : IPdfDocumentFactory
{
    // PDFium BGRA pixel format constant.
    private const int FpdfBitmapBgra = 4;

    // https://pdfium.googlesource.com/pdfium/+/main/public/fpdfview.h
    private const uint FpdfErrSuccess = 0;
    private const uint FpdfErrPassword = 4;

    public Task<IPdfDocument> OpenAsync(string path, string? password = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();

                lock (PdfiumSync.Gate)
                {
                    var handle = fpdfview.FPDF_LoadDocument(path, password);
                    if (handle is null)
                    {
                        var error = fpdfview.FPDF_GetLastError();
                        if (error == FpdfErrPassword)
                        {
                            throw new PdfPasswordRequiredException(path, passwordWasProvided: password is not null);
                        }

                        throw new InvalidOperationException(
                            error == FpdfErrSuccess
                                ? $"Failed to open PDF: {path}"
                                : $"Failed to open PDF (error {error}): {path}");
                    }

                    var pageCount = fpdfview.FPDF_GetPageCount(handle);
                    var pages = new List<PdfiumPage>(pageCount);
                    var document = new PdfiumDocument(path, handle, pages, isEncrypted: password is not null);

                    for (var i = 0; i < pageCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        double width = 0;
                        double height = 0;
                        fpdfview.FPDF_GetPageSizeByIndex(handle, i, ref width, ref height);
                        pages.Add(new PdfiumPage(document, i, width, height, rotationDegrees: 0));
                    }

                    return (IPdfDocument)document;
                }
            },
            cancellationToken);
    }

    // Expose format constant for renderer in same assembly.
    internal static int BitmapBgraFormat => FpdfBitmapBgra;
}
