using FluentAssertions;
using Glyph.Core.Documents;
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
                RememberLastPage = false,
                RememberZoom = false,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.DefaultPageLayout.Should().Be("TwoPageWithCover");
            settings.DefaultZoom.Should().Be(1.5);
            settings.RememberLastPage.Should().BeFalse();
            settings.RememberZoom.Should().BeFalse();
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
    public async Task Save_and_load_round_trips_find_options()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-find-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                FindCaseSensitive = true,
                FindAnyWord = true,
                FindSortByRelevance = true,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.FindCaseSensitive.Should().BeTrue();
            settings.FindAnyWord.Should().BeTrue();
            settings.FindSortByRelevance.Should().BeTrue();
            FindOptionsPolicy.SortComboIndex(settings.FindSortByRelevance)
                .Should().Be(FindOptionsPolicy.SortRelevanceIndex);
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
    public async Task Load_missing_remember_view_prefs_means_null_default_on()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-remember-missing-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, """{"theme":"System","defaultZoom":1.25}""");
            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.RememberLastPage.Should().BeNull();
            settings.RememberZoom.Should().BeNull();
            DocumentViewRestorePolicy.EffectiveRememberLastPage(settings.RememberLastPage).Should().BeTrue();
            DocumentViewRestorePolicy.EffectiveRememberZoom(settings.RememberZoom).Should().BeTrue();
            settings.FindCaseSensitive.Should().BeFalse();
            settings.FindAnyWord.Should().BeFalse();
            settings.FindSortByRelevance.Should().BeFalse();
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
    public async Task Save_and_load_round_trips_remember_view_prefs_on()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-remember-on-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                RememberLastPage = true,
                RememberZoom = true,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.RememberLastPage.Should().BeTrue();
            settings.RememberZoom.Should().BeTrue();
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
        settings.AutoSaveToOriginal.Should().BeFalse();
        settings.VersionSnapshotsEnabled.Should().BeFalse();
        settings.ToolbarHiddenCommands.Should().BeEmpty();
        settings.RememberLastPage.Should().BeNull();
        settings.RememberZoom.Should().BeNull();
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

    [Fact]
    public async Task Save_and_load_round_trips_toolbar_hidden_commands()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-toolbar-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                CompactToolbar = true,
                ToolbarHiddenCommands =
                [
                    ToolbarCommands.Share,
                    ToolbarCommands.Ocr,
                    "UNKNOWN-COMMAND",
                    "  Print  ",
                ],
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.CompactToolbar.Should().BeTrue();
            settings.ToolbarHiddenCommands.Should().BeEquivalentTo(
                [ToolbarCommands.Share, ToolbarCommands.Ocr, ToolbarCommands.Print]);
            settings.ToolbarCommandOrder.Should().BeEmpty();
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
    public async Task Save_and_load_round_trips_toolbar_command_order()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-toolbar-order-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                ToolbarCommandOrder =
                [
                    ToolbarCommands.Ocr,
                    ToolbarCommands.Share,
                    "UNKNOWN",
                    ToolbarCommands.Print,
                ],
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.ToolbarCommandOrder[0].Should().Be(ToolbarCommands.Ocr);
            settings.ToolbarCommandOrder[1].Should().Be(ToolbarCommands.Share);
            settings.ToolbarCommandOrder[2].Should().Be(ToolbarCommands.Print);
            settings.ToolbarCommandOrder.Should().Contain(ToolbarCommands.Sidebar);
            settings.ToolbarCommandOrder.Should().HaveCount(ToolbarCommands.Catalog.Count);
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
    public async Task Save_empty_toolbar_hidden_means_default_all_visible()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-toolbar-reset-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                ToolbarHiddenCommands = [ToolbarCommands.Zoom],
            });
            (await new JsonSettingsStore(path).LoadAsync()).ToolbarHiddenCommands.Should().ContainSingle()
                .Which.Should().Be(ToolbarCommands.Zoom);

            // Reset toolbar to default = clear hidden list (F54-19).
            await store.SaveAsync(new AppSettings { ToolbarHiddenCommands = [] });
            (await new JsonSettingsStore(path).LoadAsync()).ToolbarHiddenCommands.Should().BeEmpty();
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
    public async Task Save_and_load_round_trips_sidebar_and_thumbnail_widths()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-widths-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                SidebarVisible = false,
                ToolbarVisible = false,
                SidebarWidth = 240,
                ThumbnailWidth = 120,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.SidebarVisible.Should().BeFalse();
            settings.ToolbarVisible.Should().BeFalse();
            settings.SidebarWidth.Should().Be(240);
            settings.ThumbnailWidth.Should().Be(120);
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
    public async Task Save_clamps_default_stroke_width()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-stroke-clamp-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings { DefaultStrokeWidthPoints = 0.1 });
            (await new JsonSettingsStore(path).LoadAsync()).DefaultStrokeWidthPoints.Should().Be(0.5);

            await store.SaveAsync(new AppSettings { DefaultStrokeWidthPoints = 99 });
            (await new JsonSettingsStore(path).LoadAsync()).DefaultStrokeWidthPoints.Should().Be(12);
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
    public async Task Save_and_load_round_trips_open_in_separate_windows_and_author()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-shell-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                OpenFilesInSeparateWindows = true,
                AnnotationAuthor = "  Ada Lovelace  ",
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.OpenFilesInSeparateWindows.Should().BeTrue();
            settings.AnnotationAuthor.Should().Be("Ada Lovelace");
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
    public async Task Save_normalizes_zoom100_and_interpolation_prefs()
    {
        var path = Path.Combine(Path.GetTempPath(), "glyph-settings-zoom-interp-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                Zoom100Meaning = "print",
                DefaultInterpolation = "Nearest-neighbor",
                ColorManagedDisplayDefault = true,
            });

            var settings = await new JsonSettingsStore(path).LoadAsync();
            settings.Zoom100Meaning.Should().Be("Print");
            settings.DefaultInterpolation.Should().Be("NearestNeighbor");
            settings.ColorManagedDisplayDefault.Should().BeTrue();

            await store.SaveAsync(new AppSettings
            {
                Zoom100Meaning = "nonsense",
                DefaultInterpolation = "weird",
            });
            var fallback = await new JsonSettingsStore(path).LoadAsync();
            fallback.Zoom100Meaning.Should().Be("Pixels");
            fallback.DefaultInterpolation.Should().Be("Auto");
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
