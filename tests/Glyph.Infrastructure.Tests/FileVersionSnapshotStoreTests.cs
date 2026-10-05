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
}
