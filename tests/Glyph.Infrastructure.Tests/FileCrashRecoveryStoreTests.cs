using FluentAssertions;
using Glyph.Infrastructure.Session;

namespace Glyph.Infrastructure.Tests;

public class FileCrashRecoveryStoreTests
{
    [Fact]
    public async Task Save_list_and_discard_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-recovery-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-orig-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllTextAsync(original, "%PDF-original");
            var store = new FileCrashRecoveryStore(root);
            await using (var stream = new MemoryStream("%PDF-recovery"u8.ToArray()))
            {
                await store.SaveSnapshotAsync(original, stream, ".pdf");
            }

            var listed = await store.ListAsync();
            listed.Should().ContainSingle();
            listed[0].OriginalPath.Should().Be(Path.GetFullPath(original));
            listed[0].DisplayName.Should().Be(Path.GetFileName(original));
            File.Exists(listed[0].RecoveryPath).Should().BeTrue();
            (await File.ReadAllTextAsync(listed[0].RecoveryPath)).Should().Be("%PDF-recovery");

            await store.DiscardAsync(original);
            (await store.ListAsync()).Should().BeEmpty();
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
    public async Task DiscardAll_removes_recovery_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-recovery-all-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-orig-all-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await File.WriteAllTextAsync(original, "png");
            var store = new FileCrashRecoveryStore(root);
            await using (var stream = new MemoryStream([1, 2, 3]))
            {
                await store.SaveSnapshotAsync(original, stream, ".png");
            }

            (await store.ListAsync()).Should().ContainSingle();
            await store.DiscardAllAsync();
            Directory.Exists(root).Should().BeFalse();
            (await store.ListAsync()).Should().BeEmpty();
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
    public async Task Save_twice_overwrites_same_original()
    {
        var root = Path.Combine(Path.GetTempPath(), "glyph-recovery-ow-" + Guid.NewGuid().ToString("N"));
        var original = Path.Combine(Path.GetTempPath(), "glyph-orig-ow-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllTextAsync(original, "%PDF-original");
            var store = new FileCrashRecoveryStore(root);
            await using (var first = new MemoryStream("%PDF-v1"u8.ToArray()))
            {
                await store.SaveSnapshotAsync(original, first, ".pdf");
            }

            await using (var second = new MemoryStream("%PDF-v2"u8.ToArray()))
            {
                await store.SaveSnapshotAsync(original, second, ".pdf");
            }

            var listed = await store.ListAsync();
            listed.Should().ContainSingle();
            (await File.ReadAllTextAsync(listed[0].RecoveryPath)).Should().Be("%PDF-v2");
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

