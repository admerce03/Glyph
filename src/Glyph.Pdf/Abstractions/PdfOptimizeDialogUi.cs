using Glyph.Core.Documents;

namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Optimize PDF dialog labels / preset combo (F24).
/// </summary>
public static class PdfOptimizeDialogUi
{
    public const string DialogTitle = "Optimize PDF";
    public const string ApplyButton = DialogButtons.Apply;
    public const string CancelledStatus = "Optimize cancelled.";
    public const string OptimizingStatus = "Optimizing…";
    public const string FailedPrefix = "Optimize failed: ";
    public const string JpegQualityHeader = "JPEG quality";
    public const string StripAttachmentsLabel = "Remove embedded files";
    public const string PreserveMonoLabel = "Preserve monochrome images";
    public const string StripMetadataLabel = "Remove metadata";

    public static IReadOnlyList<string> PresetLabels { get; } =
    [
        "Lossless (full rewrite)",
        "High quality (200 DPI)",
        "Balanced (150 DPI)",
        "Small file (96 DPI + strip attachments/metadata)",
        "Custom",
    ];

    public const int DefaultPresetIndex = 2; // Balanced

    public static PdfOptimizePreset FromComboIndex(int selectedIndex) =>
        selectedIndex switch
        {
            0 => PdfOptimizePreset.Lossless,
            1 => PdfOptimizePreset.HighQuality,
            3 => PdfOptimizePreset.SmallFile,
            4 => PdfOptimizePreset.Custom,
            _ => PdfOptimizePreset.Balanced,
        };

    public static bool IsCustomIndex(int selectedIndex) => selectedIndex == 4;

    public static string ResultStatus(PdfOptimizeResult result, string bytesBefore, string bytesAfter) =>
        $"Optimized: {result.ImagesDownsampled} image(s) downsampled, "
        + $"{result.AttachmentsRemoved} attachment(s) removed; "
        + $"{bytesBefore} → {bytesAfter}. Save to keep.";

    public static string FailedStatus(string message) => FailedPrefix + message;
}
