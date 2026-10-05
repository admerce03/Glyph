namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Deferred image capabilities: HDR display, HEIF encode, ML subject, in-app map.
/// </summary>
public static class ImageAdvancedDeferredPolicy
{
    public const bool HdrDisplayPipelineSupported = false;
    public const bool HeifEncodeDelegateAvailable = false;
    public const bool AutomaticSubjectDetectionSupported = false;
    public const bool EmbeddedMapWebViewSupported = false;

    public const string HdrReason =
        "Needs WinUI HDR display pipeline; revisit with monitor color management.";

    public const string HeifEncodeReason =
        "Magick build lacks HEIF encode delegate in CI/dev snapshots.";

    public const string SubjectDetectionReason =
        "Needs on-device ML model; flood-fill covers solid backgrounds.";

    public const string EmbeddedMapReason =
        "Open map uses OSM/browser; in-app WebView map is post-M8.";
}
