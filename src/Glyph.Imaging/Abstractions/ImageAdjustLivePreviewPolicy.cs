namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Color-adjust dialog live preview via checkpoint clone/restore (F33-15).
/// </summary>
public static class ImageAdjustLivePreviewPolicy
{
    public const bool UsesCheckpointPreview = true;
    public const string PreviewHint = "Live preview uses a temporary checkpoint restore.";

    public static bool ShouldRestoreBaselineOnCancel(bool previewApplied) => previewApplied;
}
