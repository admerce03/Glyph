using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageMarkupFlattenPolicyTests
{
    [Fact]
    public void Pending_and_status()
    {
        ImageMarkupFlattenPolicy.HasPendingMarkup(1, 0).Should().BeTrue();
        ImageMarkupFlattenPolicy.HasPendingMarkup(0, 0).Should().BeFalse();
        ImageMarkupFlattenPolicy.Flattened(3).Should().Contain("3");
    }
}

public class ImageBatchColorProfilePolicyTests
{
    [Fact]
    public void Profile_from_combo()
    {
        ImageBatchColorProfilePolicy.FromComboIndex(0).Should().Be(ImageColorProfileKind.Srgb);
        ImageBatchColorProfilePolicy.FromComboIndex(1).Should().Be(ImageColorProfileKind.AdobeRgb);
        ImageBatchColorProfilePolicy.UpdatedStatus(4).Should().Contain("4");
    }
}
