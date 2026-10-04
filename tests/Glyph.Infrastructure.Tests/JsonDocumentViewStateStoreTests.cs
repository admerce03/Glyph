using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Infrastructure.Documents;

namespace Glyph.Infrastructure.Tests;

public class JsonDocumentViewStateStoreTests
{
    [Fact]
    public async Task Save_and_load_round_trips_page_zoom_and_layout()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-view-state-" + Guid.NewGuid().ToString("N") + ".json");
        var pdfPath = Path.Combine(Path.GetTempPath(), "sample-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllText(pdfPath, "%PDF-stub");
            var store = new JsonDocumentViewStateStore(path);
            await store.SaveAsync(pdfPath, new DocumentViewState
            {
                Zoom = 1.5,
                CurrentPageIndex = 7,
                PageLayout = PageLayoutMode.TwoPage,
            });

            var loaded = await store.TryLoadAsync(pdfPath);
            loaded.Should().NotBeNull();
            loaded!.Zoom.Should().Be(1.5);
            loaded.CurrentPageIndex.Should().Be(7);
            loaded.PageLayout.Should().Be(PageLayoutMode.TwoPage);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }
        }
    }

    [Fact]
    public async Task Missing_entry_returns_null()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-view-state-empty-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonDocumentViewStateStore(path);
            (await store.TryLoadAsync(Path.Combine(Path.GetTempPath(), "nope.pdf"))).Should().BeNull();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
