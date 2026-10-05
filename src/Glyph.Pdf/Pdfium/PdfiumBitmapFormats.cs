namespace Glyph.Pdf.Pdfium;

/// <summary>
/// PDFium bitmap format constants used with <c>FPDFBitmap_CreateEx</c>.
/// </summary>
internal static class PdfiumBitmapFormats
{
    public const int Unknown = 0;
    public const int Gray = 1;
    public const int Bgr = 2;
    public const int Bgrx = 3;
    public const int Bgra = 4;
}
