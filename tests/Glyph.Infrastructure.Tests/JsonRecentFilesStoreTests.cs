using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Infrastructure.RecentFiles;

namespace Glyph.Infrastructure.Tests;

public class JsonRecentFilesStoreTests
{
    [Fact]
    public async Task AddAsync_prepends_unique_paths_and_persists()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-recent-" + Guid.NewGuid().ToString("N") + ".json");
        var docA = Path.Combine(Path.GetTempPath(), "glyph-a.pdf");
        var docB = Path.Combine(Path.GetTempPath(), "glyph-b.png");

        try
        {
            var store = new JsonRecentFilesStore(path, capacity: 3);

            await store.AddAsync(docA);
            await store.AddAsync(docB);
            await store.AddAsync(docA);

            var recent = store.GetRecent();
            recent.Should().HaveCount(2);
            recent[0].Path.Should().Be(Path.GetFullPath(docA));
            recent[0].Kind.Should().Be(DocumentKind.Pdf);
            recent[1].Kind.Should().Be(DocumentKind.Image);

            var reloaded = new JsonRecentFilesStore(path, capacity: 3);
            reloaded.GetRecent().Should().HaveCount(2);
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
    public async Task Capacity_is_enforced()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-recent-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonRecentFilesStore(path, capacity: 2);
            await store.AddAsync(Path.Combine(Path.GetTempPath(), "1.pdf"));
            await store.AddAsync(Path.Combine(Path.GetTempPath(), "2.pdf"));
            await store.AddAsync(Path.Combine(Path.GetTempPath(), "3.pdf"));

            store.GetRecent().Select(e => e.DisplayName).Should().Equal("3.pdf", "2.pdf");
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
    public async Task Load_trims_when_capacity_provider_shrinks()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-recent-cap-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var seed = new JsonRecentFilesStore(path, capacity: 5);
            await seed.AddAsync(Path.Combine(Path.GetTempPath(), "a.pdf"));
            await seed.AddAsync(Path.Combine(Path.GetTempPath(), "b.pdf"));
            await seed.AddAsync(Path.Combine(Path.GetTempPath(), "c.pdf"));
            seed.GetRecent().Should().HaveCount(3);

            var capacity = 5;
            var live = new JsonRecentFilesStore(path, () => capacity);
            live.GetRecent().Should().HaveCount(3);

            capacity = 1;
            live.GetRecent().Select(e => e.DisplayName).Should().Equal("c.pdf");

            var reloaded = new JsonRecentFilesStore(path, capacity: 1);
            reloaded.GetRecent().Select(e => e.DisplayName).Should().Equal("c.pdf");
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
    public async Task ClearAsync_empties_persisted_list()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-recent-clear-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonRecentFilesStore(path, capacity: 5);
            await store.AddAsync(Path.Combine(Path.GetTempPath(), "keep-me.pdf"));
            store.GetRecent().Should().ContainSingle();

            await store.ClearAsync();
            store.GetRecent().Should().BeEmpty();

            var reloaded = new JsonRecentFilesStore(path, capacity: 5);
            reloaded.GetRecent().Should().BeEmpty();
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
