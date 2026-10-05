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
        var dir = Path.Combine("tmp");
        PageExtractFileNames.TempPdfPath(dir, id)
            .Should().Be(Path.Combine(dir, "Glyph-pages-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"));
    }
}
