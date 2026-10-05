using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfiumDocumentInfoServiceTests
{
    [Fact]
    public async Task GetInfo_reads_metadata_and_unencrypted_permissions()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            var info = infoService.GetInfo(document);
            info.Title.Should().Be("Glyph Title");
            info.Author.Should().Be("Glyph Author");
            info.Subject.Should().Be("Glyph Subject");
            info.Keywords.Should().Be("glyph, test");
            info.PageCount.Should().Be(1);
            info.IsEncrypted.Should().BeFalse();
            info.SecurityHandlerRevision.Should().Be(-1);
            info.PermissionFlags.Should().Be(0xffff_ffff);
            info.Permissions.CanPrint.Should().BeTrue();
            info.Permissions.CanCopy.Should().BeTrue();
            info.FileSizeBytes.Should().BeGreaterThan(0);
            info.PdfVersion.Should().NotBeNullOrWhiteSpace();
            info.PageWidthPoints.Should().BeApproximately(612, 0.5); // Letter
            info.PageHeightPoints.Should().BeApproximately(792, 0.5);
            info.Fonts.Should().NotBeEmpty();
            info.EmbeddedAttachmentCount.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetInfo_reports_encryption_for_password_pdf()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "encrypted.pdf");
        File.Exists(path).Should().BeTrue();

        var factory = new PdfiumDocumentFactory();
        var infoService = new PdfiumDocumentInfoService();
        await using var document = await factory.OpenAsync(path, "secret");

        var info = infoService.GetInfo(document);
        info.IsEncrypted.Should().BeTrue();
        info.SecurityHandlerRevision.Should().BeGreaterThanOrEqualTo(0);
        info.PageCount.Should().Be(1);
    }

    private static string CreateInfoPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-info-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = "Glyph Title";
        builder.DocumentInformation.Author = "Glyph Author";
        builder.DocumentInformation.Subject = "Glyph Subject";
        builder.DocumentInformation.Keywords = "glyph, test";

        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.Letter);
        page.AddText("Info sample", 14, new PdfPoint(72, 720), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
