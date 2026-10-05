using FluentAssertions;
using Glyph.Core.Documents;
using Xunit;

namespace Glyph.Core.Tests;

public class PdfUiStatusCatalogTests
{
    [Fact]
    public void Find_bookmark_attachment_and_group_labels()
    {
        PdfFindStatus.NoResults.Should().Contain("search");
        PdfFindStatus.FormatFollowedLink(3).Should().Contain("page 3");
        BookmarkStatus.Renamed.Should().Contain("renamed");
        BookmarkStatus.FormatBookmarked("A").Should().Contain("A");
        AttachmentSaveStatus.FormatSaved("x.pdf", 12).Should().Contain("12");
        AnnotationGroupStatus.FormatGrouped(4).Should().Contain("4");
        FormFieldActionStatus.FormatButtonToPage("Go", 2).Should().Contain("page 2");
        PageClipboardStatus.Extracting.Should().Contain("Extracting");
        FullscreenTogglePolicy.Toggled.Should().Contain("Fullscreen");
        DocumentSaveStatus.Cancelled.Should().Contain("cancelled");
        PageEditStatus.FormatRotated(1).Should().Be("Rotated 1 page.");
        PageEditStatus.FormatRotated(2).Should().Contain("pages");
    }
}
