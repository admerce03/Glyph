using FluentAssertions;
using Glyph.Imaging.Abstractions;

namespace Glyph.Imaging.Tests;

public class ImageColorManagedDisplayPolicyTests
{
    [Fact]
    public void Soft_proof_labels()
    {
        ImageColorManagedDisplayPolicy.SoftProofOn.Should().Contain("Adobe RGB");
        ImageColorManagedDisplayPolicy.SoftProofOff.Should().Contain("off");
    }
}

public class ImageEditUndoPolicyTests
{
    [Fact]
    public void Max_depth_and_trim()
    {
        ImageEditUndoPolicy.MaxDepth.Should().Be(12);
        ImageEditUndoPolicy.TrimCount(15).Should().Be(3);
        ImageEditUndoPolicy.Undone.Should().Contain("undone");
    }
}
