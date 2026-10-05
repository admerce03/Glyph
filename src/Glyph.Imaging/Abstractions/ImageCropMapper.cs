namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Maps a selection drawn on a displayed (possibly downscaled) image back to
/// document pixel coordinates for crop.
/// </summary>
public static class ImageCropMapper
{
    public static ImageRect ToDocumentPixels(
        double selectionX,
        double selectionY,
        double selectionWidth,
        double selectionHeight,
        double displayWidth,
        double displayHeight,
        int documentWidth,
        int documentHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0 || documentWidth <= 0 || documentHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayWidth));
        }

        if (selectionWidth <= 0 || selectionHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionWidth));
        }

        var scaleX = documentWidth / displayWidth;
        var scaleY = documentHeight / displayHeight;

        var x = (int)Math.Floor(Math.Min(selectionX, selectionX + selectionWidth) * scaleX);
        var y = (int)Math.Floor(Math.Min(selectionY, selectionY + selectionHeight) * scaleY);
        var right = (int)Math.Ceiling(Math.Max(selectionX, selectionX + selectionWidth) * scaleX);
        var bottom = (int)Math.Ceiling(Math.Max(selectionY, selectionY + selectionHeight) * scaleY);

        x = Math.Clamp(x, 0, documentWidth - 1);
        y = Math.Clamp(y, 0, documentHeight - 1);
        right = Math.Clamp(right, x + 1, documentWidth);
        bottom = Math.Clamp(bottom, y + 1, documentHeight);

        return new ImageRect(x, y, right - x, bottom - y);
    }
}
