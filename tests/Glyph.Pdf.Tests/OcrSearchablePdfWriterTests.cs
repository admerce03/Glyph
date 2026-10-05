using System.IO.Compression;
using FluentAssertions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Text;

namespace Glyph.Pdf.Tests;

public class OcrSearchablePdfWriterTests
{
    [Fact]
    public async Task BuildFromPng_embeds_invisible_text_that_search_finds()
    {
        var png = CreateSolidPng(200, 100);
        var words = new[]
        {
            new SearchablePdfWord("GlyphSearchable", X: 10, Y: 20, Width: 120, Height: 14),
        };

        var pdfBytes = OcrSearchablePdfWriter.BuildFromPng(png, pixelWidth: 200, pixelHeight: 100, words);
        pdfBytes.Length.Should().BeGreaterThan(100);

        var path = Path.Combine(Path.GetTempPath(), "glyph-ocr-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, pdfBytes);
            var search = new PdfPigTextSearchService();
            var result = await search.SearchAsync(path, "GlyphSearchable");
            result.Hits.Should().NotBeEmpty();
            result.Hits[0].Snippet.Should().ContainEquivalentOf("GlyphSearchable");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task BuildPages_writes_multi_page_searchable_pdf()
    {
        var png = CreateSolidPng(120, 80);
        var pages = new[]
        {
            (png, false, 120, 80, (IEnumerable<SearchablePdfWord>)[new SearchablePdfWord("Alpha", 8, 10, 40, 12)]),
            (png, false, 120, 80, (IEnumerable<SearchablePdfWord>)[new SearchablePdfWord("Beta", 8, 10, 40, 12)]),
        };

        var pdfBytes = OcrSearchablePdfWriter.BuildPages(pages);
        var path = Path.Combine(Path.GetTempPath(), "glyph-ocr-multi-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, pdfBytes);
            var search = new PdfPigTextSearchService();
            (await search.SearchAsync(path, "Alpha")).Hits.Should().NotBeEmpty();
            (await search.SearchAsync(path, "Beta")).Hits.Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static byte[] CreateSolidPng(int width, int height)
    {
        static byte[] Chunk(byte[] type, byte[] data)
        {
            var len = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(data.Length));
            var payload = type.Concat(data).ToArray();
            uint crc = 0xFFFFFFFF;
            foreach (var b in payload)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }

            crc ^= 0xFFFFFFFF;
            var crcBytes = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((int)crc));
            return len.Concat(payload).Concat(crcBytes).ToArray();
        }

        var ihdr = new byte[13];
        Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(width)), 0, ihdr, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(height)), 0, ihdr, 4, 4);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // RGB

        var row = new byte[1 + width * 3];
        for (var x = 0; x < width; x++)
        {
            row[1 + x * 3] = 255;
            row[2 + x * 3] = 255;
            row[3 + x * 3] = 255;
        }

        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.Write(row);
        }

        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        ms.Write(Chunk("IHDR"u8.ToArray(), ihdr));
        ms.Write(Chunk("IDAT"u8.ToArray(), Compress(raw.ToArray())));
        ms.Write(Chunk("IEND"u8.ToArray(), []));
        return ms.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
