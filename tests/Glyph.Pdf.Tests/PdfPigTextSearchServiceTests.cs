using FluentAssertions;
using Glyph.Pdf.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Glyph.Pdf.Tests;

public class PdfPigTextSearchServiceTests
{
    [Fact]
    public async Task Search_finds_matches_across_pages()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-search-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            CreateSearchSample(path);
            var search = new PdfPigTextSearchService();

            var hits = await search.SearchAsync(path, "alpha");
            hits.Should().ContainSingle();
            hits[0].PageIndex.Should().Be(0);
            hits[0].Snippet.Should().ContainEquivalentOf("alpha");

            var page2 = await search.SearchAsync(path, "bravo");
            page2.Should().ContainSingle();
            page2[0].PageIndex.Should().Be(1);
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
    public async Task Search_respects_case_sensitivity()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-search-case-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            CreateSearchSample(path);
            var search = new PdfPigTextSearchService();

            (await search.SearchAsync(path, "ALPHA", caseSensitive: true)).Should().BeEmpty();
            (await search.SearchAsync(path, "ALPHA", caseSensitive: false)).Should().ContainSingle();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void CreateSearchSample(string path)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page1 = builder.AddPage(PageSize.Letter);
        page1.AddText("Hello alpha world", 14, new PdfPoint(50, 700), font);
        var page2 = builder.AddPage(PageSize.Letter);
        page2.AddText("Second page with bravo", 14, new PdfPoint(50, 700), font);
        File.WriteAllBytes(path, builder.Build());
    }
}
