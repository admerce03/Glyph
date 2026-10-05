using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class ContactSheetLayoutTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 3)]
    [InlineData(4, 1, 0)]
    [InlineData(9, 2, 1)]
    public void Cell_maps_page_to_row_column(int page, int row, int col)
    {
        ContactSheetLayout.Cell(page).Should().Be((row, col));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(8, 2)]
    public void RowCount_ceil_divides(int pages, int expectedRows)
    {
        ContactSheetLayout.RowCount(pages).Should().Be(expectedRows);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(1, false)]
    public void StartsRow(int page, bool expected)
    {
        ContactSheetLayout.StartsRow(page).Should().Be(expected);
    }
}
