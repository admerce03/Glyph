namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Folder batch progress dialog labels (F36-09).
/// </summary>
public static class BatchProgressUi
{
    public const string CancelHint = "Cancel stops after the current file.";
    public const string CloseButton = "Cancel";

    public static string ProgressLabel(int completedOrCurrent, int total) =>
        $"{completedOrCurrent} / {total}";

    public static string ProgressWithFile(int oneBasedIndex, int total, string fileName) =>
        $"{oneBasedIndex} / {total} · {fileName}";

    public static string CancelledStatus(string operation, int updatedCount) =>
        $"{operation} cancelled after {updatedCount} file(s).";

    public static string FormatConvertWrote(string format, int converted) =>
        $"Batch convert → {format}: wrote {converted} file(s).";

    public static string FormatStripMetadata(int strippedCount) =>
        $"Batch strip metadata: updated {strippedCount} folder image(s).";

    public static string FormatRenamed(int renamed) =>
        $"Batch rename: renamed {renamed} file(s).";
}
