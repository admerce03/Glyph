using FluentAssertions;
using Glyph.Core.IO;

namespace Glyph.Core.Tests;

public class ByteSizeFormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1572864, "1.5 MB")]
    public void Format_thresholds(long bytes, string expected)
    {
        ByteSizeFormat.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public void FormatOptional_null_uses_label()
    {
        ByteSizeFormat.FormatOptional(null).Should().Be("—");
        ByteSizeFormat.FormatOptional(null, "?").Should().Be("?");
        ByteSizeFormat.FormatOptional(2048).Should().Be("2 KB");
    }
}
