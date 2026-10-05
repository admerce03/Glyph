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
    public const int Stamp = 13;
    public const int Caret = 14;
    public const int Ink = 15;
    public const int Popup = 16;
    public const int FileAttachment = 17;
    public const int Sound = 18;
    public const int Movie = 19;
    public const int Widget = 20;
    public const int Screen = 21;
    public const int PrinterMark = 22;
    public const int TrapNet = 23;
    public const int Watermark = 24;
    public const int ThreeD = 25;
    public const int RichMedia = 26;
}

/// <summary>
/// <c>FLAT_*</c> / <c>FLATTEN_*</c> values for <c>FPDFPage_Flatten</c> (not exported by PDFiumCore).
/// </summary>
internal static class PdfiumFlattenFlags
{
    public const int FlatNormalDisplay = 0;
    public const int FlatPrint = 1;

    public const int FlattenFail = 0;
    public const int FlattenSuccess = 1;
    public const int FlattenNothingToDo = 2;
}
