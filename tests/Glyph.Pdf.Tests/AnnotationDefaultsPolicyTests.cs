using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class AnnotationDefaultsPolicyTests
{
    [Fact]
    public void ResolveAuthor_trims_preferred_over_fallback()
    {
        AnnotationDefaultsPolicy.ResolveAuthor("  Ada  ", "fallback").Should().Be("Ada");
    }

    [Fact]
    public void ResolveAuthor_keeps_fallback_when_preferred_blank()
    {
        AnnotationDefaultsPolicy.ResolveAuthor("  ", "Owner").Should().Be("Owner");
    }

    [Fact]
    public void ClampStrokeWidthPoints_clamps_range()
    {
        AnnotationDefaultsPolicy.ClampStrokeWidthPoints(0.1).Should().Be(0.5f);
        AnnotationDefaultsPolicy.ClampStrokeWidthPoints(2.5).Should().Be(2.5f);
        AnnotationDefaultsPolicy.ClampStrokeWidthPoints(99).Should().Be(12f);
    }

    [Fact]
    public void TryResolveHighlight_matches_preset_case_insensitively()
    {
        AnnotationDefaultsPolicy.TryResolveHighlight("green", out var color).Should().BeTrue();
        color.Should().Be(PdfAnnotationColor.HighlightPresets.First(p => p.Name == "Green").Color);
    }

    [Fact]
    public void TryResolveHighlight_rejects_unknown()
    {
        AnnotationDefaultsPolicy.TryResolveHighlight("not-a-color", out _).Should().BeFalse();
    }

    [Fact]
    public void TryResolveStroke_matches_preset()
    {
        AnnotationDefaultsPolicy.TryResolveStroke("dodger blue", out var color).Should().BeTrue();
        color.Should().Be(PdfAnnotationColor.StrokePresets.First(p => p.Name == "Dodger blue").Color);
    }
}
