using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

internal static class PdfiumPageCatalog
{
    public static List<PdfiumPage> Build(PdfiumDocument document, FpdfDocumentT handle)
    {
        var pageCount = fpdfview.FPDF_GetPageCount(handle);
        var pages = new List<PdfiumPage>(pageCount);
        for (var i = 0; i < pageCount; i++)
        {
            double width = 0;
            double height = 0;
            fpdfview.FPDF_GetPageSizeByIndex(handle, i, ref width, ref height);

            var rotationDegrees = 0;
            var page = fpdfview.FPDF_LoadPage(handle, i);
            if (page is not null)
            {
                try
                {
                    var rotationQuarterTurns = fpdf_edit.FPDFPageGetRotation(page);
                    rotationDegrees = Math.Clamp(rotationQuarterTurns, 0, 3) * 90;
                }
                finally
                {
                    fpdfview.FPDF_ClosePage(page);
                }
            }

            pages.Add(new PdfiumPage(document, i, width, height, rotationDegrees));
        }

        return pages;
    }

    /// <summary>
    /// Builds a 1-based PDFium page-range string preserving the given zero-based order.
    /// </summary>
    public static string ToPageRange(IReadOnlyList<int> zeroBasedIndexes)
    {
        if (zeroBasedIndexes.Count == 0)
        {
            throw new ArgumentException("At least one page index is required.", nameof(zeroBasedIndexes));
        }

        return string.Join(',', zeroBasedIndexes.Select(i => (i + 1).ToString()));
    }
}
