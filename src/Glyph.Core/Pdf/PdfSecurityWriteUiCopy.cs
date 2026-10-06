namespace Glyph.Core.Pdf;

/// <summary>
/// UI copy for password-protect / permission-write (F23-02…07).
/// </summary>
public static class PdfSecurityWriteUiCopy
{
    public const string ToolbarLabel = "Protect";
    public const string ToolbarTooltip = "Password-protect / permissions";
    public const string ToolbarTooltipBlocked = "Password-protect / permissions (blocked — ADR-015)";
    public const string DialogTitle = "PDF password protection";
    public const string CloseButton = "Close";
    public const string ApplyOpenPassword = "Apply";
    public const string RemoveProtection = "Remove protection";
    public const string OpenPasswordHeader = "Open password";
    public const string OwnerPasswordHeader = "Owner password (optional for open-password; required for permissions-only)";
    public const string RestrictCopyLabel = "Restrict copying / extraction";
    public const string RestrictPrintLabel = "Restrict printing";
    public const string RestrictEditLabel = "Restrict editing / annotations";
    public const string StatusBlocked = "Password-protect write blocked (ADR-015).";
    public const string StatusApplied = "Open password applied.";
    public const string StatusPermissionsApplied = "Owner password / permissions applied.";
    public const string StatusRemoved = "Password protection removed.";
    public const string StatusFailedPrefix = "Password-protect failed: ";

    public static string DialogBody() =>
        PdfPasswordWriteBlockedPolicy.Reason
        + "\n\nSet an open password, or owner-only permissions. Opening encrypted PDFs and Info remain available.";

    public static string DialogBodyBlocked() =>
        "PDFium has no write-encrypt / permission-write API; needs ADR-015 approval for an alternate path."
        + "\n\nProduct choice required (ADR-015): Accept A (PdfSharp MIT), C (commercial SDK), or D (keep blocked)."
        + "\n\nOpening encrypted PDFs and viewing encryption/permissions (Info) remain available.";
}
