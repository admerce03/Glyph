namespace Glyph.Core.Pdf;

/// <summary>
/// PDF optimize capabilities deferred on ADR-016 (F24-11 / F24-14).
/// </summary>
public static class PdfOptimizeDeferredPolicy
{
    public const bool FontSubsettingSupported = false;
    public const bool LinearizeFastWebViewSupported = false;

    public const string Adr = "ADR-016";

    public const string Reason =
        "PDFium exposes no font-subset API or linearize flag in the current binding.";
}
