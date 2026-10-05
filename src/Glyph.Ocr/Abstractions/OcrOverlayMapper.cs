namespace Glyph.Ocr.Abstractions;

/// <summary>
/// Maps OCR word boxes from the recognition bitmap into the on-screen display bitmap.
/// </summary>
public static class OcrOverlayMapper
{
    public static (double X, double Y, double Width, double Height) MapToDisplay(
        OcrWord word,
        int sourceWidth,
        int sourceHeight,
        int displayWidth,
        int displayHeight)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayHeight);

        var scaleX = displayWidth / (double)sourceWidth;
        var scaleY = displayHeight / (double)sourceHeight;
        return (
            word.X * scaleX,
            word.Y * scaleY,
            Math.Max(1, word.Width * scaleX),
            Math.Max(1, word.Height * scaleY));
    }
}
