using FluentAssertions;
using Glyph.Infrastructure.Settings;

namespace Glyph.Infrastructure.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public async Task Save_and_load_round_trips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                Theme = ThemePreference.Dark,
                RecentFileCapacity = 12,
                SidebarVisible = false,
                RestorePreviousSession = true,
                AutoSaveToOriginal = true,
                CrashRecoveryIntervalSeconds = 90,
            });

            var reloaded = new JsonSettingsStore(path);
            var settings = await reloaded.LoadAsync();

            settings.Theme.Should().Be(ThemePreference.Dark);
            settings.RecentFileCapacity.Should().Be(12);
            settings.SidebarVisible.Should().BeFalse();
            settings.RestorePreviousSession.Should().BeTrue();
            settings.AutoSaveToOriginal.Should().BeTrue();
            settings.CrashRecoveryIntervalSeconds.Should().Be(90);
            reloaded.Current.Theme.Should().Be(ThemePreference.Dark);
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
    public async Task Load_returns_defaults_for_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new JsonSettingsStore(path);
        var settings = await store.LoadAsync();
        settings.Theme.Should().Be(ThemePreference.System);
        settings.SidebarVisible.Should().BeTrue();
    }
}
