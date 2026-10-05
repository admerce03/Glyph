namespace Glyph.Core.Ocr;

/// <summary>
/// OCR overlay layer contract (F08-01/11): recognition on demand; page pixels stay underneath.
/// </summary>
public static class OcrLayerPolicy
{
    public const bool DetectOnDemand = true;
    public const bool PreserveImageUnderOverlay = true;

    public static IReadOnlyList<string> DeclaredBehaviors { get; } =
    [
        "Toolbar OCR runs Windows.Media.Ocr on demand",
        "Full-bleed page render remains visible under OCR word boxes",
        "OCR overlay visuals are translucent selection chrome only",
    ];
}
