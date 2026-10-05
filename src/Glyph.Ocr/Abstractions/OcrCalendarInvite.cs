using System.Globalization;
using System.Text;

namespace Glyph.Ocr.Abstractions;

/// <summary>
/// Builds a minimal .ics calendar invite from OCR date/time entities.
/// </summary>
public static class OcrCalendarInvite
{
    public static string BuildAllDayEvent(DateTime date, string summary = "Glyph OCR date")
    {
        var day = date.Date;
        var next = day.AddDays(1);
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Glyph//OCR//EN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine("UID:" + Guid.NewGuid().ToString("N") + "@glyph.local");
        sb.AppendLine("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
        sb.AppendLine("DTSTART;VALUE=DATE:" + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        sb.AppendLine("DTEND;VALUE=DATE:" + next.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        sb.AppendLine("SUMMARY:" + EscapeText(summary));
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    public static string BuildTimedEvent(DateTime startLocal, TimeSpan duration, string summary = "Glyph OCR event")
    {
        var end = startLocal + duration;
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Glyph//OCR//EN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine("UID:" + Guid.NewGuid().ToString("N") + "@glyph.local");
        sb.AppendLine("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
        sb.AppendLine("DTSTART:" + startLocal.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
        sb.AppendLine("DTEND:" + end.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
        sb.AppendLine("SUMMARY:" + EscapeText(summary));
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    public static bool TryParseDate(string value, out DateTime date)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    public static bool TryParseTime(string value, out TimeSpan time)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
        {
            time = dt.TimeOfDay;
            return true;
        }

        time = default;
        return false;
    }

    private static string EscapeText(string text)
        => text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
