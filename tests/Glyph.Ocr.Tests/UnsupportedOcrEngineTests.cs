using FluentAssertions;
using Glyph.Ocr;
using Glyph.Ocr.Abstractions;
using Xunit;

namespace Glyph.Ocr.Tests;

public class UnsupportedOcrEngineTests
{
    [Fact]
    public async Task Recognize_throws_platform_not_supported()
    {
        var engine = new UnsupportedOcrEngine();
        var request = new OcrRequest(2, 2, new byte[16]);
        var act = async () => await engine.RecognizeAsync(request);
        await act.Should().ThrowAsync<PlatformNotSupportedException>();
    }
}

public class FakeOcrEngineTests
{
    [Fact]
    public async Task Fake_engine_returns_text_and_word_boxes()
    {
        IOcrEngine engine = new FakeOcrEngine("hello", new OcrWord("hello", 1, 2, 10, 4));
        var result = await engine.RecognizeAsync(new OcrRequest(20, 10, new byte[800]));
        result.Text.Should().Be("hello");
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Words[0].X.Should().Be(1);
        result.Lines[0].Words[0].Width.Should().Be(10);
    }

    private sealed class FakeOcrEngine(string text, params OcrWord[] words) : IOcrEngine
    {
        public Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OcrResult(text, [new OcrLine(text, words)]));
        }
    }
}
