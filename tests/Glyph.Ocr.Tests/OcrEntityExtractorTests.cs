using FluentAssertions;
using Glyph.Ocr.Abstractions;
using Glyph.Ocr.Entities;

namespace Glyph.Ocr.Tests;

public class OcrEntityExtractorTests
{
    [Fact]
    public void Extract_finds_url_email_phone_and_date()
    {
        var text = "Visit https://example.com or mail a@b.co, call 555-123-4567 on 2024-10-04.";
        var entities = OcrEntityExtractor.Extract(text);

        entities.Should().Contain(e => e.Kind == OcrEntityKind.Url && e.Value.Contains("example.com"));
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Email && e.Value == "a@b.co");
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Phone && e.Value.Contains("555"));
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Date && e.Value.Contains("2024"));
    }
}
