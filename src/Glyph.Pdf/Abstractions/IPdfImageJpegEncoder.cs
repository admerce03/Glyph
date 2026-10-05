namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Optional JPEG encoder used by PDF optimize to rewrite downsampled images.
/// </summary>
public interface IPdfImageJpegEncoder
{
    /// <summary>
    /// Encode tightly packed BGRA32 pixels as a JPEG. Returns null to fall back to SetBitmap.
    /// </summary>
    byte[]? EncodeBgraToJpeg(ReadOnlySpan<byte> bgra, int width, int height, int quality);
}
