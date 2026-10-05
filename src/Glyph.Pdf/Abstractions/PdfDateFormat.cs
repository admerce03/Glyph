using System.Globalization;

namespace Glyph.Pdf.Abstractions;

/// <summary>
/// PDF date string formatting for Info/annot timestamps (F15-11 / Info ModDate).
/// </summary>
public static class PdfDateFormat
{
    public static string Format(DateTimeOffset value) =>
        "D:" + value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
}
