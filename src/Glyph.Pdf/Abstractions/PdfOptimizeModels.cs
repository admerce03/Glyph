namespace Glyph.Pdf.Abstractions;

public enum PdfOptimizePreset
{
    Lossless,
    HighQuality,
    Balanced,
    SmallFile,
    Custom,
}

/// <summary>
/// PDF optimization controls. Presets fill sensible defaults; Custom uses the explicit fields.
/// </summary>
public sealed record PdfOptimizeOptions(
    PdfOptimizePreset Preset = PdfOptimizePreset.Balanced,
    bool DownsampleImages = true,
    /// <summary>Only downsample images whose effective DPI exceeds this threshold.</summary>
    double DownsampleAboveDpi = 225,
    double TargetDpi = 150,
    int JpegQuality = 75,
    bool PreserveMonochrome = true,
    bool RemoveEmbeddedAttachments = false,
    bool RemoveMetadata = false)
{
    public static PdfOptimizeOptions FromPreset(PdfOptimizePreset preset) =>
        preset switch
        {
            PdfOptimizePreset.Lossless => new(
                Preset: PdfOptimizePreset.Lossless,
                DownsampleImages: false,
                RemoveEmbeddedAttachments: false,
                RemoveMetadata: false),
            PdfOptimizePreset.HighQuality => new(
                Preset: PdfOptimizePreset.HighQuality,
                DownsampleImages: true,
                DownsampleAboveDpi: 300,
                TargetDpi: 200,
                JpegQuality: 85,
                RemoveEmbeddedAttachments: false),
            PdfOptimizePreset.Balanced => new(
                Preset: PdfOptimizePreset.Balanced,
                DownsampleImages: true,
                DownsampleAboveDpi: 225,
                TargetDpi: 150,
                JpegQuality: 75,
                RemoveEmbeddedAttachments: false),
            PdfOptimizePreset.SmallFile => new(
                Preset: PdfOptimizePreset.SmallFile,
                DownsampleImages: true,
                DownsampleAboveDpi: 150,
                TargetDpi: 96,
                JpegQuality: 55,
                RemoveEmbeddedAttachments: true,
                RemoveMetadata: true),
            _ => new(Preset: PdfOptimizePreset.Custom),
        };
}

public sealed record PdfOptimizeEstimate(
    long CurrentBytes,
    long EstimatedBytes,
    int ImagesEligibleForDownsample,
    int AttachmentCount);

public sealed record PdfOptimizeResult(
    int ImagesDownsampled,
    int AttachmentsRemoved,
    long BytesBefore,
    long BytesAfter);
