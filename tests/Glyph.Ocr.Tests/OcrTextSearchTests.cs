using FluentAssertions;
using Glyph.Ocr.Abstractions;

namespace Glyph.Ocr.Tests;

public class OcrTextSearchTests
{
    [Fact]
    public void FindWordIndexes_matches_case_insensitive_substring()
    {
        var words = new[]
        {
            new OcrWord("Hello", 0, 0, 10, 4),
            new OcrWord("WORLD", 12, 0, 12, 4),
            new OcrWord("here", 30, 0, 8, 4),
        };

        OcrTextSearch.FindWordIndexes(words, "wor").Should().Equal(1);
        OcrTextSearch.FindWordIndexes(words, "HE").Should().Equal(0, 2);
        OcrTextSearch.FindWordIndexes(words, "zzz").Should().BeEmpty();
    }

    [Fact]
    public void FindWordIndexes_respects_case_sensitive_flag()
    {
        var words = new[] { new OcrWord("Pdf", 0, 0, 6, 4) };
        OcrTextSearch.FindWordIndexes(words, "pdf", caseSensitive: true).Should().BeEmpty();
        OcrTextSearch.FindWordIndexes(words, "Pdf", caseSensitive: true).Should().Equal(0);
    }

    [Fact]
    public void FlattenWords_walks_lines_in_order()
    {
        var result = new OcrResult(
            "a b c",
            [
                new OcrLine("a b", [new OcrWord("a", 0, 0, 1, 1), new OcrWord("b", 2, 0, 1, 1)]),
                new OcrLine("c", [new OcrWord("c", 0, 10, 1, 1)]),
            ]);
        OcrTextSearch.FlattenWords(result).Select(w => w.Text).Should().Equal("a", "b", "c");
    }
}
