namespace Glyph.Core.Pdf;

/// <summary>
/// Password-write / permission-write policy (F23-02…07, F45-09).
/// ADR-015 Accept A (PdfSharp MIT) enables the write path.
/// </summary>
public static class PdfPasswordWriteBlockedPolicy
{
    public const bool CreatePasswordProtectedSupported = true;
    public const bool SetOpenPasswordSupported = true;
    public const bool SetOwnerPermissionsSupported = true;
    public const bool RemoveProtectionSupported = true;

    public const string Adr = "ADR-015";

    public const string AcceptedOption = "A";

    public const string Reason =
        "ADR-015 Accept A — PdfSharp MIT write-encrypt behind IPdfSecurityService.";
}
