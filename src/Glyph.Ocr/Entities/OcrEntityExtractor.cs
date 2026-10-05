using System.Globalization;
using System.Text.RegularExpressions;
using Glyph.Ocr.Abstractions;

namespace Glyph.Ocr.Entities;

/// <summary>
/// Offline actionable-entity extraction from OCR/plain text.
/// </summary>
public static partial class OcrEntityExtractor
{
    public static IReadOnlyList<OcrEntity> Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var entities = new List<OcrEntity>();
        foreach (Match match in UrlRegex().Matches(text))
        {
            entities.Add(new OcrEntity(OcrEntityKind.Url, match.Value, match.Index, match.Length));
        }

        foreach (Match match in EmailRegex().Matches(text))
        {
            entities.Add(new OcrEntity(OcrEntityKind.Email, match.Value, match.Index, match.Length));
        }

        foreach (Match match in PhoneRegex().Matches(text))
        {
            entities.Add(new OcrEntity(OcrEntityKind.Phone, match.Value, match.Index, match.Length));
        }

        foreach (Match match in DateRegex().Matches(text))
        {
            if (DateTime.TryParse(match.Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _)
                || DateTime.TryParse(match.Value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out _))
            {
                entities.Add(new OcrEntity(OcrEntityKind.Date, match.Value, match.Index, match.Length));
            }
        }

        return entities
            .OrderBy(e => e.StartIndex)
            .ThenBy(e => e.Kind)
            .ToList();
    }

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?:\+?\d{1,3}[\s\-.]?)?(?:\(?\d{3}\)?[\s\-.]?)\d{3}[\s\-.]?\d{4}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?:\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}|\d{4}[/\-.]\d{1,2}[/\-.]\d{1,2})\b")]
    private static partial Regex DateRegex();
}
