namespace Glyph.Core.Documents;

/// <summary>
/// Version snapshots list UI (F51-02): timestamp, thumbnail, and file size.
/// </summary>
public static class VersionSnapshotThumbnailPolicy
{
    public const int ThumbEdgePixels = 56;

    public const bool ShowsTimestamp = true;

    public const bool ShowsFileSize = true;

    public const bool ShowsImageFilePreview = true;

    public const bool ShowsPdfFirstPagePreview = true;

    public const string Reason =
        "File → Version Snapshots lists each entry with local time, byte size, and a thumbnail (image file preview or PDF page 1 raster).";
}
