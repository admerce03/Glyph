namespace Glyph.Pdf.Abstractions;

public interface IPdfPage
{
    int Index { get; }

    /// <summary>Width in PDF points.</summary>
    double WidthPoints { get; }

    /// <summary>Height in PDF points.</summary>
    double HeightPoints { get; }

    int RotationDegrees { get; }
}
