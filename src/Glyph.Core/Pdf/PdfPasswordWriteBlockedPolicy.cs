namespace Glyph.Core.Pdf;

/// <summary>
/// Password-write / permission-write blocked on ADR-015 (F23-02…07, F45-09).
/// </summary>
public static class PdfPasswordWriteBlockedPolicy
{
    public const bool CreatePasswordProtectedSupported = false;
    public const bool SetOpenPasswordSupported = false;
    public const bool SetOwnerPermissionsSupported = false;
    public const bool RemoveProtectionSupported = false;

    public const string Adr = "ADR-015";

    public const string Reason =
        "PDFium has no write-encrypt / permission-write API; needs ADR-015 approval for an alternate path.";
}
