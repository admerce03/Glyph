namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Hit-test helpers for image selection overlays (rect / ellipse / lasso).
/// </summary>
public static class ImageSelectionGeometry
{
    public static bool ContainsInEllipse(
        double x,
        double y,
        double left,
        double top,
        double width,
        double height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var cx = left + (width / 2.0);
        var cy = top + (height / 2.0);
        var rx = width / 2.0;
        var ry = height / 2.0;
        var nx = (x - cx) / rx;
        var ny = (y - cy) / ry;
        return (nx * nx) + (ny * ny) <= 1.0;
    }

    public static bool ContainsInRect(
        double x,
        double y,
        double left,
        double top,
        double width,
        double height) =>
        width > 0
        && height > 0
        && x >= left
        && y >= top
        && x <= left + width
        && y <= top + height;

    /// <summary>
    /// Ray-cast point-in-polygon. Returns false when fewer than 3 vertices.
    /// </summary>
    public static bool ContainsInPolygon(double x, double y, IReadOnlyList<(double X, double Y)> polygon)
    {
        if (polygon is null || polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (piX, piY) = polygon[i];
            var (pjX, pjY) = polygon[j];
            var intersect = ((piY > y) != (pjY > y))
                && (x < ((pjX - piX) * (y - piY) / ((pjY - piY) + double.Epsilon)) + piX);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
