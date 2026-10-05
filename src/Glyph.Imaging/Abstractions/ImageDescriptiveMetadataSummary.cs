namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Formats IPTC/XMP descriptive fields for the image Meta inspector (F37-20…26).
/// </summary>
public static class ImageDescriptiveMetadataSummary
{
    public static bool HasAny(ImageMetadataInfo info) =>
        !string.IsNullOrWhiteSpace(info.Title)
        || !string.IsNullOrWhiteSpace(info.Description)
        || !string.IsNullOrWhiteSpace(info.Keywords)
        || !string.IsNullOrWhiteSpace(info.Copyright)
        || info.Rating is not null
        || info.HasIptc
        || info.HasXmp;

    public static string Format(ImageMetadataInfo info)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.Title))
        {
            parts.Add("Title: " + info.Title);
        }

        if (!string.IsNullOrWhiteSpace(info.Description))
        {
            parts.Add("Description: " + info.Description);
        }

        if (!string.IsNullOrWhiteSpace(info.Keywords))
        {
            parts.Add("Keywords: " + info.Keywords);
        }

        if (!string.IsNullOrWhiteSpace(info.Copyright))
        {
            parts.Add("© " + info.Copyright);
        }

        if (info.Rating is int rating)
        {
            parts.Add("Rating: " + rating);
        }

        if (!string.IsNullOrWhiteSpace(info.LensModel))
        {
            parts.Add("Lens: " + info.LensModel);
        }

        if (info.HasIptc)
        {
            parts.Add("IPTC");
        }

        if (info.HasXmp)
        {
            parts.Add("XMP");
        }

        return string.Join(" · ", parts);
    }

    public static string? AutomationNameFromDescription(ImageMetadataInfo info) =>
        string.IsNullOrWhiteSpace(info.Description) ? null : info.Description.Trim();
}
