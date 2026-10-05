using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class GpuAccelerationPolicyTests
{
    [Fact]
    public void Gpu_path_not_adopted()
    {
        GpuAccelerationPolicy.Win2dCompositionPathAdopted.Should().BeFalse();
        GpuAccelerationPolicy.DeferredReason.Should().Contain("CPU");
    }
}
