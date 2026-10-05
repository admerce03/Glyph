using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfStrokeShapeRecognizerTests
{
    [Fact]
    public void Recognizes_near_straight_line()
    {
        var points = new List<PdfPagePoint>();
        for (var i = 0; i <= 20; i++)
        {
            points.Add(new PdfPagePoint(10 + (i * 5), 100 + (i * 0.1)));
        }

        var result = PdfStrokeShapeRecognizer.Recognize(points);
        result.Shape.Should().Be(PdfRecognizedStrokeShape.Line);
    }

    [Fact]
    public void Recognizes_closed_rectangle()
    {
        var points = BuildRectPath(50, 50, 150, 120, samplesPerEdge: 12);
        var result = PdfStrokeShapeRecognizer.Recognize(points);
        result.Shape.Should().Be(PdfRecognizedStrokeShape.Rectangle);
        result.Bounds.Width.Should().BeApproximately(100, 5);
        result.Bounds.Height.Should().BeApproximately(70, 5);
    }

    [Fact]
    public void Recognizes_closed_ellipse()
    {
        var points = new List<PdfPagePoint>();
        const double cx = 100, cy = 80, rx = 40, ry = 30;
        for (var i = 0; i <= 48; i++)
        {
            var t = (Math.PI * 2 * i) / 48;
            points.Add(new PdfPagePoint(cx + (Math.Cos(t) * rx), cy + (Math.Sin(t) * ry)));
        }

        var result = PdfStrokeShapeRecognizer.Recognize(points);
        result.Shape.Should().Be(PdfRecognizedStrokeShape.Ellipse);
    }

    [Fact]
    public void Recognizes_triangle()
    {
        var a = new PdfPagePoint(100, 40);
        var b = new PdfPagePoint(40, 140);
        var c = new PdfPagePoint(160, 140);
        var points = new List<PdfPagePoint>();
        points.AddRange(SampleEdge(a, b, 10));
        points.AddRange(SampleEdge(b, c, 10).Skip(1));
        points.AddRange(SampleEdge(c, a, 10).Skip(1));

        var result = PdfStrokeShapeRecognizer.Recognize(points);
        result.Shape.Should().Be(PdfRecognizedStrokeShape.Triangle);
        result.Vertices.Should().NotBeNull();
        result.Vertices!.Count.Should().Be(3);
    }

    [Fact]
    public void Scribble_is_not_recognized()
    {
        var points = new List<PdfPagePoint>();
        for (var i = 0; i < 40; i++)
        {
            points.Add(new PdfPagePoint(50 + (i % 7) * 3, 50 + Math.Sin(i) * 20 + (i * 0.5)));
        }

        var result = PdfStrokeShapeRecognizer.Recognize(points);
        result.Shape.Should().Be(PdfRecognizedStrokeShape.None);
    }

    private static List<PdfPagePoint> BuildRectPath(double l, double b, double r, double t, int samplesPerEdge)
    {
        var points = new List<PdfPagePoint>();
        points.AddRange(SampleEdge(new PdfPagePoint(l, b), new PdfPagePoint(r, b), samplesPerEdge));
        points.AddRange(SampleEdge(new PdfPagePoint(r, b), new PdfPagePoint(r, t), samplesPerEdge).Skip(1));
        points.AddRange(SampleEdge(new PdfPagePoint(r, t), new PdfPagePoint(l, t), samplesPerEdge).Skip(1));
        points.AddRange(SampleEdge(new PdfPagePoint(l, t), new PdfPagePoint(l, b), samplesPerEdge).Skip(1));
        return points;
    }

    private static IEnumerable<PdfPagePoint> SampleEdge(PdfPagePoint a, PdfPagePoint b, int samples)
    {
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            yield return new PdfPagePoint(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
        }
    }
}
