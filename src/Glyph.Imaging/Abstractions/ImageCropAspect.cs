namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Constrains a drag selection rectangle to a target aspect ratio (width/height).
/// </summary>
public static class ImageCropAspect
{
    /// <summary>
    /// Given drag start and current pointer, returns top-left + size constrained to
    /// <paramref name="aspectWidthOverHeight"/> when set; otherwise free rectangle.
    /// Clamped to the display bounds.
    /// </summary>
    public static (double X, double Y, double Width, double Height) Constrain(
        double startX,
        double startY,
        double currentX,
        double currentY,
        double displayWidth,
        double displayHeight,
        double? aspectWidthOverHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayWidth));
        }

        var rawW = currentX - startX;
        var rawH = currentY - startY;

        if (aspectWidthOverHeight is not { } aspect || aspect <= 0)
        {
            var x = Math.Max(0, Math.Min(startX, currentX));
            var y = Math.Max(0, Math.Min(startY, currentY));
            var right = Math.Min(displayWidth, Math.Max(startX, currentX));
            var bottom = Math.Min(displayHeight, Math.Max(startY, currentY));
            return (x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        }

        // Prefer the dominant drag axis; keep the other from aspect.
        double width;
        double height;
        if (Math.Abs(rawW) >= Math.Abs(rawH) * aspect)
        {
            width = Math.Abs(rawW);
            height = width / aspect;
            if (height > Math.Abs(rawH) && Math.Abs(rawH) > 1)
            {
                // If height would overshoot the drag, fit to height instead.
                height = Math.Abs(rawH);
                width = height * aspect;
            }
        }
        else
        {
            height = Math.Abs(rawH);
            width = height * aspect;
        }

        // Anchor at start; grow in the drag direction.
        var left = rawW >= 0 ? startX : startX - width;
        var top = rawH >= 0 ? startY : startY - height;

        // Clamp into display while preserving aspect.
        if (left < 0)
        {
            width += left;
            left = 0;
            height = width / aspect;
            if (rawH < 0)
            {
                top = startY - height;
            }
        }

        if (top < 0)
        {
            height += top;
            top = 0;
            width = height * aspect;
            if (rawW < 0)
            {
                left = startX - width;
            }
        }

        if (left + width > displayWidth)
        {
            width = displayWidth - left;
            height = width / aspect;
            if (rawH < 0)
            {
                top = startY - height;
                if (top < 0)
                {
                    height += top;
                    top = 0;
                    width = height * aspect;
                }
            }
        }

        if (top + height > displayHeight)
        {
            height = displayHeight - top;
            width = height * aspect;
            if (rawW < 0)
            {
                left = startX - width;
                if (left < 0)
                {
                    width += left;
                    left = 0;
                    height = width / aspect;
                }
            }
        }

        width = Math.Max(0, Math.Min(width, displayWidth - left));
        height = Math.Max(0, Math.Min(height, displayHeight - top));
        return (left, top, width, height);
    }
}
