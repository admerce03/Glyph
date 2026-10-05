using FluentAssertions;
using Glyph.Infrastructure.Forms;

namespace Glyph.Infrastructure.Tests;

public class JsonFormValueHistoryTests
{
    [Fact]
    public async Task Remember_prefers_field_specific_then_global()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-form-hist-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonFormValueHistory(path);
            await store.RememberAsync("Name", "Ada Lovelace");
            await store.RememberAsync("Email", "ada@example.com");
            await store.RememberAsync("Name", "Grace Hopper");

            var forName = store.GetSuggestions("Name", limit: 5);
            forName.Should().StartWith("Grace Hopper");
            forName.Should().Contain("Ada Lovelace");
            forName.Should().Contain("ada@example.com");

            var reloaded = new JsonFormValueHistory(path);
            reloaded.GetSuggestions("name").Should().StartWith("Grace Hopper");
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
    public async Task Empty_and_whitespace_values_are_ignored()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-form-hist-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonFormValueHistory(path);
            await store.RememberAsync("City", "   ");
            await store.RememberAsync("City", "");
            store.GetSuggestions("City").Should().BeEmpty();
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
