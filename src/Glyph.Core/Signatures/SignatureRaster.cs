namespace Glyph.Core.Signatures;

/// <summary>
/// BGRA32 raster of a signature stroke plus the content size in PDF points
/// (excluding padding) for placement.
/// </summary>
public sealed record SignatureRaster(
    byte[] BgraPixels,
    int PixelWidth,
    int PixelHeight,
    double ContentWidthPoints,
    double ContentHeightPoints,
    double PaddingPoints);
