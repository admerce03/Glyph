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
                DefaultHighlightColor = "Green",
                DefaultStrokeColor = "Black",
                DefaultStickyNoteColor = "Blue",
                DefaultStrokeWidthPoints = 3.5,
                AnimationAutoplay = true,
                StripMetadataByDefault = true,
            });

            var reloaded = new JsonSettingsStore(path);
            var settings = await reloaded.LoadAsync();

            settings.Theme.Should().Be(ThemePreference.Dark);
            settings.RecentFileCapacity.Should().Be(12);
            settings.SidebarVisible.Should().BeFalse();
            settings.RestorePreviousSession.Should().BeTrue();
            settings.AutoSaveToOriginal.Should().BeTrue();
            settings.CrashRecoveryIntervalSeconds.Should().Be(90);
            settings.DefaultHighlightColor.Should().Be("Green");
            settings.DefaultStrokeColor.Should().Be("Black");
            settings.DefaultStickyNoteColor.Should().Be("Blue");
            settings.DefaultStrokeWidthPoints.Should().Be(3.5);
            settings.AnimationAutoplay.Should().BeTrue();
            settings.StripMetadataByDefault.Should().BeTrue();
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
    public async Task Save_and_load_round_trips_ocr_language()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-ocr-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings { OcrLanguageTag = "de-DE" });
            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.OcrLanguageTag.Should().Be("de-DE");
            settings.LocalOnlyOcr.Should().BeTrue();
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
    public async Task Save_and_load_round_trips_pdf_open_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-pdf-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                DefaultPageLayout = "TwoPageWithCover",
                DefaultZoom = 1.5,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.DefaultPageLayout.Should().Be("TwoPageWithCover");
            settings.DefaultZoom.Should().Be(1.5);
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

    [Fact]
    public async Task Save_clamps_crash_recovery_interval()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-clamp-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings { CrashRecoveryIntervalSeconds = -5 });
            (await new JsonSettingsStore(path).LoadAsync()).CrashRecoveryIntervalSeconds.Should().Be(0);

            await store.SaveAsync(new AppSettings { CrashRecoveryIntervalSeconds = 99999 });
            (await new JsonSettingsStore(path).LoadAsync()).CrashRecoveryIntervalSeconds.Should().Be(3600);
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
    public async Task Save_and_load_round_trips_version_snapshot_prefs()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-snap-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                VersionSnapshotsEnabled = true,
                VersionSnapshotCapacity = 12,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.VersionSnapshotsEnabled.Should().BeTrue();
            settings.VersionSnapshotCapacity.Should().Be(12);
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
    public async Task Save_clamps_version_snapshot_capacity()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-snap-clamp-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings { VersionSnapshotCapacity = 0 });
            (await new JsonSettingsStore(path).LoadAsync()).VersionSnapshotCapacity.Should().Be(1);

            await store.SaveAsync(new AppSettings { VersionSnapshotCapacity = 999 });
            (await new JsonSettingsStore(path).LoadAsync()).VersionSnapshotCapacity.Should().Be(50);
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
