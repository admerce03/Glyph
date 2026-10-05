namespace Glyph.Core.Platform;

/// <summary>
/// Expected Win32 DPI awareness declaration for F02-18 / F02-20.
/// Verified against <c>src/Glyph.App/app.manifest</c> in unit tests.
/// </summary>
public static class DpiAwarenessDeclaration
{
    public const string ManifestRelativePath = "src/Glyph.App/app.manifest";
    public const string ExpectedDpiAwareness = "PerMonitorV2, PerMonitor";
    public const string ExpectedDpiAware = "true/pm";

    public static bool ManifestDeclaresPerMonitorV2(string manifestXml) =>
        !string.IsNullOrWhiteSpace(manifestXml)
        && manifestXml.Contains(ExpectedDpiAwareness, StringComparison.Ordinal)
        && manifestXml.Contains(ExpectedDpiAware, StringComparison.Ordinal);
}
