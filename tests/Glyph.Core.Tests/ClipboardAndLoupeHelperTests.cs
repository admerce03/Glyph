using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class ClipboardImageFileNameTests
{
    [Fact]
    public void ForTimestamp_uses_local_clock_pattern()
    {
        var name = ClipboardImageFileName.ForTimestamp(new DateTime(2026, 10, 5, 13, 44, 6));
        name.Should().Be("Clipboard-20261005-134406.png");
    }
}

public class DocumentPropertiesRoutingTests
{
    [Theory]
    [InlineData(DocumentKind.Pdf, DocumentPropertiesKind.PdfInfo)]
    [InlineData(DocumentKind.Image, DocumentPropertiesKind.ImageMetadata)]
    [InlineData(DocumentKind.Unknown, DocumentPropertiesKind.None)]
    public void For_maps_kind(DocumentKind kind, DocumentPropertiesKind expected)
    {
        DocumentPropertiesRouting.For(kind).Should().Be(expected);
    }
}

public class StartupReadyStatusTests
{
    [Fact]
    public void Format_includes_milliseconds()
    {
        StartupReadyStatus.Format(123).Should().Contain("123 ms");
    }
}

public class PdfLoupeSampleRegionTests
{
    [Fact]
    public void Resolve_maps_pdf_bottom_left_to_bitmap_top_left_sample()
    {
        // 200x100 page on 200x100 bitmap; centre 20x20 rect around (100,50) PDF → mid bitmap.
        var sample = PdfLoupeSampleRegion.Resolve(
            bitmapWidth: 200,
            bitmapHeight: 100,
            pageWidthPoints: 200,
            pageHeightPoints: 100,
            left: 90,
            bottom: 40,
            right: 110,
            top: 60,
            zoom: PdfLoupeSampleRegion.DefaultZoom);

        sample.Should().NotBeNull();
        sample!.Value.Size.Should().BeGreaterThanOrEqualTo(2);
        sample.Value.Left.Should().BeInRange(0, 199);
        sample.Value.Top.Should().BeInRange(0, 99);
    }

    [Fact]
    public void Resolve_rejects_invalid_zoom()
    {
        PdfLoupeSampleRegion.Resolve(100, 100, 100, 100, 0, 0, 10, 10, zoom: 0.5)
            .Should().BeNull();
    }

    [Fact]
    public void Higher_zoom_shrinks_sample()
    {
        var normal = PdfLoupeSampleRegion.Resolve(300, 300, 300, 300, 140, 140, 160, 160, 3.0)!;
        var tight = PdfLoupeSampleRegion.Resolve(300, 300, 300, 300, 140, 140, 160, 160, 6.0)!;
        tight.Value.Size.Should().BeLessThan(normal.Value.Size);
    }
}
