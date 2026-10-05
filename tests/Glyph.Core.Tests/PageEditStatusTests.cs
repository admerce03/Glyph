using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PageEditStatusTests
{
    [Fact]
    public void Progress_and_done_labels_are_stable()
    {
        PageEditStatus.Reordering.Should().Contain("Reordering");
        PageEditStatus.PagesReordered.Should().Be("Pages reordered.");
        PageEditStatus.Rotating.Should().Contain("Rotating");
        PageEditStatus.Deleting.Should().Contain("Deleting");
        PageEditStatus.CannotDeleteEveryPage.Should().Contain("every page");
        PageEditStatus.InsertingBlankPage.Should().Contain("blank");
        PageEditStatus.InsertedBlankPage.Should().Contain("Inserted");
        PageEditStatus.Duplicating.Should().Contain("Duplicating");
        PageEditStatus.Cropping.Should().Contain("Cropping");
        PageEditStatus.UndidPageEdit.Should().Contain("Undid");
        PageEditStatus.RedidPageEdit.Should().Contain("Redid");
        PageEditStatus.NoPagesToInsert.Should().Contain("insert");
        PageEditStatus.SplitCancelled.Should().Contain("cancelled");
        PageEditStatus.SplitWouldBeSingle.Should().Contain("single");
    }

    [Fact]
    public void Merge_split_helpers_format_counts()
    {
        PageEditStatus.FormatMerging(1).Should().Be("Merging PDF…");
        PageEditStatus.FormatMerging(3).Should().Contain("3 PDFs");
        PageEditStatus.FormatMerged(1).Should().Be("Merged 1 page.");
        PageEditStatus.FormatMerged(5).Should().Contain("5 pages");
        PageEditStatus.FormatSplitting(2).Should().Contain("2 PDFs");
        PageEditStatus.FormatSplitDone(2, "Out").Should().Contain("Out");
        PageEditStatus.FormatPastePagesFailed("x").Should().Be("Paste pages failed: x");
        PageEditStatus.FormatCropped(1).Should().Be("Cropped 1 page.");
        PageEditStatus.FormatCropped(3).Should().Contain("3 pages");
        PageEditStatus.FormatInsertedFromFile(2, 2).Should().Contain("files");
        PageEditStatus.FormatSelectedPages(1).Should().Be("Selected 1 page.");
    }
}
