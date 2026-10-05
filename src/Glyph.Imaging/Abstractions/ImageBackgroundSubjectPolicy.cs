namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Background remove / subject extract dialog labels (F29-04/07/08/10/11).
/// </summary>
public static class ImageBackgroundSubjectPolicy
{
    public const string DialogTitle = "Background / subject";
    public const string RemoveInPlace = "Remove background (edit in place)";
    public const string ExtractToClipboard = "Extract subject → clipboard PNG";
    public const string ExtractToFile = "Extract subject → save PNG";
    public const string TransparencyHint = " · Save/Convert to PNG/WebP to keep transparency.";
    public const string SaveCancelled = "Save subject cancelled.";

    public static bool FormatSupportsAlpha(string? extension)
    {
        var ext = (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        return ext is "png" or "webp" or "tif" or "tiff";
    }

    public static string SuggestedSubjectFileName(string baseName) =>
        $"{baseName}-subject.png";
}
