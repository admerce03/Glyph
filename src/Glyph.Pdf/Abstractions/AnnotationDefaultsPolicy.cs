namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Resolve Preferences annotation author / default highlight+stroke / stroke width (F55-10 / F55-18 / F55-19).
/// </summary>
public static class AnnotationDefaultsPolicy
{
    public const double MinStrokeWidthPoints = 0.5;
    public const double MaxStrokeWidthPoints = 12;

    public static string ResolveAuthor(string? preferred, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? Environment.UserName : fallback;
    }

    public static float ClampStrokeWidthPoints(double points) =>
        (float)Math.Clamp(points, MinStrokeWidthPoints, MaxStrokeWidthPoints);

    public static bool TryResolveHighlight(string? name, out PdfAnnotationColor color)
    {
        var match = PdfAnnotationColor.HighlightPresets
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(match.Name))
        {
            color = default;
            return false;
        }

        color = match.Color;
        return true;
    }

    public static bool TryResolveStroke(string? name, out PdfAnnotationColor color)
    {
        var match = PdfAnnotationColor.StrokePresets
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(match.Name))
        {
            color = default;
            return false;
        }

        color = match.Color;
        return true;
    }
}
