using FluentAssertions;
using Glyph.Infrastructure.Session;

namespace Glyph.Infrastructure.Tests;

public class FileVersionSnapshotStoreTests
{
    [Fact]
    public async Task Capture_lists_and_trims_to_capacity()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-snap-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-snap-orig-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllTextAsync(original, "%PDF-1");
            var store = new FileVersionSnapshotStore(root, capacity: 2);
            await store.CaptureAsync(original);
            await Task.Delay(15);
            await File.WriteAllTextAsync(original, "%PDF-2");
            await store.CaptureAsync(original);
            await Task.Delay(15);
            await File.WriteAllTextAsync(original, "%PDF-3");
            await store.CaptureAsync(original);

            var listed = await store.ListAsync(original);
            listed.Should().HaveCount(2);
            listed[0].ByteLength.Should().BeGreaterThan(0);
            listed[0].DisplayName.Should().Be(Path.GetFileName(original));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (File.Exists(original))
            {
                File.Delete(original);
            }
        }
    }

    [Fact]
    public async Task Delete_removes_snapshot_and_meta()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-snap-del-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-snap-del-orig-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await File.WriteAllBytesAsync(original, [1, 2, 3, 4]);
            var store = new FileVersionSnapshotStore(root, capacity: 5);
            var entry = await store.CaptureAsync(original);
            entry.Should().NotBeNull();
            (await store.ListAsync(original)).Should().ContainSingle();
            await store.DeleteAsync(entry!.Id, original);
            (await store.ListAsync(original)).Should().BeEmpty();
            File.Exists(entry.SnapshotPath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (File.Exists(original))
            {
                File.Delete(original);
            }
        }
    }

    [Fact]
    public async Task Capture_preserves_bytes_for_open_as_copy_and_restore()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-snap-bytes-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-snap-bytes-orig-" + Guid.NewGuid().ToString("N") + ".pdf");
        var restoreTarget = Path.Combine(Path.GetTempPath(), "glyph-snap-restore-" + Guid.NewGuid().ToString("N") + ".pdf");
        var openAsCopy = Path.Combine(Path.GetTempPath(), "glyph-snap-copy-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var payload = "%PDF-glyph-snapshot-payload"u8.ToArray();
            await File.WriteAllBytesAsync(original, payload);
            var store = new FileVersionSnapshotStore(root, capacity: 3);
            var entry = await store.CaptureAsync(original);
            entry.Should().NotBeNull();
            File.ReadAllBytes(entry!.SnapshotPath).Should().Equal(payload);

            // Open-as-copy / restore are File.Copy of the snapshot path (MainWindow).
            File.Copy(entry.SnapshotPath, openAsCopy, overwrite: true);
            File.ReadAllBytes(openAsCopy).Should().Equal(payload);

            await File.WriteAllBytesAsync(original, "%PDF-changed"u8.ToArray());
            File.Copy(entry.SnapshotPath, restoreTarget, overwrite: true);
            File.Copy(restoreTarget, original, overwrite: true);
            File.ReadAllBytes(original).Should().Equal(payload);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            foreach (var path in new[] { original, restoreTarget, openAsCopy })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    [Fact]
    public async Task DeleteAll_removes_folder_for_original()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-snap-all-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-snap-all-orig-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await File.WriteAllBytesAsync(original, [9, 8, 7]);
            var store = new FileVersionSnapshotStore(root, capacity: 5);
            await store.CaptureAsync(original);
            await Task.Delay(15);
            await File.WriteAllBytesAsync(original, [1, 2]);
            await store.CaptureAsync(original);
            (await store.ListAsync(original)).Should().HaveCount(2);

            await store.DeleteAllAsync(original);
            (await store.ListAsync(original)).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (File.Exists(original))
            {
                File.Delete(original);
            }
        }
    }

    [Fact]
    public async Task Constructor_clamps_zero_capacity_to_one()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-snap-cap-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-snap-cap-orig-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await File.WriteAllBytesAsync(original, [1]);
            var store = new FileVersionSnapshotStore(root, capacity: 0);
            await store.CaptureAsync(original);
            await Task.Delay(15);
            await File.WriteAllBytesAsync(original, [2]);
            await store.CaptureAsync(original);
            // capacity 0 clamps to 1 → only newest retained
            (await store.ListAsync(original)).Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (File.Exists(original))
            {
                File.Delete(original);
            }
        }
    }
}
