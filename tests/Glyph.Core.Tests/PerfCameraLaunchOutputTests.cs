using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class PerformanceBehaviorPolicyTests
{
    [Fact]
    public void Virtualization_and_lazy_ocr()
    {
        PerformanceBehaviorPolicy.PreferVisiblePageBeforeThumbs.Should().BeTrue();
        PerformanceBehaviorPolicy.LazyOcrRequiresExplicitRequest.Should().BeTrue();
        PerformanceBehaviorPolicy.IsOutsideMaterializedWindow(0, 50, 100).Should().BeTrue();
        PerformanceBehaviorPolicy.IsOutsideMaterializedWindow(50, 50, 100).Should().BeFalse();
        PerformanceBehaviorPolicy.CancelOcrButton.Should().Contain("Cancel");
        PerformanceBehaviorPolicy.ProgressSurfaces.Should().Contain("Scanning");
        PerformanceBehaviorPolicy.PageRenderCacheCapacity.Should().Be(32);
    }
}

public class WebcamCaptureUiTests
{
    [Fact]
    public void Capture_labels()
    {
        WebcamCaptureUi.CaptureButton.Should().Be("Camera");
        WebcamCaptureUi.InsertsIntoPdfAsStamp.Should().BeTrue();
        WebcamCaptureUi.OpensAsImageTab.Should().BeTrue();
        WebcamCaptureUi.StartingCamera.Should().Contain("camera");
        WebcamCaptureUi.CaptureCancelledOrUnavailable.Should().Contain("unavailable");
        WebcamCaptureUi.CaptureDiscarded.Should().Contain("discarded");
        WebcamCaptureUi.FormatOpenedCapture("a.png").Should().Contain("a.png");
    }
}

public class ExternalLaunchPolicyTests
{
    [Fact]
    public void Http_url_normalize()
    {
        ExternalLaunchPolicy.LooksLikeHttpUrl("https://example.com").Should().BeTrue();
        ExternalLaunchPolicy.LooksLikeHttpUrl("example.com").Should().BeFalse();
        ExternalLaunchPolicy.NormalizeHttpUrl("example.com").Should().Be("https://example.com");
        ExternalLaunchPolicy.NormalizeHttpUrl("http://a").Should().Be("http://a");
    }
}

public class ClipboardIntegrationPolicyTests
{
    [Fact]
    public void Paste_and_path_flags()
    {
        ClipboardIntegrationPolicy.PasteImageIntoImageDocument.Should().BeTrue();
        ClipboardIntegrationPolicy.CopyFilePathSupported.Should().BeTrue();
        ClipboardIntegrationPolicy.PastePathOpensWhenEmpty.Should().BeTrue();
    }
}

public class OutputFormatSupportTests
{
    [Fact]
    public void Pdf_output_paths()
    {
        OutputFormatSupport.SupportsPdfOutput.Should().BeTrue();
        OutputFormatSupport.PdfOutputPaths.Should().Contain("OCR → PDF");
    }
}

public class DocumentExportCompressionTests
{
    [Fact]
    public void Lossless_and_annotation_flatten()
    {
        DocumentExportFormats.SupportsLosslessCompression("WebP").Should().BeTrue();
        DocumentExportFormats.SupportsLosslessCompression("JPEG").Should().BeFalse();
        DocumentExportFormats.WebpLosslessLabel.Should().Contain("Lossless");
        DocumentExportFormats.AnnotationsFlattenedInRasterExport.Should().BeTrue();
    }
}
