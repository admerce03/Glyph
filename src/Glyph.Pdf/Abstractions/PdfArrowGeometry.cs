namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Shaft truncation and arrowhead ink strokes for <see cref="PdfShapeKind.Arrow"/>.
/// </summary>
public static class PdfArrowGeometry
{
    public static double ComputeHeadLength(double shaftLength) =>
        Math.Clamp(shaftLength * 0.22, 8.0, 28.0);

    public static (PdfPagePoint ShaftEnd, IReadOnlyList<IReadOnlyList<PdfPagePoint>> HeadStrokes) Build(
        PdfPagePoint start,
        PdfPagePoint end,
        PdfArrowheadStyle headStyle,
        double headLength)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1)
        {
            return (end, []);
        }

        var ux = dx / length;
        var uy = dy / length;
        var nx = -uy;
        var ny = ux;
        var head = Math.Max(1, headLength);
        var shaftEnd = length > head + 1
            ? new PdfPagePoint(end.X - (ux * head), end.Y - (uy * head))
            : start;

        if (length <= head + 1)
        {
            return (shaftEnd, []);
        }

        var headStrokes = headStyle switch
        {
            PdfArrowheadStyle.Open => BuildOpenHead(end, ux, uy, head),
            PdfArrowheadStyle.Filled => BuildFilledHead(end, ux, uy, nx, ny, head),
            PdfArrowheadStyle.Diamond => BuildDiamondHead(end, ux, uy, nx, ny, head),
            _ => BuildOpenHead(end, ux, uy, head),
        };

        return (shaftEnd, headStrokes);
    }

    private static IReadOnlyList<IReadOnlyList<PdfPagePoint>> BuildOpenHead(
        PdfPagePoint tip,
        double ux,
        double uy,
        double head)
    {
        const double wingRadians = Math.PI / 7;
        var cos = Math.Cos(wingRadians);
        var sin = Math.Sin(wingRadians);
        var backX = -ux * head;
        var backY = -uy * head;
        var wing1 = new PdfPagePoint(
            tip.X + (backX * cos) - (backY * sin),
            tip.Y + (backX * sin) + (backY * cos));
        var wing2 = new PdfPagePoint(
            tip.X + (backX * cos) + (backY * sin),
            tip.Y + (-backX * sin) + (backY * cos));

        return
        [
            [tip, wing1],
            [tip, wing2],
        ];
    }

    private static IReadOnlyList<IReadOnlyList<PdfPagePoint>> BuildFilledHead(
        PdfPagePoint tip,
        double ux,
        double uy,
        double nx,
        double ny,
        double head)
    {
        var halfWidth = head * 0.45;
        var baseCenter = new PdfPagePoint(tip.X - (ux * head), tip.Y - (uy * head));
        var baseLeft = new PdfPagePoint(
            baseCenter.X + (nx * halfWidth),
            baseCenter.Y + (ny * halfWidth));
        var baseRight = new PdfPagePoint(
            baseCenter.X - (nx * halfWidth),
            baseCenter.Y - (ny * halfWidth));

        return [[baseLeft, tip, baseRight, baseLeft]];
    }

    private static IReadOnlyList<IReadOnlyList<PdfPagePoint>> BuildDiamondHead(
        PdfPagePoint tip,
        double ux,
        double uy,
        double nx,
        double ny,
        double head)
    {
        var halfWidth = head * 0.35;
        var back = new PdfPagePoint(tip.X - (ux * head), tip.Y - (uy * head));
        var mid = new PdfPagePoint(tip.X - (ux * head * 0.55), tip.Y - (uy * head * 0.55));
        var left = new PdfPagePoint(mid.X + (nx * halfWidth), mid.Y + (ny * halfWidth));
        var right = new PdfPagePoint(mid.X - (nx * halfWidth), mid.Y - (ny * halfWidth));

        return [[left, tip, right, back, left]];
    }
}
