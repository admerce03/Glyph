using FluentAssertions;
using Glyph.Core.IO;

namespace Glyph.Core.Tests;

public class DocumentFileNamePolicyTests
{
    [Fact]
    public void SuggestDuplicatePath_appends_copy_and_increments()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"/tmp/doc.pdf",
            @"/tmp/doc copy.pdf",
        };

        DocumentFileNamePolicy.SuggestDuplicatePath(@"/tmp/doc.pdf", existing.Contains)
            .Should().Be(@"/tmp/doc copy 2.pdf");
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
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"/tmp/a.pdf",
            @"/tmp/taken.pdf",
        };

        DocumentFileNamePolicy.EvaluateRename(@"/tmp/a.pdf", "a.pdf", existing.Contains)
            .Status.Should().Be(RenamePathStatus.Unchanged);
        DocumentFileNamePolicy.EvaluateRename(@"/tmp/a.pdf", "bad/x.pdf", existing.Contains)
            .Status.Should().Be(RenamePathStatus.Invalid);
        DocumentFileNamePolicy.EvaluateRename(@"/tmp/a.pdf", "taken.pdf", existing.Contains)
            .Status.Should().Be(RenamePathStatus.Conflict);

        var ok = DocumentFileNamePolicy.EvaluateRename(@"/tmp/a.pdf", "b.pdf", existing.Contains);
        ok.Status.Should().Be(RenamePathStatus.Ok);
        ok.DestinationPath.Should().Be(@"/tmp/b.pdf");
    }
}
