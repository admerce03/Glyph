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
}
