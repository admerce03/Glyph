namespace Glyph.Core.Documents;

/// <summary>
/// Timestamped capture file names for camera / scanner imports (F43-02).
/// </summary>
public static class CaptureFileName
{
    public static string CameraPng(DateTime localNow) =>
        $"Camera-{localNow:yyyyMMdd-HHmmss}.png";

    public static string ScanPdf(DateTime localNow) =>
        $"Scan-{localNow:yyyyMMdd-HHmmss}.pdf";

    public static string ScanInsertTempPdf() =>
        "glyph-scan-insert-" + Guid.NewGuid().ToString("N") + ".pdf";
}
