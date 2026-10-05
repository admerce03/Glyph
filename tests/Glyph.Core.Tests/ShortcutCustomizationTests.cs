using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class KeyboardGestureSyntaxTests
{
    [Theory]
    [InlineData("Ctrl+S", true, false, false, "S")]
    [InlineData("ctrl+shift+s", true, true, false, "S")]
    [InlineData("F11", false, false, false, "F11")]
    [InlineData("Ctrl++", true, false, false, "+")]
    [InlineData("Ctrl+-", true, false, false, "-")]
    [InlineData("Ctrl+OemComma", true, false, false, "OemComma")]
    [InlineData("Shift+F3", false, true, false, "F3")]
    [InlineData("PageDown", false, false, false, "PageDown")]
    public void TryParse_normalizes_tokens(string input, bool ctrl, bool shift, bool alt, string key)
    {
        KeyboardGestureSyntax.TryParse(input, out var parsed).Should().BeTrue();
        parsed.Ctrl.Should().Be(ctrl);
        parsed.Shift.Should().Be(shift);
        parsed.Alt.Should().Be(alt);
        parsed.Key.Should().Be(key);
    }

    [Fact]
    public void Matches_respects_modifiers_and_key()
    {
        KeyboardGestureSyntax.Matches("Ctrl+Shift+A", ctrl: true, shift: true, alt: false, "A")
            .Should().BeTrue();
        KeyboardGestureSyntax.Matches("Ctrl+Shift+A", ctrl: true, shift: false, alt: false, "A")
            .Should().BeFalse();
    }

    [Fact]
    public void Normalize_orders_modifiers()
    {
        KeyboardGestureSyntax.Normalize("shift+ctrl+o").Should().Be("Ctrl+Shift+O");
    }
}

public class ShortcutCustomizationPolicyTests
{
    [Fact]
    public void Resolve_uses_override_when_present()
    {
        var map = new Dictionary<string, string> { ["Open"] = "Ctrl+Shift+O" };
        ShortcutCustomizationPolicy.Resolve("Open", map).Should().Be("Ctrl+Shift+O");
        ShortcutCustomizationPolicy.Resolve("Save", map).Should().Be("Ctrl+S");
    }

    [Fact]
    public void NormalizeOverrides_drops_defaults_and_unknowns()
    {
        var raw = new Dictionary<string, string>
        {
            ["Open"] = "Ctrl+O",
            ["Save"] = "Ctrl+Shift+S",
            ["NotACommand"] = "Ctrl+Z",
            ["Find"] = "bad",
        };
        var normalized = ShortcutCustomizationPolicy.NormalizeOverrides(raw);
        normalized.Should().ContainKey("Save");
        normalized.Should().NotContainKey("Open");
        normalized.Should().NotContainKey("NotACommand");
        normalized.Should().NotContainKey("Find");
    }

    [Fact]
    public void ValidationError_detects_duplicate_gestures()
    {
        var map = new Dictionary<string, string>
        {
            ["Open"] = "Ctrl+S",
        };
        ShortcutCustomizationPolicy.ValidationError(map).Should().Contain("Ctrl+S");
    }

    [Fact]
    public void DefaultCatalog_includes_shell_and_document_commands()
    {
        ShortcutCustomizationPolicy.DefaultCatalog.Should().Contain(c => c.Command == "Open");
        ShortcutCustomizationPolicy.DefaultCatalog.Should().Contain(c => c.Command == "Find");
        ShortcutCustomizationPolicy.DefaultCatalog.Should().Contain(c => c.Command == "Toggle Toolbar");
        ShortcutCustomizationPolicy.DefaultCatalog.Should().Contain(c => c.Command == "Toggle Sidebar");
        ShortcutCustomizationPolicy.DefaultCatalog.Select(c => c.Command)
            .Should().OnlyHaveUniqueItems();
    }
}
