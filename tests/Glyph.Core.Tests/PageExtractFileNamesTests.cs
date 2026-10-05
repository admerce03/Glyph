using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PageExtractFileNamesTests
{
    [Fact]
    public void TempPdfPath_uses_glyph_pages_prefix()
    {
        var id = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        PageExtractFileNames.TempPdfPath("/tmp", id)
            .Should().Be("/tmp/Glyph-pages-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf");
    }
}
