namespace Glyph.Core.Signatures;

/// <summary>
/// Keys near-white paper backgrounds to transparent for webcam/scanned signature photos (F19-02).
/// </summary>
public static class SignaturePaperKeying
{
    /// <summary>
    /// Default luminance threshold (0–255). Pixels at or above this become fully transparent.
    /// Soft falloff below threshold preserves ink antialiasing.
    /// </summary>
    public const byte DefaultWhiteThreshold = 235;

    /// <summary>
    /// Soft edge width below <paramref name="whiteThreshold"/> where alpha ramps from 0→255.
    /// </summary>
    public const byte DefaultSoftness = 28;

    /// <summary>
    /// Mutates a BGRA32 buffer in place: near-white paper → transparent, ink kept opaque.
    /// </summary>
    public static void KeyOutNearWhite(
        Span<byte> bgra,
        int width,
        int height,
        byte whiteThreshold = DefaultWhiteThreshold,
        byte softness = DefaultSoftness)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var expected = checked(width * height * 4);
        if (bgra.Length < expected)
        {
            throw new ArgumentException($"BGRA buffer length {bgra.Length} is shorter than {expected}.", nameof(bgra));
        }

        if (softness == 0)
        {
            softness = 1;
        }

        var softStart = Math.Max(0, whiteThreshold - softness);
        for (var i = 0; i < expected; i += 4)
        {
            var b = bgra[i];
            var g = bgra[i + 1];
            var r = bgra[i + 2];
            // Rec. 601 luma approximation.
            var luma = (byte)((r * 299 + g * 587 + b * 114 + 500) / 1000);
            if (luma >= whiteThreshold)
            {
                bgra[i + 3] = 0;
            }
            else if (luma > softStart)
            {
                // Ramp alpha: softStart → 255, whiteThreshold → 0.
                var t = (luma - softStart) / (double)softness;
                bgra[i + 3] = (byte)Math.Clamp(255.0 * (1.0 - t), 0, 255);
            }
            // else keep existing alpha (typically 255 from camera)
        }
    }
}
