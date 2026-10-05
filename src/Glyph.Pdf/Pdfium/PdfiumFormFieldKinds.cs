namespace Glyph.Pdf.Pdfium;

/// <summary>
/// <c>FPDF_FORMFIELD_*</c> / <c>FORMTYPE_*</c> values (not exported as named constants by PDFiumCore).
/// </summary>
internal static class PdfiumFormFieldKinds
{
    public const int Unknown = 0;
    public const int PushButton = 1;
    public const int CheckBox = 2;
    public const int RadioButton = 3;
    public const int ComboBox = 4;
    public const int ListBox = 5;
    public const int TextField = 6;
    public const int Signature = 7;
}

internal static class PdfiumFormTypes
{
    public const int None = 0;
    public const int AcroForm = 1;
    public const int XfaFull = 2;
    public const int XfaForeground = 3;
}
