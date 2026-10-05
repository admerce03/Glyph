namespace Glyph.Core.Pdf;

/// <summary>
/// UI copy for password-protect / permission-write (F23-02…07) while ADR-015 blocks the write path.
/// </summary>
public static class PdfSecurityWriteUiCopy
{
    public const string ToolbarLabel = "Protect";
    public const string ToolbarTooltip = "Password-protect / permissions (blocked — ADR-015)";
    public const string DialogTitle = "PDF password protection";
    public const string CloseButton = "Close";
    public const string StatusBlocked = "Password-protect write blocked (ADR-015).";

    public static string DialogBody() =>
        PdfPasswordWriteBlockedPolicy.Reason
        + "\n\nProduct choice required (ADR-015): Accept A (PdfSharp MIT), C (commercial SDK), or D (keep blocked)."
        + "\n\nOpening encrypted PDFs and viewing encryption/permissions (Info) remain available.";
}
