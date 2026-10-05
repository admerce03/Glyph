namespace Glyph.Core.Documents;

/// <summary>
/// PDF thumbnail strip width limits (F03-01 / F04-13).
/// </summary>
public static class ThumbnailWidthConstraints
{
    public const double Default = 108;
    public const double Min = 72;
    public const double Max = 180;

    public static double Clamp(double width) => Math.Clamp(width, Min, Max);
}
