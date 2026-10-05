namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Luminance histogram bins for the Adjust dialog (F33-16).
/// </summary>
public static class ImageLuminanceHistogram
{
    public const int DefaultBinCount = 64;

    /// <summary>
    /// Builds luminance bins from tightly packed BGRA32 pixels.
    /// </summary>
    public static int[] BuildBins(ReadOnlySpan<byte> bgra, int binCount = DefaultBinCount)
    {
        if (binCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(binCount));
        }

        var bins = new int[binCount];
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            // Rec.601-ish integer luma from B,G,R.
            var lum = (bgra[i] * 29 + bgra[i + 1] * 150 + bgra[i + 2] * 77) / 256;
            bins[Math.Clamp(lum * binCount / 256, 0, binCount - 1)]++;
        }

        return bins;
    }
}
