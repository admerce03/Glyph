namespace Glyph.Core.Signatures;

/// <summary>
/// Visual signature stamp placement into form signature widgets (F20-09).
/// PKCS#7 cryptographically signed fields are out of scope (visual stamps only).
/// </summary>
public static class SignatureFormPlacementPolicy
{
    public const bool PlacesVisualStampInFieldBounds = true;
    public const bool CryptographicPkcs7Supported = false;

    public static bool IsSignatureWidget(string? fieldKindName) =>
        string.Equals(fieldKindName, "Signature", StringComparison.OrdinalIgnoreCase);
}
