using System.Globalization;

namespace Glyph.Imaging.Abstractions;

/// <summary>
/// GPS coordinate copy / OpenStreetMap launch helpers (F38-02/03).
/// </summary>
public static class ImageGpsActions
{
    public const string CopyButton = "Copy GPS";
    public const string OpenMapButton = "Open map";
    public const string RemoveButton = "Remove GPS";
    public const string RemovedStatus = "GPS metadata removed.";

    public static string FormatCoords(double latitude, double longitude) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.######}, {1:0.######}", latitude, longitude);

    public static string CopiedStatus(string coords) => "Copied GPS " + coords;

    public static string OpenStreetMapUri(double latitude, double longitude)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        return $"https://www.openstreetmap.org/?mlat={Uri.EscapeDataString(lat)}&mlon={Uri.EscapeDataString(lon)}#map=15/{Uri.EscapeDataString(lat)}/{Uri.EscapeDataString(lon)}";
    }
}
