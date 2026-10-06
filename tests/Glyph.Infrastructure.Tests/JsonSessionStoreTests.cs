using System.Text.Json;
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
            loaded.Windows.Should().HaveCount(1);
            loaded.Windows[0].Paths.Should().Equal(loaded.Paths);
            loaded.Windows[0].ActiveIndex.Should().Be(1);
        }
        finally
        {
            DeleteQuietly(storePath, fileA, fileB);
        }
    }

    [Fact]
    public async Task Load_migrates_legacy_flat_paths_into_windows()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "glyph-session-legacy-" + Guid.NewGuid().ToString("N") + ".json");
        var file = Path.Combine(Path.GetTempPath(), "glyph-legacy-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllTextAsync(file, "%PDF");
            var full = Path.GetFullPath(file);
            // Pre-multi-window JSON shape (no windows array); same camelCase as JsonSessionStore.
            var legacyJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["paths"] = new[] { full },
                    ["activeIndex"] = 0,
                    ["updatedAtUtc"] = "2026-01-01T00:00:00+00:00",
                });
            await File.WriteAllTextAsync(storePath, legacyJson);

            var store = new JsonSessionStore(storePath);
            var loaded = await store.TryLoadAsync();
            loaded.Should().NotBeNull();
            loaded!.Windows.Should().HaveCount(1);
            loaded.Windows[0].Id.Should().Be("legacy");
            loaded.Windows[0].Paths.Should().Equal(full);
            loaded.Paths.Should().Equal(full);
        }
        finally
        {
            DeleteQuietly(storePath, file);
        }
    }

    [Fact]
    public async Task UpsertWindow_merges_multiple_windows_and_empty_removes()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "glyph-session-mw-" + Guid.NewGuid().ToString("N") + ".json");
        var fileA = Path.Combine(Path.GetTempPath(), "glyph-mw-a-" + Guid.NewGuid().ToString("N") + ".pdf");
        var fileB = Path.Combine(Path.GetTempPath(), "glyph-mw-b-" + Guid.NewGuid().ToString("N") + ".pdf");
        var fileC = Path.Combine(Path.GetTempPath(), "glyph-mw-c-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllTextAsync(fileA, "%PDF");
            await File.WriteAllTextAsync(fileB, "%PDF");
            await File.WriteAllTextAsync(fileC, "%PDF");
            var store = new JsonSessionStore(storePath);

            await store.UpsertWindowAsync("win-a", [fileA, fileB], activeIndex: 1);
            await store.UpsertWindowAsync(
                "win-b",
                [fileC],
                activeIndex: 0,
                new SessionWindowBounds { X = 40, Y = 60, Width = 900, Height = 700, IsMaximized = false });

            var loaded = await store.TryLoadAsync();
            loaded.Should().NotBeNull();
            loaded!.Windows.Should().HaveCount(2);
            loaded.Windows.Select(w => w.Id).Should().BeEquivalentTo(["win-a", "win-b"]);
            loaded.Windows.Single(w => w.Id == "win-a").ActiveIndex.Should().Be(1);
            loaded.Windows.Single(w => w.Id == "win-a").Paths.Should().HaveCount(2);
            var winB = loaded.Windows.Single(w => w.Id == "win-b");
            winB.Paths.Should().Equal(Path.GetFullPath(fileC));
            winB.X.Should().Be(40);
            winB.Y.Should().Be(60);
            winB.Width.Should().Be(900);
            winB.Height.Should().Be(700);
            winB.IsMaximized.Should().BeFalse();
            loaded.Paths.Should().HaveCount(3);

            await store.UpsertWindowAsync("win-a", [], activeIndex: 0);
            var afterRemove = await store.TryLoadAsync();
            afterRemove.Should().NotBeNull();
            afterRemove!.Windows.Should().HaveCount(1);
            afterRemove.Windows[0].Id.Should().Be("win-b");

            await store.UpsertWindowAsync("win-b", [], activeIndex: 0);
            (await store.TryLoadAsync()).Should().BeNull();
            File.Exists(storePath).Should().BeFalse();
        }
        finally
        {
            DeleteQuietly(storePath, fileA, fileB, fileC);
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
            DeleteQuietly(storePath);
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
            DeleteQuietly(storePath, file);
        }
    }

    [Fact]
    public void SessionState_includes_windows_collection()
    {
        var names = typeof(SessionState).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        names.Should().Equal("ActiveIndex", "Paths", "UpdatedAtUtc", "Windows");
        typeof(SessionWindowState).GetProperties().Select(p => p.Name).OrderBy(n => n)
            .Should().Equal("ActiveIndex", "Height", "Id", "IsMaximized", "Paths", "Width", "X", "Y");
    }

    [Fact]
    public void NormalizeInPlace_migrates_flat_paths_and_drops_empty_windows()
    {
        var state = new SessionState
        {
            Paths = [Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)],
            ActiveIndex = 0,
            Windows =
            [
                new SessionWindowState { Id = "keep", Paths = [Path.GetTempPath()], ActiveIndex = 0 },
                new SessionWindowState { Id = "drop", Paths = [], ActiveIndex = 0 },
            ],
        };

        JsonSessionStore.NormalizeInPlace(state, requireExistingFiles: false);
        state.Windows.Should().HaveCount(1);
        state.Windows[0].Id.Should().Be("keep");
        state.Paths.Should().NotBeEmpty();
    }

    private static void DeleteQuietly(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
