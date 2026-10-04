namespace Glyph.Infrastructure.Settings;

public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public int RecentFileCapacity { get; set; } = 20;

    public bool RestorePreviousSession { get; set; }

    public bool SidebarVisible { get; set; } = true;
}
