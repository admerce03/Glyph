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
    public async Task Remember_trims_to_per_field_and_global_capacity()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-form-hist-cap-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonFormValueHistory(path, perFieldCapacity: 2, globalCapacity: 3);
            await store.RememberAsync("City", "Paris");
            await store.RememberAsync("City", "London");
            await store.RememberAsync("City", "Tokyo");
            await store.RememberAsync("City", "Berlin");

            // Field bucket is capped at 2; GetSuggestions then appends globals, so
            // the first two City hits must be the newest field values.
            var city = store.GetSuggestions("City", limit: 10);
            city.Take(2).Should().Equal("Berlin", "Tokyo");
            city.Should().NotContain("Paris");

            await store.RememberAsync("A", "one");
            await store.RememberAsync("B", "two");
            await store.RememberAsync("C", "three");
            await store.RememberAsync("D", "four");
            // Unknown field → globals only, capped at 3 newest.
            store.GetSuggestions("Other", limit: 10).Should().Equal("four", "three", "two");
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
