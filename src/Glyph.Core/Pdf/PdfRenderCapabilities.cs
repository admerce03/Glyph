namespace Glyph.Core.Pdf;

/// <summary>
/// Declares what the PDFium render path is expected to cover (F04-02..05).
/// Verified indirectly by open/render integration tests; this catalogs the contract.
/// </summary>
public static class PdfRenderCapabilities
{
    public const bool VectorContent = true;
    public const bool EmbeddedImages = true;
    public const bool EmbeddedFonts = true;
    public const bool Transparency = true;

    public static IReadOnlyList<string> DeclaredCapabilities { get; } =
    [
        nameof(VectorContent),
        nameof(EmbeddedImages),
        nameof(EmbeddedFonts),
        nameof(Transparency),
    ];
}
