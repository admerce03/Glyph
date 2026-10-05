using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class ProductFramingPolicyTests
{
    [Fact]
    public void Six_tools_and_charter()
    {
        ProductFramingPolicy.IntegratedTools.Should().HaveCount(6);
        ProductFramingPolicy.CatalogContains("PDF").Should().BeTrue();
        ProductFramingPolicy.CatalogContains("OCR").Should().BeTrue();
        ProductFramingPolicy.Charter.Should().Contain("Glyph");
    }
}
