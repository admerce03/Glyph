using System.Diagnostics;
using System.Globalization;
using System.Text;
using Glyph.Ocr.Abstractions;
using Glyph.Ocr.Entities;

namespace Glyph.Ocr.Tesseract;

/// <summary>
/// Offline OCR via the system <c>tesseract</c> CLI (no network).
/// </summary>
public sealed class TesseractCliOcrEngine : IOcrEngine
{
    private readonly string _executablePath;

    public TesseractCliOcrEngine(string? executablePath = null)
    {
        _executablePath = executablePath ?? FindExecutable() ?? "tesseract";
    }

    public string EngineName => "Tesseract CLI";

    public bool IsAvailable => File.Exists(_executablePath) || FindExecutable() is not null;

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PixelWidth <= 0 || request.PixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (request.BgraPixels.Length < checked(request.PixelWidth * request.PixelHeight * 4))
        {
            throw new ArgumentException("BGRA buffer is too small for the declared dimensions.", nameof(request));
        }

        request.Progress?.Report(new OcrProgress(0.05, "Preparing image…"));
        var tempDir = Path.Combine(Path.GetTempPath(), "glyph-ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var pngPath = Path.Combine(tempDir, "page.png");
        var outBase = Path.Combine(tempDir, "out");
        try
        {
            await WritePngAsync(pngPath, request.PixelWidth, request.PixelHeight, request.BgraPixels, cancellationToken);
            request.Progress?.Report(new OcrProgress(0.35, "Running Tesseract…"));

            var lang = string.IsNullOrWhiteSpace(request.LanguageTag) ? "eng" : request.LanguageTag.Replace('-', '_');
            var psi = new ProcessStartInfo
            {
                FileName = _executablePath,
                ArgumentList = { pngPath, outBase, "-l", lang, "tsv" },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start tesseract.");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var err = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException("tesseract failed: " + err.Trim());
            }

            request.Progress?.Report(new OcrProgress(0.8, "Parsing OCR output…"));
            var tsvPath = outBase + ".tsv";
            var textPath = outBase + ".txt";
            // tesseract with tsv still writes .tsv; also request text via reading words.
            var lines = ParseTsv(await File.ReadAllTextAsync(tsvPath, cancellationToken));
            var text = lines.Count > 0
                ? string.Join('\n', lines.Select(l => l.Text))
                : (File.Exists(textPath) ? (await File.ReadAllTextAsync(textPath, cancellationToken)).Trim() : string.Empty);
            var entities = OcrEntityExtractor.Extract(text);
            request.Progress?.Report(new OcrProgress(1.0, "Done"));
            return new OcrResult(text, lines, entities);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static string? FindExecutable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? "tesseract.exe" : "tesseract");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return File.Exists("/usr/bin/tesseract") ? "/usr/bin/tesseract" : null;
    }

    private static async Task WritePngAsync(string path, int width, int height, byte[] bgra, CancellationToken cancellationToken)
    {
        // Minimal PNG writer (8-bit RGBA) to avoid pulling imaging into OCR for the CLI path.
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var src = i * 4;
            var dst = i * 4;
            rgba[dst] = bgra[src + 2];
            rgba[dst + 1] = bgra[src + 1];
            rgba[dst + 2] = bgra[src];
            rgba[dst + 3] = bgra[src + 3];
        }

        await using var stream = File.Create(path);
        await WritePngRgbaAsync(stream, width, height, rgba, cancellationToken);
    }

    private static async Task WritePngRgbaAsync(Stream stream, int width, int height, byte[] rgba, CancellationToken cancellationToken)
    {
        // Signature
        await stream.WriteAsync(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, cancellationToken);

        // IHDR
        var ihdr = new byte[13];
        WriteInt(ihdr, 0, width);
        WriteInt(ihdr, 4, height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        await WriteChunkAsync(stream, "IHDR", ihdr, cancellationToken);

        // IDAT (zlib-compressed scanlines)
        var raw = new byte[(width * 4 + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (width * 4 + 1);
            raw[rowStart] = 0; // filter None
            Buffer.BlockCopy(rgba, y * width * 4, raw, rowStart + 1, width * 4);
        }

        using var deflate = new MemoryStream();
        // zlib header + deflate + adler32
        deflate.WriteByte(0x78);
        deflate.WriteByte(0x01);
        await using (var zlib = new System.IO.Compression.DeflateStream(deflate, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            await zlib.WriteAsync(raw, cancellationToken);
        }

        var adler = Adler32(raw);
        var adlerBytes = BitConverter.GetBytes(adler);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(adlerBytes);
        }

        deflate.Write(adlerBytes, 0, 4);
        await WriteChunkAsync(stream, "IDAT", deflate.ToArray(), cancellationToken);
        await WriteChunkAsync(stream, "IEND", [], cancellationToken);
    }

    private static async Task WriteChunkAsync(Stream stream, string type, byte[] data, CancellationToken cancellationToken)
    {
        var len = BitConverter.GetBytes(data.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(len);
        }

        await stream.WriteAsync(len, cancellationToken);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        await stream.WriteAsync(typeBytes, cancellationToken);
        if (data.Length > 0)
        {
            await stream.WriteAsync(data, cancellationToken);
        }

        var crc = Crc32(typeBytes, data);
        var crcBytes = BitConverter.GetBytes(crc);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(crcBytes);
        }

        await stream.WriteAsync(crcBytes, cancellationToken);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var t in data)
        {
            a = (a + t) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static IReadOnlyList<OcrLine> ParseTsv(string tsv)
    {
        var wordsByLine = new Dictionary<int, List<OcrWord>>();
        var lineText = new Dictionary<int, StringBuilder>();
        using var reader = new StringReader(tsv);
        _ = reader.ReadLine(); // header
        string? row;
        while ((row = reader.ReadLine()) is not null)
        {
            var parts = row.Split('\t');
            if (parts.Length < 12)
            {
                continue;
            }

            if (!int.TryParse(parts[0], out var level) || level != 5)
            {
                continue;
            }

            if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left)
                || !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top)
                || !int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
                || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNum))
            {
                continue;
            }

            var word = parts[11];
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            if (!wordsByLine.TryGetValue(lineNum, out var list))
            {
                list = [];
                wordsByLine[lineNum] = list;
                lineText[lineNum] = new StringBuilder();
            }

            list.Add(new OcrWord(word, left, top, width, height));
            if (lineText[lineNum].Length > 0)
            {
                lineText[lineNum].Append(' ');
            }

            lineText[lineNum].Append(word);
        }

        return wordsByLine.Keys.OrderBy(k => k)
            .Select(k => new OcrLine(lineText[k].ToString(), wordsByLine[k]))
            .ToList();
    }
}
