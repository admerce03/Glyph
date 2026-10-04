using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;

namespace Glyph.Pdf.Tests;

public class PageRenderCacheTests
{
    [Fact]
    public void Evicts_least_recently_used_entries()
    {
        var cache = new PageRenderCache(capacity: 2);
        cache.Set("doc", 0, 1.0, new PdfRenderResult(1, 1, new byte[4]));
        cache.Set("doc", 1, 1.0, new PdfRenderResult(1, 1, new byte[4]));
        cache.TryGet("doc", 0, 1.0, out _).Should().BeTrue();

        cache.Set("doc", 2, 1.0, new PdfRenderResult(1, 1, new byte[4]));

        cache.Count.Should().Be(2);
        cache.TryGet("doc", 1, 1.0, out _).Should().BeFalse();
        cache.TryGet("doc", 0, 1.0, out _).Should().BeTrue();
        cache.TryGet("doc", 2, 1.0, out _).Should().BeTrue();
    }
}
