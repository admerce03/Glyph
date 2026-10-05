namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Point in PDF page space (points, origin bottom-left).
/// </summary>
public readonly record struct PdfPagePoint(double X, double Y);
