using System.Text;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using PDFiumCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

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
            info.Creator.Should().Be("Glyph Creator");
            info.Producer.Should().Be("Glyph Producer");
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

    [Fact]
    public async Task SetInfo_updates_title_author_subject_keywords_creator_producer()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            infoService.SetInfo(
                document,
                new PdfDocumentInfoUpdate(
                    Title: "New Title",
                    Author: "New Author",
                    Subject: "New Subject",
                    Keywords: "alpha, beta",
                    Creator: "New Creator",
                    Producer: "New Producer"));

            var info = infoService.GetInfo(document);
            info.Title.Should().Be("New Title");
            info.Author.Should().Be("New Author");
            info.Subject.Should().Be("New Subject");
            info.Keywords.Should().Be("alpha, beta");
            info.Creator.Should().Be("New Creator");
            info.Producer.Should().Be("New Producer");
            info.ModificationDate.Should().NotBeNullOrWhiteSpace();
            info.ModificationDate.Should().StartWith("D:");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetInfo_preserves_creation_date_when_updating()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            // Seed CreationDate via a clear-then-set cycle that writes ModDate, then set fields
            // while preserving whatever CreationDate PDFium already exposed (often null on PdfPig).
            infoService.SetInfo(
                document,
                new PdfDocumentInfoUpdate(Title: "Seed"));
            var afterSeed = infoService.GetInfo(document);
            var creation = afterSeed.CreationDate;

            infoService.SetInfo(
                document,
                new PdfDocumentInfoUpdate(Title: "Updated Again"));

            var info = infoService.GetInfo(document);
            info.Title.Should().Be("Updated Again");
            info.CreationDate.Should().Be(creation);
            info.ModificationDate.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetInfo_preserves_creator_producer_when_omitted()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            var before = infoService.GetInfo(document);
            before.Creator.Should().Be("Glyph Creator");
            before.Producer.Should().Be("Glyph Producer");

            infoService.SetInfo(
                document,
                new PdfDocumentInfoUpdate(Title: "Only Title"));

            var info = infoService.GetInfo(document);
            info.Title.Should().Be("Only Title");
            info.Creator.Should().Be("Glyph Creator");
            info.Producer.Should().Be("Glyph Producer");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetInfo_clear_all_removes_info_fields()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);

            infoService.SetInfo(document, new PdfDocumentInfoUpdate(ClearAll: true));
            var info = infoService.GetInfo(document);
            info.Title.Should().BeNullOrEmpty();
            info.Author.Should().BeNullOrEmpty();
            info.Subject.Should().BeNullOrEmpty();
            info.Keywords.Should().BeNullOrEmpty();
            info.Creator.Should().BeNullOrEmpty();
            info.Producer.Should().BeNullOrEmpty();
            info.CreationDate.Should().BeNullOrEmpty();
            info.ModificationDate.Should().BeNullOrEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ListAttachments_and_GetAttachmentBytes_round_trip()
    {
        var path = CreateInfoPdf();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var infoService = new PdfiumDocumentInfoService();
            await using var document = await factory.OpenAsync(path);
            var pdfium = (PdfiumDocument)document;

            var payload = Encoding.UTF8.GetBytes("glyph-attachment-payload");
            lock (PdfiumSync.Gate)
            {
                AddAttachment(pdfium.Handle, "note.txt", payload);
            }

            var listed = infoService.ListAttachments(document);
            listed.Should().ContainSingle();
            listed[0].Name.Should().Be("note.txt");
            listed[0].SizeBytes.Should().Be(payload.Length);

            var bytes = infoService.GetAttachmentBytes(document, listed[0].Index);
            bytes.Should().Equal(payload);

            infoService.GetInfo(document).EmbeddedAttachmentCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static unsafe void AddAttachment(FpdfDocumentT handle, string name, byte[] contents)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name + "\0");
        fixed (byte* namePtr = nameBytes)
        {
            var attachment = fpdf_attachment.FPDFDocAddAttachment(handle, ref *(ushort*)namePtr);
            attachment.Should().NotBeNull();
            fixed (byte* dataPtr = contents)
            {
                fpdf_attachment.FPDFAttachmentSetFile(
                        attachment,
                        handle,
                        (IntPtr)dataPtr,
                        (uint)contents.Length)
                    .Should().NotBe(0);
            }
        }
    }

    private static string CreateInfoPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-info-" + Guid.NewGuid().ToString("N") + ".pdf");
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = "Glyph Title";
        builder.DocumentInformation.Author = "Glyph Author";
        builder.DocumentInformation.Subject = "Glyph Subject";
        builder.DocumentInformation.Keywords = "glyph, test";
        builder.DocumentInformation.Creator = "Glyph Creator";
        builder.DocumentInformation.Producer = "Glyph Producer";

        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.Letter);
        page.AddText("Info sample", 14, new PdfPoint(72, 720), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
