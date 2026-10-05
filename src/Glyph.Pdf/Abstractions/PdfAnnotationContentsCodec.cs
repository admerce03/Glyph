namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Pure string codecs for Glyph-owned annotation Contents / custom keys.
/// Keeps callout, underline, ink-shape, and line-endpoint markers out of PDFium plumbing.
/// </summary>
public static class PdfAnnotationContentsCodec
{
    public const string GlyphLineEndsKey = "GlyphLineEnds";
    public const string GlyphUnderlineKey = "GlyphUnderline";
    public const string GlyphTextUnderlinePrefix = "GlyphTextUnderline:";
    public const string CalloutPointerPrefix = "CalloutPointer:";
    public const string CalloutPointerLegacy = "CalloutPointer";

    public static string FormatCalloutPointerContents(int calloutAnnotIndex) =>
        CalloutPointerPrefix + calloutAnnotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsCalloutPointerContents(string? contents) =>
        !string.IsNullOrEmpty(contents)
        && (contents.Equals(CalloutPointerLegacy, StringComparison.Ordinal)
            || contents.StartsWith(CalloutPointerPrefix, StringComparison.Ordinal));

    public static bool TryParseCalloutPointerOwner(string? contents, out int ownerIndex)
    {
        ownerIndex = -1;
        if (string.IsNullOrEmpty(contents)
            || !contents.StartsWith(CalloutPointerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(contents.AsSpan(CalloutPointerPrefix.Length), out ownerIndex);
    }

    public static string FormatTextUnderlineContents(int ownerAnnotIndex) =>
        GlyphTextUnderlinePrefix + ownerAnnotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsTextUnderlineContents(string? contents) =>
        !string.IsNullOrEmpty(contents)
        && (contents.Equals("GlyphTextUnderline", StringComparison.Ordinal)
            || contents.StartsWith(GlyphTextUnderlinePrefix, StringComparison.Ordinal));

    public static bool TryParseTextUnderlineOwner(string? contents, out int ownerIndex)
    {
        ownerIndex = -1;
        if (string.IsNullOrEmpty(contents)
            || !contents.StartsWith(GlyphTextUnderlinePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(contents.AsSpan(GlyphTextUnderlinePrefix.Length), out ownerIndex);
    }

    public static string FormatLineContents(PdfInkLineStyle line) =>
        line == PdfInkLineStyle.Solid ? "Line" : $"Line|{line}";

    public static string FormatArrowContents(PdfArrowheadStyle head, PdfInkLineStyle line)
    {
        if (head == PdfArrowheadStyle.Open && line == PdfInkLineStyle.Solid)
        {
            return "Arrow";
        }

        if (line == PdfInkLineStyle.Solid)
        {
            return $"Arrow|{head}";
        }

        if (head == PdfArrowheadStyle.Open)
        {
            return $"Arrow|{line}";
        }

        return $"Arrow|{head}|{line}";
    }

    public static PdfShapeKind? FromInkShapeContents(string? contents)
    {
        if (string.IsNullOrEmpty(contents))
        {
            return null;
        }

        if (contents.Equals("Line", StringComparison.Ordinal)
            || contents.StartsWith("Line|", StringComparison.Ordinal))
        {
            return PdfShapeKind.Line;
        }

        if (contents.Equals("Arrow", StringComparison.Ordinal)
            || contents.StartsWith("Arrow|", StringComparison.Ordinal))
        {
            return PdfShapeKind.Arrow;
        }

        return contents switch
        {
            "Freeform" => PdfShapeKind.Freeform,
            "Star" => PdfShapeKind.Star,
            "Polygon" => PdfShapeKind.Polygon,
            "SpeechBubble" => PdfShapeKind.SpeechBubble,
            _ => null,
        };
    }

    public static string FormatLineEndpoints(PdfPagePoint start, PdfPagePoint end) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{start.X:0.###},{start.Y:0.###},{end.X:0.###},{end.Y:0.###}");

    public static bool TryParseLineEndpoints(string? raw, out PdfPagePoint start, out PdfPagePoint end)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Split(',');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x1)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y1)
            || !double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x2)
            || !double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y2))
        {
            return false;
        }

        start = new PdfPagePoint(x1, y1);
        end = new PdfPagePoint(x2, y2);
        return true;
    }

    public static void ParseLineOrArrowStyle(
        string? contents,
        out PdfShapeKind kind,
        out PdfInkLineStyle lineStyle,
        out PdfArrowheadStyle arrowhead)
    {
        kind = PdfShapeKind.Line;
        lineStyle = PdfInkLineStyle.Solid;
        arrowhead = PdfArrowheadStyle.Open;
        if (string.IsNullOrEmpty(contents))
        {
            return;
        }

        if (contents.Equals("Arrow", StringComparison.Ordinal)
            || contents.StartsWith("Arrow|", StringComparison.Ordinal))
        {
            kind = PdfShapeKind.Arrow;
            var parts = contents.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts.Skip(1))
            {
                if (Enum.TryParse<PdfArrowheadStyle>(part, ignoreCase: true, out var head))
                {
                    arrowhead = head;
                }
                else if (Enum.TryParse<PdfInkLineStyle>(part, ignoreCase: true, out var line))
                {
                    lineStyle = line;
                }
            }

            return;
        }

        if (contents.Equals("Line", StringComparison.Ordinal)
            || contents.StartsWith("Line|", StringComparison.Ordinal))
        {
            kind = PdfShapeKind.Line;
            var parts = contents.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 1 && Enum.TryParse<PdfInkLineStyle>(parts[1], ignoreCase: true, out var line))
            {
                lineStyle = line;
            }
        }
    }
}
