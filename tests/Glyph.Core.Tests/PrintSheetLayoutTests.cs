using FluentAssertions;
using Glyph.Core.Printing;
using Xunit;

namespace Glyph.Core.Tests;

public class PrintSheetLayoutTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(8, 1)]
    public void NormalizePagesPerSheet_clamps_to_1_2_or_4(int input, int expected)
    {
        PrintSheetLayout.NormalizePagesPerSheet(input).Should().Be(expected);
    }

    [Fact]
    public void Cells_one_up_fills_page()
    {
        var cells = PrintSheetLayout.Cells(800, 1000, 1);
        cells.Should().HaveCount(1);
        cells[0].Should().Be(new PrintCell(0, 0, 800, 1000));
    }

    [Fact]
    public void Cells_two_up_side_by_side_with_gap()
    {
        var cells = PrintSheetLayout.Cells(800, 1000, 2);
        cells.Should().HaveCount(2);
        var expectedW = (800 - PrintSheetLayout.TwoUpGap) / 2;
        cells[0].Should().Be(new PrintCell(0, 0, expectedW, 1000));
        cells[1].Should().Be(new PrintCell(expectedW + PrintSheetLayout.TwoUpGap, 0, expectedW, 1000));
    }

    [Fact]
    public void Cells_four_up_is_2x2_grid()
    {
        var cells = PrintSheetLayout.Cells(810, 610, 4);
        cells.Should().HaveCount(4);
        var gap = PrintSheetLayout.FourUpGap;
        var w = (810 - gap) / 2;
        var h = (610 - gap) / 2;
        cells[0].Should().Be(new PrintCell(0, 0, w, h));
        cells[1].Should().Be(new PrintCell(w + gap, 0, w, h));
        cells[2].Should().Be(new PrintCell(0, h + gap, w, h));
        cells[3].Should().Be(new PrintCell(w + gap, h + gap, w, h));
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(5, 1, 5)]
    [InlineData(5, 2, 3)]
    [InlineData(5, 4, 2)]
    [InlineData(8, 4, 2)]
    public void SheetCount_ceil_divides(int pages, int perSheet, int expected)
    {
        PrintSheetLayout.SheetCount(pages, perSheet).Should().Be(expected);
    }

    [Theory]
    [InlineData(200, 100, 100, 200, true)]  // landscape content in portrait cell
    [InlineData(100, 200, 200, 100, true)]  // portrait content in landscape cell
    [InlineData(200, 100, 200, 100, false)] // already matching
    [InlineData(100, 200, 100, 200, false)]
    public void ShouldAutoRotate_when_aspects_disagree(
        double cw, double ch, double aw, double ah, bool expected)
    {
        PrintSheetLayout.ShouldAutoRotate(cw, ch, aw, ah).Should().Be(expected);
    }

    [Fact]
    public void ComputeTarget_fit_scales_uniformly()
    {
        var layout = PrintSheetLayout.ComputeTarget(
            contentW: 400, contentH: 200,
            availW: 200, availH: 200,
            PrintScaleMode.Fit, autoRotate: false);
        layout.Rotate.Should().BeFalse();
        layout.ImageWidth.Should().Be(200);
        layout.ImageHeight.Should().Be(100);
        layout.OccupiedWidth.Should().Be(200);
        layout.OccupiedHeight.Should().Be(100);
    }

    [Fact]
    public void ComputeTarget_fill_uses_full_cell()
    {
        var layout = PrintSheetLayout.ComputeTarget(
            100, 50, 200, 200, PrintScaleMode.Fill, autoRotate: false);
        layout.ImageWidth.Should().Be(200);
        layout.ImageHeight.Should().Be(200);
    }

    [Fact]
    public void ComputeTarget_actual_size_clamps_to_cell()
    {
        var layout = PrintSheetLayout.ComputeTarget(
            500, 400, 200, 300, PrintScaleMode.ActualSize, autoRotate: false);
        layout.ImageWidth.Should().Be(200);
        layout.ImageHeight.Should().Be(300);
    }

    [Fact]
    public void ComputeTarget_auto_rotate_swaps_occupied_size()
    {
        // Landscape content in portrait cell → rotate; fit into swapped layout (200×100).
        var layout = PrintSheetLayout.ComputeTarget(
            contentW: 400, contentH: 100,
            availW: 100, availH: 200,
            PrintScaleMode.Fit, autoRotate: true);
        layout.Rotate.Should().BeTrue();
        layout.ImageWidth.Should().Be(200);  // fit into layoutW=200 (availH)
        layout.ImageHeight.Should().Be(50);
        layout.OccupiedWidth.Should().Be(50);   // swapped host
        layout.OccupiedHeight.Should().Be(200);
    }

    [Fact]
    public void PlaceInCell_centers_when_requested()
    {
        PrintSheetLayout.PlaceInCell(10, 20, 100, 80, 40, 20, center: true)
            .Should().Be((40, 50));
        PrintSheetLayout.PlaceInCell(10, 20, 100, 80, 40, 20, center: false)
            .Should().Be((10, 20));
    }
}
