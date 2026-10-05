using FluentAssertions;
using Glyph.Core.IO;

namespace Glyph.Core.Tests;

public class DocumentFileNamePolicyTests
{
    [Fact]
    public void SuggestDuplicatePath_appends_copy_and_increments()
    {
        var dir = Path.Combine("tmp");
        var source = Path.Combine(dir, "doc.pdf");
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            source,
            Path.Combine(dir, "doc copy.pdf"),
        };

        DocumentFileNamePolicy.SuggestDuplicatePath(source, existing.Contains)
            .Should().Be(Path.Combine(dir, "doc copy 2.pdf"));
    }

    [Theory]
    [InlineData("ok.pdf", true)]
    [InlineData("bad/name.pdf", false)]
    [InlineData(@"bad\name.pdf", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidFileName(string name, bool expected)
    {
        DocumentFileNamePolicy.IsValidFileName(name).Should().Be(expected);
    }

    [Fact]
    public void EvaluateRename_covers_outcomes()
    {
        var dir = Path.Combine("tmp");
        var current = Path.Combine(dir, "a.pdf");
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            current,
            Path.Combine(dir, "taken.pdf"),
        };

        DocumentFileNamePolicy.EvaluateRename(current, "a.pdf", existing.Contains)
            .Status.Should().Be(RenamePathStatus.Unchanged);
        DocumentFileNamePolicy.EvaluateRename(current, "bad/x.pdf", existing.Contains)
            .Status.Should().Be(RenamePathStatus.Invalid);
        DocumentFileNamePolicy.EvaluateRename(current, "taken.pdf", existing.Contains)
            .Status.Should().Be(RenamePathStatus.Conflict);

        var ok = DocumentFileNamePolicy.EvaluateRename(current, "b.pdf", existing.Contains);
        ok.Status.Should().Be(RenamePathStatus.Ok);
        ok.DestinationPath.Should().Be(Path.Combine(dir, "b.pdf"));
    }
}
