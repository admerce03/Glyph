using FluentAssertions;
using Glyph.Core.IO;

namespace Glyph.Core.Tests;

public class DocumentMovePolicyTests
{
    [Fact]
    public void DestinationPath_keeps_file_name()
    {
        DocumentMovePolicy.DestinationPath("/tmp/docs/a.pdf", "/archives")
            .Should().Be(Path.Combine("/archives", "a.pdf"));
    }

    [Fact]
    public void IsSameFolder_compares_full_paths()
    {
        var dir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var file = Path.Combine(dir, "glyph-move-" + Guid.NewGuid().ToString("N") + ".pdf");
        DocumentMovePolicy.IsSameFolder(file, dir).Should().BeTrue();
        DocumentMovePolicy.IsSameFolder(file, Path.Combine(dir, "other")).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, false, MoveOverwriteDecision.Proceed)]
    [InlineData(true, false, false, MoveOverwriteDecision.Cancelled)]
    [InlineData(true, false, true, MoveOverwriteDecision.Proceed)]
    [InlineData(true, true, true, MoveOverwriteDecision.BlockedReadOnly)]
    public void EvaluateOverwrite(bool exists, bool readOnly, bool confirmed, MoveOverwriteDecision expected)
    {
        DocumentMovePolicy.EvaluateOverwrite(exists, readOnly, confirmed).Should().Be(expected);
    }
}
