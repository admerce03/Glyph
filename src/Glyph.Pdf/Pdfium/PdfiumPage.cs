using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

internal sealed class PdfiumPage : IPdfPage
{
    public PdfiumPage(PdfiumDocument document, int index, double widthPoints, double heightPoints, int rotationDegrees)
    {
        Document = document;
        Index = index;
        WidthPoints = widthPoints;
        HeightPoints = heightPoints;
        RotationDegrees = rotationDegrees;
    }

    internal PdfiumDocument Document { get; }

    public int Index { get; }

    public double WidthPoints { get; }

    public double HeightPoints { get; }

    public int RotationDegrees { get; }

    internal FpdfPageT LoadNativePage()
    {
        Document.ThrowIfDisposed();
        var page = fpdfview.FPDF_LoadPage(Document.Handle, Index);
        if (page is null)
        {
            throw new InvalidOperationException($"Failed to load PDF page {Index}.");
        }

        return page;
    }
}
