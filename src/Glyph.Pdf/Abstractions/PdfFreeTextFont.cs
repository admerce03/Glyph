namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Standard PDF base-font family for FreeText default appearance (<c>/DA</c>).
/// Resource names match common Acrobat short names (Helv/HeBo/…).
/// </summary>
public enum PdfFreeTextFontFamily
{
    Helvetica = 0,
    Times = 1,
    Courier = 2,
}

/// <summary>
/// Resolves FreeText <c>/DA</c> font resource names for the standard 14 Type1 faces.
/// </summary>
public static class PdfFreeTextFont
{
    public static string ResolveResourceName(
        PdfFreeTextFontFamily family,
        bool bold = false,
        bool italic = false) =>
        (family, bold, italic) switch
        {
            (PdfFreeTextFontFamily.Helvetica, false, false) => "Helv",
            (PdfFreeTextFontFamily.Helvetica, true, false) => "HeBo",
            (PdfFreeTextFontFamily.Helvetica, false, true) => "HeOb",
            (PdfFreeTextFontFamily.Helvetica, true, true) => "HeBI",
            (PdfFreeTextFontFamily.Times, false, false) => "TiRo",
            (PdfFreeTextFontFamily.Times, true, false) => "TiBo",
            (PdfFreeTextFontFamily.Times, false, true) => "TiIt",
            (PdfFreeTextFontFamily.Times, true, true) => "TiBI",
            (PdfFreeTextFontFamily.Courier, false, false) => "Cour",
            (PdfFreeTextFontFamily.Courier, true, false) => "CoBo",
            (PdfFreeTextFontFamily.Courier, false, true) => "CoOb",
            (PdfFreeTextFontFamily.Courier, true, true) => "CoBI",
            _ => "Helv",
        };
}
