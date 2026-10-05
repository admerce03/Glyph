using FluentAssertions;
using Glyph.Infrastructure.Signatures;

namespace Glyph.Infrastructure.Tests;

public class FileSignatureLibraryTests
{
    [Fact]
    public async Task Save_list_open_and_delete_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "glyph-sigs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileSignatureLibrary(dir);
            await using (var png = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                var saved = await library.SaveAsync("My Sig", png, description: "Signature of Test User");
                saved.Name.Should().Be("My Sig");
                saved.Description.Should().Be("Signature of Test User");
                saved.Id.Should().NotBeNullOrWhiteSpace();
            }

            var listed = await library.ListAsync();
            listed.Should().ContainSingle(e => e.Name == "My Sig" && e.Description == "Signature of Test User");

            await library.UpdateDescriptionAsync(listed[0].Id, "Updated alt text");
            (await library.ListAsync())[0].Description.Should().Be("Updated alt text");

            await using (var opened = await library.OpenImageAsync(listed[0].Id))
            {
                opened.Length.Should().BeGreaterThan(0);
            }

            await library.DeleteAsync(listed[0].Id);
            (await library.ListAsync()).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClearAll_removes_entries_and_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "glyph-sigs-clear-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileSignatureLibrary(dir);
            string id;
            await using (var png = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            {
                id = (await library.SaveAsync("ToClear", png)).Id;
            }

            var pngPath = Path.Combine(dir, id + ".png");
            File.Exists(pngPath).Should().BeTrue();

            await library.ClearAllAsync();
            (await library.ListAsync()).Should().BeEmpty();
            File.Exists(pngPath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Reorder_persists_new_order()
    {
        var dir = Path.Combine(Path.GetTempPath(), "glyph-sigs-reorder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileSignatureLibrary(dir);
            string idA, idB, idC;
            await using (var png = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            {
                idA = (await library.SaveAsync("A", png)).Id;
            }

            await using (var png = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            {
                idB = (await library.SaveAsync("B", png)).Id;
            }

            await using (var png = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            {
                idC = (await library.SaveAsync("C", png)).Id;
            }

            await library.ReorderAsync([idC, idA, idB]);
            var listed = await library.ListAsync();
            listed.Select(e => e.Id).Should().Equal(idC, idA, idB);
            listed.Select(e => e.Name).Should().Equal("C", "A", "B");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
