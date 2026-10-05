using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class StickyNoteExpandPolicyTests
{
    [Fact]
    public void Expand_collapse_status_and_collapse_all()
    {
        StickyNoteExpandPolicy.Expanded("Note p1").Should().Contain("Expanded");
        StickyNoteExpandPolicy.Collapsed("Note p1").Should().Contain("Collapsed");
        StickyNoteExpandPolicy.ShouldCollapseAll(hasSelectedSticky: false, expandedCount: 2).Should().BeTrue();
        StickyNoteExpandPolicy.ShouldCollapseAll(hasSelectedSticky: true, expandedCount: 2).Should().BeFalse();
        StickyNoteExpandPolicy.ShouldCollapseAll(hasSelectedSticky: false, expandedCount: 0).Should().BeFalse();
        StickyNoteExpandPolicy.NoExpandedNotes.Should().Contain("No expanded");
    }
}
