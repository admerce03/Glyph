namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Folder batch progress dialog labels (F36-09).
/// </summary>
public static class BatchProgressUi
{
    public const string CancelHint = "Cancel stops after the current file.";
    public const string CloseButton = "Cancel";
    public const string BatchResize = "Batch resize";
    public const string BatchConvert = "Batch convert";
    public const string BatchStrip = "Batch strip";
    public const string BatchRename = "Batch rename";
    public const string StampPlacementHint = "Places at selection top-left (or 0,0).";
    public const string StampPlacementHintWithUndo =
        "Places at selection top-left (or 0,0). Undo with Ctrl+Z.";

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

    public static string FormatAppliesToFolder(int imageCount) =>
        $"Applies to {imageCount} images in this folder. Convert/rename write new files; orientation/strip/profile overwrite originals.";
}
