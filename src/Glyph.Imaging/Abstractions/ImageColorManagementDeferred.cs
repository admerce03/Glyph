namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Deferred color-management capabilities (F39-05 / F39-08).
/// </summary>
public static class ImageColorManagementDeferred
{
    public const bool MonitorProfileSupported = false;
    public const bool GamutWarningOverlaySupported = false;

    public const string MonitorProfileReason =
        "Needs WinUI/monitor ICC plumbing; display currently transforms to sRGB.";

    public const string GamutWarningReason =
        "Needs gamut visualization overlay; soft-proof Adobe RGB is the interim path.";
}
