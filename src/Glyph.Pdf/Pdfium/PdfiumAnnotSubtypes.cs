namespace Glyph.Pdf.Pdfium;

/// <summary>
/// PDFium <c>FPDF_ANNOTATION_SUBTYPE</c> values (not exported as named constants by PDFiumCore).
/// </summary>
internal static class PdfiumAnnotSubtypes
{
    public const int Unknown = 0;
    public const int Text = 1;
    public const int Link = 2;
    public const int FreeText = 3;
    public const int Line = 4;
    public const int Square = 5;
    public const int Circle = 6;
    public const int Polygon = 7;
    public const int Polyline = 8;
    public const int Highlight = 9;
    public const int Underline = 10;
    public const int Squiggly = 11;
    public const int StrikeOut = 12;
}
