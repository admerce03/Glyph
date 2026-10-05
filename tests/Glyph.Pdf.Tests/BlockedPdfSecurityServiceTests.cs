using FluentAssertions;
using Glyph.Core.Pdf;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Security;

namespace Glyph.Pdf.Tests;

public class BlockedPdfSecurityServiceTests
{
    private readonly BlockedPdfSecurityService _sut = new();

    [Fact]
    public void Write_protect_is_blocked_with_adr_015()
    {
        _sut.WriteProtectSupported.Should().BeFalse();
        _sut.BlockingAdr.Should().Be("ADR-015");
        _sut.UnavailableReason.Should().Contain("ADR-015");
        _sut.UnavailableReason.Should().Be(PdfPasswordWriteBlockedPolicy.Reason);
    }

    [Fact]
    public async Task SetOpenPasswordAsync_returns_structured_failure()
    {
        await using var doc = new StubPdfDocument();
        var result = await _sut.SetOpenPasswordAsync(doc, "secret");
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("ADR-015");
    }

    [Fact]
    public async Task RemoveProtectionAsync_returns_structured_failure()
    {
        await using var doc = new StubPdfDocument();
        var result = await _sut.RemoveProtectionAsync(doc, "owner");
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("ADR-015");
    }

    private sealed class StubPdfDocument : IPdfDocument
    {
        public string? Path { get; set; }

        public int PageCount => 1;

        public bool IsEncrypted => false;

        public event EventHandler? PagesChanged
        {
            add { }
            remove { }
        }

        public IPdfPage GetPage(int pageIndex) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}

public class PdfSecurityWriteUiCopyTests
{
    [Fact]
    public void Dialog_copy_mentions_adr_and_open_path()
    {
        PdfSecurityWriteUiCopy.ToolbarLabel.Should().Be("Protect");
        PdfSecurityWriteUiCopy.DialogBody().Should().Contain("ADR-015");
        PdfSecurityWriteUiCopy.DialogBody().Should().Contain("PdfSharp");
        PdfSecurityWriteUiCopy.DialogBody().Should().Contain("Opening encrypted");
        PdfSecurityWriteUiCopy.StatusBlocked.Should().Contain("ADR-015");
    }
}
