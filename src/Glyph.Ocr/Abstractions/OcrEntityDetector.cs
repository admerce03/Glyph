using System.Globalization;
using System.Text.RegularExpressions;

namespace Glyph.Ocr.Abstractions;

/// <summary>
/// Detects actionable entities in OCR (or other) plain text.
/// </summary>
public static partial class OcrEntityDetector
{
    public static IReadOnlyList<OcrEntity> Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var found = new List<OcrEntity>();
        Collect(found, text, UrlRegex(), OcrEntityKind.Url);
        Collect(found, text, EmailRegex(), OcrEntityKind.Email);
        Collect(found, text, PhoneRegex(), OcrEntityKind.Phone);
        CollectAddresses(found, text);
        Collect(found, text, DateRegex(), OcrEntityKind.Date);
        Collect(found, text, TimeRegex(), OcrEntityKind.Time);

        return found
            .OrderBy(e => e.StartIndex)
            .ThenBy(e => e.Kind)
            .ToArray();
    }

    private static void Collect(
        List<OcrEntity> sink,
        string text,
        Regex regex,
        OcrEntityKind kind)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (!match.Success || match.Length == 0)
            {
                continue;
            }

            var value = match.Value.Trim().TrimEnd('.', ',', ';', ')');
            if (value.Length == 0)
            {
                continue;
            }

            if (kind == OcrEntityKind.Date
                && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _)
                && !DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out _))
            {
                continue;
            }

            sink.Add(new OcrEntity(kind, value, match.Index, value.Length));
        }
    }

    private static void CollectAddresses(List<OcrEntity> sink, string text)
    {
        var covered = new List<(int Start, int End)>();

        foreach (Match street in StreetAddressRegex().Matches(text))
        {
            if (!street.Success || street.Length == 0)
            {
                continue;
            }

            var start = street.Index;
            var end = street.Index + street.Length;
            var value = street.Value.Trim().TrimEnd('.', ',', ';');

            // Prefer a fuller "street + city, ST ZIP" when the city line follows nearby.
            var lookahead = text.AsSpan(end);
            var cityMatch = CityStateZipRegex().Match(text, end);
            if (cityMatch.Success
                && cityMatch.Index - end <= 48
                && text.AsSpan(end, cityMatch.Index - end).Trim().Length <= 8)
            {
                end = cityMatch.Index + cityMatch.Length;
                value = text[start..end].Trim().TrimEnd('.', ',', ';');
            }

            if (value.Length == 0)
            {
                continue;
            }

            sink.Add(new OcrEntity(OcrEntityKind.Address, value, start, value.Length));
            covered.Add((start, start + value.Length));
        }

        foreach (Match city in CityStateZipRegex().Matches(text))
        {
            if (!city.Success || city.Length == 0)
            {
                continue;
            }

            var start = city.Index;
            var end = city.Index + city.Length;
            if (covered.Any(c => start >= c.Start && end <= c.End))
            {
                continue;
            }

            var value = city.Value.Trim().TrimEnd('.', ',', ';');
            if (value.Length == 0)
            {
                continue;
            }

            sink.Add(new OcrEntity(OcrEntityKind.Address, value, start, value.Length));
        }
    }

    [GeneratedRegex(@"https?://[^\s<>""']+|www\.[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?:\+?\d{1,3}[\s\-.]*)?(?:\(?\d{2,4}\)?[\s\-.]*)?\d{3,4}[\s\-.]*\d{3,4}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(
        @"\b\d{1,6}\s+(?:[A-Za-z0-9][A-Za-z0-9.'\-]*\s+){0,6}(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Drive|Dr|Lane|Ln|Court|Ct|Way|Place|Pl|Highway|Hwy|Parkway|Pkwy|Circle|Cir|Terrace|Ter|Trail|Trl)\.?\b(?:\s*(?:Suite|Ste|Apt|Unit|#)\.?\s*[A-Za-z0-9\-]+)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StreetAddressRegex();

    [GeneratedRegex(
        @"\b[A-Z][A-Za-z]+(?:[\s\-]+[A-Z][A-Za-z]+)*,\s*[A-Z]{2}\s+\d{5}(?:-\d{4})?\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex CityStateZipRegex();

    [GeneratedRegex(@"\b(?:\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}|\d{4}[/\-.]\d{1,2}[/\-.]\d{1,2}|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{1,2},?\s+\d{2,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\b(?:[01]?\d|2[0-3]):[0-5]\d(?:\s*[AaPp][Mm])?\b", RegexOptions.CultureInvariant)]
    private static partial Regex TimeRegex();
}
