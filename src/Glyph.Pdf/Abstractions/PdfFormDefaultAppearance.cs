using System.Globalization;
using System.Text.RegularExpressions;

namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Helpers for AcroForm widget <c>/DA</c> (default appearance) strings.
/// Font size <c>0</c> is the PDF convention for automatic sizing to fit the field.
/// </summary>
public static partial class PdfFormDefaultAppearance
{
    [GeneratedRegex(
        @"/(?<font>[A-Za-z0-9_+,-]+)\s+(?<size>-?\d+(?:\.\d+)?)\s+Tf",
        RegexOptions.CultureInvariant)]
    private static partial Regex FontSizePattern();

    /// <summary>
    /// Returns the <c>Tf</c> size from a DA string, or null when absent/unparseable.
    /// </summary>
    public static float? TryGetFontSize(string? da)
    {
        if (string.IsNullOrWhiteSpace(da))
        {
            return null;
        }

        var match = FontSizePattern().Match(da);
        if (!match.Success)
        {
            return null;
        }

        return float.TryParse(
            match.Groups["size"].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var size)
            ? size
            : null;
    }

    /// <summary>
    /// True when DA requests automatic font sizing (<c>0 Tf</c>).
    /// </summary>
    public static bool UsesAutoFontSize(string? da) =>
        TryGetFontSize(da) is 0f;

    /// <summary>
    /// Rewrites the <c>Tf</c> size in <paramref name="da"/>, preserving font resource and trailing operators.
    /// When DA is empty, synthesizes a Helvetica gray appearance.
    /// </summary>
    public static string WithFontSize(string? da, float fontSizePoints)
    {
        if (fontSizePoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSizePoints));
        }

        var sizeText = fontSizePoints.ToString("0.##", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(da))
        {
            return $"/Helv {sizeText} Tf 0 g";
        }

        var match = FontSizePattern().Match(da);
        if (!match.Success)
        {
            return $"/{ExtractFontResource(da)} {sizeText} Tf {StripLeadingFontOps(da)}".Trim();
        }

        return FontSizePattern().Replace(
            da,
            m => $"/{m.Groups["font"].Value} {sizeText} Tf",
            count: 1);
    }

    /// <summary>
    /// Sets font size to <c>0</c> so viewers auto-fit text to the field rect.
    /// </summary>
    public static string WithAutoFontSize(string? da) => WithFontSize(da, 0f);

    /// <summary>
    /// Estimates a concrete point size that fits <paramref name="text"/> in <paramref name="bounds"/>.
    /// Used when a viewer cannot honor <c>0 Tf</c>; prefer <see cref="WithAutoFontSize"/> for durable fills.
    /// </summary>
    public static float ComputeFitSize(PdfRect bounds, string? text, bool multiline = false)
    {
        var width = (float)Math.Abs(bounds.Right - bounds.Left);
        var height = (float)Math.Abs(bounds.Top - bounds.Bottom);
        if (width < 1f || height < 1f)
        {
            return 12f;
        }

        // Leave a small inset so glyphs clear the border.
        var maxByHeight = Math.Max(4f, height * 0.72f);
        if (string.IsNullOrEmpty(text))
        {
            return Math.Min(12f, maxByHeight);
        }

        if (multiline)
        {
            var lines = Math.Max(1, text.Split('\n').Length);
            // Approximate wrapped lines when there are no hard breaks.
            var approxCharsPerLine = Math.Max(8, (int)(width / 6f));
            var wrapped = Math.Max(lines, (int)Math.Ceiling(text.Length / (double)approxCharsPerLine));
            var lineHeight = height / wrapped;
            return Math.Clamp(lineHeight * 0.85f, 4f, maxByHeight);
        }

        // Helvetica average glyph width ≈ 0.5 × size.
        var maxByWidth = width / Math.Max(1f, text.Length * 0.5f);
        return Math.Clamp(Math.Min(maxByHeight, maxByWidth), 4f, 72f);
    }

    private static string ExtractFontResource(string da)
    {
        var slash = da.IndexOf('/');
        if (slash < 0)
        {
            return "Helv";
        }

        var i = slash + 1;
        while (i < da.Length && !char.IsWhiteSpace(da[i]))
        {
            i++;
        }

        return i > slash + 1 ? da[(slash + 1)..i] : "Helv";
    }

    private static string StripLeadingFontOps(string da)
    {
        var tf = da.IndexOf("Tf", StringComparison.Ordinal);
        if (tf < 0)
        {
            return "0 g";
        }

        var rest = da[(tf + 2)..].Trim();
        return string.IsNullOrEmpty(rest) ? "0 g" : rest;
    }
}
