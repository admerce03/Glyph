namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Soft-proof / color-managed display status (F39-02/06).
/// </summary>
public static class ImageColorManagedDisplayPolicy
{
    public const string SoftProofLabel = "Soft-proof Adobe RGB";
    public const string SoftProofOn = "Soft-proof Adobe RGB on.";
    public const string SoftProofOff = "Soft-proof off.";
    public const string ColorManagedOn = "Color-managed display on.";
    public const string ColorManagedOff = "Color-managed display off.";
    public const string ColorManagedLabel = "Color-managed display (ICC → sRGB)";
}
