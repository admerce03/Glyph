using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class CaptureFileNameTests
{
    [Fact]
    public void CameraPng_and_ScanPdf_use_timestamp()
    {
        var t = new DateTime(2026, 10, 5, 13, 45, 33);
        CaptureFileName.CameraPng(t).Should().Be("Camera-20261005-134533.png");
        CaptureFileName.ScanPdf(t).Should().Be("Scan-20261005-134533.pdf");
    }

    [Fact]
    public void ScanInsertTempPdf_is_unique_prefix()
    {
        var a = CaptureFileName.ScanInsertTempPdf();
        var b = CaptureFileName.ScanInsertTempPdf();
        a.Should().StartWith("glyph-scan-insert-");
        a.Should().EndWith(".pdf");
        a.Should().NotBe(b);
    }
}
