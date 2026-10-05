using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageGpsActionsTests
{
    [Fact]
    public void Formats_coords_and_osm_uri()
    {
        ImageGpsActions.FormatCoords(47.6062, -122.3321).Should().Contain("47.6062");
        var uri = ImageGpsActions.OpenStreetMapUri(47.6, -122.3);
        uri.Should().StartWith("https://www.openstreetmap.org/");
        uri.Should().Contain("mlat=");
        ImageGpsActions.CopiedStatus("1, 2").Should().Be("Copied GPS 1, 2");
    }
}

public class BatchRenamePatternTests
{
    [Fact]
    public void Expands_name_and_index_tokens()
    {
        BatchRenamePattern.Expand("{name}-{n:000}", "photo", 7).Should().Be("photo-007");
        BatchRenamePattern.Expand("{n}", "x", 3).Should().Be("3");
        BatchRenamePattern.Expand("bad/name", "x", 1).Should().Be("bad_name");
    }
}
