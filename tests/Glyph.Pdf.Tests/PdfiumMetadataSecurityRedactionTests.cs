using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfiumMetadataSecurityRedactionTests
{
    [Fact]
    public async Task Metadata_sidecar_set_is_returned_preferentially_on_get()
    {
        var path = CreatePdfWithSecretText();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var meta = new PdfiumMetadataService();
            await using var document = await factory.OpenAsync(path);

            await meta.SetAsync(document, "Glyph Title", "Author A", "Subject S", "alpha, beta");
            var got = await meta.GetAsync(document);

            got.Title.Should().Be("Glyph Title");
            got.Author.Should().Be("Author A");
            got.Subject.Should().Be("Subject S");
            got.Keywords.Should().Be("alpha, beta");
            got.PageCount.Should().Be(1);
            got.FileSizeBytes.Should().NotBeNull();
            got.FirstPageWidthPoints.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
            var sidecar = path + ".glyph-meta.json";
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }

    [Fact]
    public async Task Security_info_reports_unencrypted_document_permissions()
    {
        var path = CreatePdfWithSecretText();
        try
        {
            var factory = new PdfiumDocumentFactory();
            var security = new PdfiumSecurityService(new PdfiumSecurityInfoService());
            await using var document = await factory.OpenAsync(path);
            var info = await security.GetInfoAsync(document);
            info.IsEncrypted.Should().BeFalse();
            info.CanPrint.Should().BeTrue();
            PdfSecurityInfo.PermissionEnforcementWarning.Should().Contain("advisory");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Protect_round_trip_requires_password_and_remove_protection_clears_it()
    {
        var path = CreatePdfWithSecretText();
        var protectedPath = Path.Combine(Path.GetTempPath(), "glyph-prot-" + Guid.NewGuid().ToString("N") + ".pdf");
        var clearedPath = Path.Combine(Path.GetTempPath(), "glyph-clear-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var security = new PdfiumSecurityService(new PdfiumSecurityInfoService());
            await using (var document = await factory.OpenAsync(path))
            {
                await security.ProtectAsync(
                    document,
                    protectedPath,
                    userPassword: "user-secret",
                    ownerPassword: "owner-secret",
                    deny: PdfPermissionFlags.DenyCopy);
            }

            var openWrong = async () => await factory.OpenAsync(protectedPath, "wrong");
            await openWrong.Should().ThrowAsync<PdfPasswordRequiredException>();

            await using (var opened = await factory.OpenAsync(protectedPath, "user-secret"))
            {
                opened.IsEncrypted.Should().BeTrue();
                var info = await security.GetInfoAsync(opened);
                info.IsEncrypted.Should().BeTrue();
                await security.RemoveProtectionAsync(opened, clearedPath);
            }

            await using var cleared = await factory.OpenAsync(clearedPath);
            cleared.IsEncrypted.Should().BeFalse();
            var infoCleared = await security.GetInfoAsync(cleared);
            infoCleared.IsEncrypted.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(protectedPath))
            {
                File.Delete(protectedPath);
            }

            if (File.Exists(clearedPath))
            {
                File.Delete(clearedPath);
            }
        }
    }

    [Fact]
    public async Task Redaction_apply_removes_extractable_secret_text()
    {
        var path = CreatePdfWithSecretText();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-redact-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var redaction = new PdfiumRedactionService();
            var editor = new PdfiumPageEditor();
            var extractor = new PdfiumTextExtractor();

            await using (var document = await factory.OpenAsync(path))
            {
                var chars = await extractor.GetCharsAsync(document, 0);
                chars.Should().NotBeEmpty();
                var secret = chars.Where(c => "SECRET".Contains(c.Value, StringComparison.Ordinal)).ToList();
                secret.Should().NotBeEmpty();
                var left = secret.Min(c => c.Bounds.Left) - 1;
                var bottom = secret.Min(c => c.Bounds.Bottom) - 1;
                var right = secret.Max(c => c.Bounds.Right) + 1;
                var top = secret.Max(c => c.Bounds.Top) + 1;

                var mark = await redaction.MarkRectAsync(document, 0, new PdfRect(left, bottom, right, top));
                var pending = await redaction.ListAsync(document);
                pending.Should().ContainSingle(m => m.Id == mark.Id);

                await redaction.ApplyAsync(document);
                (await redaction.ListAsync(document)).Should().BeEmpty();

                await editor.SaveAsync(document, outPath);
            }

            await using var reopened = await factory.OpenAsync(outPath);
            var text = await extractor.GetTextAsync(reopened, 0);
            text.Should().NotContain("SECRET");
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task Optimization_estimate_and_optimize_write_output()
    {
        var path = CreatePdfWithSecretText();
        var outPath = Path.Combine(Path.GetTempPath(), "glyph-opt-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var factory = new PdfiumDocumentFactory();
            var optimizer = new PdfiumOptimizationService();
            await using var document = await factory.OpenAsync(path);
            var estimate = await optimizer.EstimateAsync(
                document,
                new PdfOptimizationOptions(PdfOptimizationPreset.Balanced, RemoveMetadata: true));
            estimate.EstimatedBytes.Should().BeGreaterThan(0);
            estimate.SourceBytes.Should().BeGreaterThan(0);

            await optimizer.OptimizeAsync(
                document,
                new PdfOptimizationOptions(PdfOptimizationPreset.Lossless),
                outPath);
            File.Exists(outPath).Should().BeTrue();
            new FileInfo(outPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    private static string CreatePdfWithSecretText()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.Letter);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Visible header", 14, new PdfPoint(72, 720), font);
        page.AddText("SECRET code 999", 18, new PdfPoint(72, 680), font);
        page.AddText("Trailing content", 12, new PdfPoint(72, 640), font);
        var path = Path.Combine(Path.GetTempPath(), "glyph-m7-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
