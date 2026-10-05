namespace Glyph.Imaging.Abstractions;

/// <summary>Small pixel-buffer helpers shared by viewers (print, export).</summary>
public static class ImagePixelOps
{
    /// <summary>Convert tightly packed BGRA32 to approximate luminance grayscale in place.</summary>
    public static void ApplyGrayscale(Span<byte> bgra)
    {
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            var y = (byte)((bgra[i] * 29 + bgra[i + 1] * 150 + bgra[i + 2] * 77) >> 8);
            bgra[i] = y;
            bgra[i + 1] = y;
            bgra[i + 2] = y;
        }
    }
}
