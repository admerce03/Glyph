using FluentAssertions;
using Glyph.Imaging.Magick;
using ImageMagick;

namespace Glyph.Imaging.Tests;

public class MagickWriteImagesAsPdfTests
{
    [Fact]
    public async Task WriteImagesAsPdfAsync_creates_multi_page_pdf()
    {
        var a = Path.Combine(Path.GetTempPath(), "glyph-scan-a-" + Guid.NewGuid().ToString("N") + ".png");
        var b = Path.Combine(Path.GetTempPath(), "glyph-scan-b-" + Guid.NewGuid().ToString("N") + ".png");
        var pdf = Path.Combine(Path.GetTempPath(), "glyph-scan-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            using (var img = new MagickImage(MagickColors.Red, 40, 30))
            {
                img.Format = MagickFormat.Png;
                await img.WriteAsync(a);
            }

            using (var img = new MagickImage(MagickColors.Blue, 40, 30))
            {
                img.Format = MagickFormat.Png;
                await img.WriteAsync(b);
            }

            var encoder = new MagickImageEncoder();
            await encoder.WriteImagesAsPdfAsync([a, b], pdf);
            File.Exists(pdf).Should().BeTrue();
            new FileInfo(pdf).Length.Should().BeGreaterThan(100);
            // Reading PDF back may require Ghostscript; verify PDF header instead.
            await using var stream = File.OpenRead(pdf);
            var header = new byte[5];
            _ = await stream.ReadAsync(header);
            System.Text.Encoding.ASCII.GetString(header).Should().Be("%PDF-");
        }
        finally
        {
            foreach (var path in new[] { a, b, pdf })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
