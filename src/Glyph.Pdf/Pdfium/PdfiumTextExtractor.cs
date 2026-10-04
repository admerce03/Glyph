using System.Text;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumTextExtractor : IPdfTextExtractor
{
    public Task<IReadOnlyList<PdfTextChar>> GetCharsAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var pageInfo = (PdfiumPage)pdfium.GetPage(pageIndex);
                    var page = pageInfo.LoadNativePage();
                    FpdfTextpageT? textPage = null;
                    try
                    {
                        textPage = fpdf_text.FPDFTextLoadPage(page);
                        if (textPage is null)
                        {
                            return (IReadOnlyList<PdfTextChar>)Array.Empty<PdfTextChar>();
                        }

                        var count = fpdf_text.FPDFTextCountChars(textPage);
                        var chars = new List<PdfTextChar>(Math.Max(0, count));
                        for (var i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var code = fpdf_text.FPDFTextGetUnicode(textPage, i);
                            double left = 0, right = 0, bottom = 0, top = 0;
                            fpdf_text.FPDFTextGetCharBox(textPage, i, ref left, ref right, ref bottom, ref top);
                            chars.Add(new PdfTextChar(
                                i,
                                UnicodeToString(code),
                                new PdfRect(left, bottom, right, top)));
                        }

                        return (IReadOnlyList<PdfTextChar>)chars;
                    }
                    finally
                    {
                        if (textPage is not null)
                        {
                            fpdf_text.FPDFTextClosePage(textPage);
                        }

                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }

    public Task<string> GetTextAsync(
        IPdfDocument document,
        int pageIndex,
        CancellationToken cancellationToken = default)
    {
        var pdfium = RequirePdfium(document);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var pageInfo = (PdfiumPage)pdfium.GetPage(pageIndex);
                    var page = pageInfo.LoadNativePage();
                    FpdfTextpageT? textPage = null;
                    try
                    {
                        textPage = fpdf_text.FPDFTextLoadPage(page);
                        if (textPage is null)
                        {
                            return string.Empty;
                        }

                        var count = fpdf_text.FPDFTextCountChars(textPage);
                        if (count <= 0)
                        {
                            return string.Empty;
                        }

                        var buffer = new ushort[count + 1];
                        fpdf_text.FPDFTextGetText(textPage, 0, count, ref buffer[0]);
                        return ushortsToString(buffer);
                    }
                    finally
                    {
                        if (textPage is not null)
                        {
                            fpdf_text.FPDFTextClosePage(textPage);
                        }

                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }

    private static string UnicodeToString(uint code)
    {
        try
        {
            return code <= char.MaxValue
                ? ((char)code).ToString()
                : char.ConvertFromUtf32(checked((int)code));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ushortsToString(ushort[] buffer)
    {
        var sb = new StringBuilder(buffer.Length);
        foreach (var unit in buffer)
        {
            if (unit == 0)
            {
                break;
            }

            sb.Append((char)unit);
        }

        return sb.ToString();
    }

    private static PdfiumDocument RequirePdfium(IPdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return pdfium;
    }
}
