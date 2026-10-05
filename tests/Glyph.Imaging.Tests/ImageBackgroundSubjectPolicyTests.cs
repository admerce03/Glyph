using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageBackgroundSubjectPolicyTests
{
    [Fact]
    public void Alpha_formats_and_labels()
    {
        ImageBackgroundSubjectPolicy.FormatSupportsAlpha("png").Should().BeTrue();
        ImageBackgroundSubjectPolicy.FormatSupportsAlpha(".WEBP").Should().BeTrue();
        ImageBackgroundSubjectPolicy.FormatSupportsAlpha("jpg").Should().BeFalse();
        ImageBackgroundSubjectPolicy.SuggestedSubjectFileName("photo")
            .Should().Be("photo-subject.png");
        ImageBackgroundSubjectPolicy.ExtractToClipboard.Should().Contain("clipboard");
    }
}
