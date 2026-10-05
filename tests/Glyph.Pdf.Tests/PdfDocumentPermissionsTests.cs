using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfDocumentPermissionsTests
{
    [Fact]
    public void AdvisoryNotice_states_enforcement_is_advisory()
    {
        PdfDocumentPermissions.AdvisoryNotice.Should().Contain("advisory");
        PdfDocumentPermissions.EncryptedAdvisoryStatus.Should().Contain("advisory");
        PdfDocumentPermissions.EncryptedAdvisoryStatus.Should().Contain("encrypted");
    }

    [Fact]
    public void FormatSection_includes_advisory_heading_and_all_flags()
    {
        var perms = new PdfDocumentPermissions(
            CanPrint: true,
            CanModify: false,
            CanCopy: true,
            CanAnnotate: false,
            CanFillForms: true,
            CanAssemble: false,
            CanPrintHighQuality: true);

        var section = perms.FormatSection();
        section.Should().StartWith(PdfDocumentPermissions.AdvisoryNotice);
        section.Should().Contain("Print: yes");
        section.Should().Contain("Modify: no");
        section.Should().Contain("Copy: yes");
        section.Should().Contain("Annotate: no");
        section.Should().Contain("Fill forms: yes");
        section.Should().Contain("Assemble: no");
        section.Should().Contain("High-quality print: yes");
    }
}
