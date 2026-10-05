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
                var saved = await library.SaveAsync("My Sig", png);
                saved.Name.Should().Be("My Sig");
                saved.Id.Should().NotBeNullOrWhiteSpace();
            }

            var listed = await library.ListAsync();
            listed.Should().ContainSingle(e => e.Name == "My Sig");

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
}
