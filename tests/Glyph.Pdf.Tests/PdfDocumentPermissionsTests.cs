using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfDocumentPermissionsTests
{
    [Fact]
    public void FromFlags_decodes_print_copy_annotate_bits()
    {
        // Bits 3,5,6 (print/copy/annotate) — 1-based PDF bit numbers → shifts 2,4,5.
        uint flags = (1u << 2) | (1u << 4) | (1u << 5);
        var perms = PdfDocumentPermissions.FromFlags(flags);
        perms.CanPrint.Should().BeTrue();
        perms.CanModify.Should().BeFalse();
        perms.CanCopy.Should().BeTrue();
        perms.CanAnnotate.Should().BeTrue();
        perms.CanFillForms.Should().BeFalse();
        perms.CanAssemble.Should().BeFalse();
        perms.CanPrintHighQuality.Should().BeFalse();
    }

    [Fact]
    public void FromFlags_all_clear_when_zero()
    {
        var perms = PdfDocumentPermissions.FromFlags(0);
        perms.CanPrint.Should().BeFalse();
        perms.CanModify.Should().BeFalse();
        perms.CanCopy.Should().BeFalse();
        perms.CanAnnotate.Should().BeFalse();
        perms.CanFillForms.Should().BeFalse();
        perms.CanAssemble.Should().BeFalse();
        perms.CanPrintHighQuality.Should().BeFalse();
    }

    [Fact]
    public void AdvisoryNotice_states_enforcement_is_advisory()
    {
        PdfDocumentPermissions.AdvisoryNotice.Should().Contain("advisory");
        PdfDocumentPermissions.EncryptedAdvisoryStatus.Should().Contain("advisory");
        PdfDocumentPermissions.EncryptedAdvisoryStatus.Should().Contain("encrypted");
        PdfDocumentPermissions.StatusBarEncryptedSuffix(true).Should().Contain("Encrypted");
        PdfDocumentPermissions.StatusBarEncryptedSuffix(false).Should().BeEmpty();
        PdfDocumentPermissions.PropertiesEncryptedMarker(true).Should().Contain("Encrypted");
        PdfDocumentPermissions.PropertiesEncryptedMarker(false).Should().BeEmpty();
        PdfDocumentPermissions.InfoEncryptedLine(true).Should().Be("Encrypted: yes");
        PdfDocumentPermissions.InfoEncryptedLine(false).Should().Be("Encrypted: no");
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
