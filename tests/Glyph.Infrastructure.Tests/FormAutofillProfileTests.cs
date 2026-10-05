using FluentAssertions;
using Glyph.Infrastructure.Forms;

namespace Glyph.Infrastructure.Tests;

public class FormAutofillMatcherTests
{
    [Theory]
    [InlineData("Email", FormAutofillMatcher.ProfileField.Email)]
    [InlineData("user_email", FormAutofillMatcher.ProfileField.Email)]
    [InlineData("Phone Number", FormAutofillMatcher.ProfileField.Phone)]
    [InlineData("mobile", FormAutofillMatcher.ProfileField.Phone)]
    [InlineData("Street Address", FormAutofillMatcher.ProfileField.Address)]
    [InlineData("ZIP", FormAutofillMatcher.ProfileField.Address)]
    [InlineData("Full Name", FormAutofillMatcher.ProfileField.Name)]
    [InlineData("name", FormAutofillMatcher.ProfileField.Name)]
    [InlineData("Quantity", FormAutofillMatcher.ProfileField.None)]
    public void Match_keywords(string field, FormAutofillMatcher.ProfileField expected)
    {
        FormAutofillMatcher.Match(field).Should().Be(expected);
    }

    [Fact]
    public void ResolveValue_returns_profile_fields()
    {
        var profile = new FormAutofillProfile(
            Name: "Ada",
            Address: "1 Analytical Engine Rd",
            Email: "ada@example.com",
            Phone: "555-0100");

        FormAutofillMatcher.ResolveValue(profile, "Email").Should().Be("ada@example.com");
        FormAutofillMatcher.ResolveValue(profile, "Full Name").Should().Be("Ada");
        FormAutofillMatcher.ResolveValue(profile, "Quantity").Should().BeNull();
    }
}

public class JsonFormAutofillProfileStoreTests
{
    [Fact]
    public async Task Save_and_reload_profile()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-profile-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonFormAutofillProfileStore(path);
            await store.SaveAsync(new FormAutofillProfile("Ada", "London", "ada@example.com", "555"));
            store.Current.Name.Should().Be("Ada");

            var reloaded = new JsonFormAutofillProfileStore(path);
            reloaded.Current.Email.Should().Be("ada@example.com");
            reloaded.Current.HasAnyValue.Should().BeTrue();
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
