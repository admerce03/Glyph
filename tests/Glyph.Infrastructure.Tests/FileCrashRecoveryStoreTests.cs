using FluentAssertions;
using Glyph.Infrastructure.Recovery;

namespace Glyph.Infrastructure.Tests;

public class FileCrashRecoveryStoreTests
{
    [Fact]
    public async Task Save_list_and_delete_snapshot_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCrashRecoveryStore(root);
            var docPath = Path.Combine(root, "doc.pdf");
            await using (var input = new MemoryStream("glyph-recovery-bytes"u8.ToArray()))
            {
                var entry = await store.SaveSnapshotAsync(docPath, input);
                entry.DocumentPath.Should().Be(docPath);
                File.Exists(entry.SnapshotPath).Should().BeTrue();
                entry.ByteLength.Should().BeGreaterThan(0);
            }

            var listed = await store.ListAsync();
            listed.Should().ContainSingle(e => e.DocumentPath == docPath);

            // Second save replaces prior snapshot for same document path.
            await using (var input = new MemoryStream("second"u8.ToArray()))
            {
                await store.SaveSnapshotAsync(docPath, input);
            }

            listed = await store.ListAsync();
            listed.Should().ContainSingle();

            await store.DeleteAsync(listed[0].SnapshotPath);
            (await store.ListAsync()).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClearAll_removes_snapshots_and_index()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCrashRecoveryStore(root);
            await using var input = new MemoryStream([1, 2, 3, 4]);
            await store.SaveSnapshotAsync("a.pdf", input);
            await store.ClearAllAsync();
            (await store.ListAsync()).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
