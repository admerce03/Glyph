namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Axis-aligned crop rectangle in PDF user space (points), matching PDFium CropBox
/// ordering: left, bottom, right, top.
/// </summary>
public readonly record struct PdfCropBox(double Left, double Bottom, double Right, double Top)
{
    public double Width => Math.Max(0, Right - Left);

    public double Height => Math.Max(0, Top - Bottom);

    public bool IsValid => Right > Left && Top > Bottom;
}

/// <summary>
/// Inset margins applied against a page's MediaBox (or current CropBox) to produce a new CropBox.
/// </summary>
public readonly record struct PdfCropMargins(
    double LeftPoints,
    double TopPoints,
    double RightPoints,
    double BottomPoints)
{
    public static PdfCropMargins Uniform(double points) => new(points, points, points, points);
}
