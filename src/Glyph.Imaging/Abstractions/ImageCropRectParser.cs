namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Parses numeric crop box text <c>x,y,w,h</c> (F30-05).
/// </summary>
public static class ImageCropRectParser
{
    /// <summary>
    /// Tries to parse comma-separated integers into an <see cref="ImageRect"/>.
    /// Requires positive width and height.
    /// </summary>
    public static ImageRect? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4
            || !int.TryParse(parts[0], out var x)
            || !int.TryParse(parts[1], out var y)
            || !int.TryParse(parts[2], out var w)
            || !int.TryParse(parts[3], out var h))
        {
            return null;
        }

        if (w <= 0 || h <= 0)
        {
            return null;
        }

        return new ImageRect(x, y, w, h);
    }
}
