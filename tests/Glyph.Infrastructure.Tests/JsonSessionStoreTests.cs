using FluentAssertions;
using Glyph.Infrastructure.Session;

namespace Glyph.Infrastructure.Tests;

public class JsonSessionStoreTests
{
    [Fact]
    public async Task Save_and_load_round_trips_paths_and_active_index()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "glyph-session-" + Guid.NewGuid().ToString("N") + ".json");
        var fileA = Path.Combine(Path.GetTempPath(), "glyph-a-" + Guid.NewGuid().ToString("N") + ".pdf");
        var fileB = Path.Combine(Path.GetTempPath(), "glyph-b-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await File.WriteAllTextAsync(fileA, "%PDF");
            await File.WriteAllTextAsync(fileB, "png");
            var store = new JsonSessionStore(storePath);
            await store.SaveAsync(new SessionState
            {
                Paths = [fileA, fileB],
                ActiveIndex = 1,
            });

            var loaded = await store.TryLoadAsync();
            loaded.Should().NotBeNull();
            loaded!.Paths.Should().HaveCount(2);
            loaded.Paths[0].Should().Be(Path.GetFullPath(fileA));
            loaded.Paths[1].Should().Be(Path.GetFullPath(fileB));
            loaded.ActiveIndex.Should().Be(1);
        }
        finally
        {
            if (File.Exists(storePath))
            {
                File.Delete(storePath);
            }

            if (File.Exists(fileA))
            {
                File.Delete(fileA);
            }

            if (File.Exists(fileB))
            {
                File.Delete(fileB);
            }
        }
    }

    [Fact]
    public async Task Load_skips_missing_files_and_returns_null_when_empty()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "glyph-session-miss-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSessionStore(storePath);
            await store.SaveAsync(new SessionState
            {
                Paths = [Path.Combine(Path.GetTempPath(), "glyph-missing-" + Guid.NewGuid().ToString("N") + ".pdf")],
                ActiveIndex = 0,
            });

            (await store.TryLoadAsync()).Should().BeNull();
        }
        finally
        {
            if (File.Exists(storePath))
            {
                File.Delete(storePath);
            }
        }
    }

    [Fact]
    public async Task Clear_removes_session_file()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "glyph-session-clear-" + Guid.NewGuid().ToString("N") + ".json");
        var file = Path.Combine(Path.GetTempPath(), "glyph-clear-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllTextAsync(file, "%PDF");
            var store = new JsonSessionStore(storePath);
            await store.SaveAsync(new SessionState { Paths = [file] });
            File.Exists(storePath).Should().BeTrue();
            await store.ClearAsync();
            File.Exists(storePath).Should().BeFalse();
            (await store.TryLoadAsync()).Should().BeNull();
        }
        finally
        {
            if (File.Exists(storePath))
            {
                File.Delete(storePath);
            }

            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
