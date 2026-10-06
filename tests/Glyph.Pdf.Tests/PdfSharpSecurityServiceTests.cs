using FluentAssertions;
using Glyph.Core.Pdf;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Security;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfSharpSecurityServiceTests
{
    private readonly PdfSharpSecurityService _sut = new();
    private readonly PdfiumDocumentFactory _factory = new();

    [Fact]
    public void Write_protect_is_supported_after_adr_015_accept_a()
    {
        _sut.WriteProtectSupported.Should().BeTrue();
        _sut.BlockingAdr.Should().BeEmpty();
        _sut.UnavailableReason.Should().BeEmpty();
        PdfPasswordWriteBlockedPolicy.CreatePasswordProtectedSupported.Should().BeTrue();
        PdfPasswordWriteBlockedPolicy.AcceptedOption.Should().Be("A");
    }

    [Fact]
    public async Task SetOpenPassword_round_trip_with_pdfium_open()
    {
        var path = CreateTempPlainPdf();
        try
        {
            await using var document = await _factory.OpenAsync(path);
            document.IsEncrypted.Should().BeFalse();

            var applied = await _sut.SetOpenPasswordAsync(document, "secret");
            applied.Succeeded.Should().BeTrue(applied.Message);
            document.IsEncrypted.Should().BeTrue();

            var missing = async () => await _factory.OpenAsync(path);
            await missing.Should().ThrowAsync<PdfPasswordRequiredException>()
                .Where(ex => !ex.PasswordWasProvided);

            var wrong = async () => await _factory.OpenAsync(path, "wrong-password");
            await wrong.Should().ThrowAsync<PdfPasswordRequiredException>()
                .Where(ex => ex.PasswordWasProvided);

            await using var reopened = await _factory.OpenAsync(path, "secret");
            reopened.PageCount.Should().Be(document.PageCount);
            reopened.IsEncrypted.Should().BeTrue();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task RemoveProtection_allows_open_without_password()
    {
        var path = CreateTempPlainPdf();
        try
        {
            await using (var document = await _factory.OpenAsync(path))
            {
                (await _sut.SetOpenPasswordAsync(document, "secret")).Succeeded.Should().BeTrue();
                document.IsEncrypted.Should().BeTrue();

                var removed = await _sut.RemoveProtectionAsync(document);
                removed.Succeeded.Should().BeTrue(removed.Message);
                document.IsEncrypted.Should().BeFalse();
            }

            await using var plain = await _factory.OpenAsync(path);
            plain.IsEncrypted.Should().BeFalse();
            plain.PageCount.Should().BeGreaterThan(0);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task SetPermissions_restricts_and_keeps_open_password()
    {
        var path = CreateTempPlainPdf();
        try
        {
            await using var document = await _factory.OpenAsync(path);
            var result = await _sut.SetPermissionsAsync(
                document,
                ownerPassword: "owner-secret",
                permissions: PdfSecurityPermissions.RestrictAll,
                userPassword: "open-secret");
            result.Succeeded.Should().BeTrue(result.Message);
            document.IsEncrypted.Should().BeTrue();

            await using var reopened = await _factory.OpenAsync(path, "open-secret");
            reopened.IsEncrypted.Should().BeTrue();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Protect_existing_encrypted_fixture_then_remove()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "encrypted.pdf");
        File.Exists(fixture).Should().BeTrue();
        var path = Path.Combine(Path.GetTempPath(), $"glyph-enc-fixture-{Guid.NewGuid():N}.pdf");
        File.Copy(fixture, path, overwrite: true);
        try
        {
            await using var document = await _factory.OpenAsync(path, "secret");
            document.IsEncrypted.Should().BeTrue();

            var changed = await _sut.SetOpenPasswordAsync(document, "new-secret");
            changed.Succeeded.Should().BeTrue(changed.Message);

            var wrong = async () => await _factory.OpenAsync(path, "secret");
            await wrong.Should().ThrowAsync<PdfPasswordRequiredException>();

            await using var withNew = await _factory.OpenAsync(path, "new-secret");
            (await _sut.RemoveProtectionAsync(withNew)).Succeeded.Should().BeTrue();
            withNew.IsEncrypted.Should().BeFalse();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string CreateTempPlainPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"glyph-sec-{Guid.NewGuid():N}.pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Glyph security fixture", 18, new PdfPoint(50, 750), font);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
