using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfPageImageExportOptionsTests
{
    [Fact]
    public void Scale_is_dpi_over_72()
    {
        var opts = new PdfPageImageExportOptions(Dpi: 144);
        opts.Scale.Should().BeApproximately(2.0, 1e-9);
        opts.Extension.Should().Be(".png");
        opts.EmbedSrgbProfile.Should().BeTrue();
    }

    [Fact]
    public void Quality_and_metadata_round_trip()
    {
        var opts = new PdfPageImageExportOptions(
            Extension: ".jpg",
            Dpi: 96,
            Quality: 80,
            Title: "T",
            Author: "A");
        opts.Quality.Should().Be(80);
        opts.Title.Should().Be("T");
        opts.Author.Should().Be("A");
        opts.Scale.Should().BeApproximately(96 / 72.0, 1e-9);
    }
}
