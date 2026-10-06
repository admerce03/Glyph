using Glyph.App.Hosting;
using Glyph.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Glyph.App;

public partial class App : Application
{
    private readonly List<MainWindow> _windows = [];

    public App()
    {
        InitializeComponent();

        Services = AppServices.Build();

        UnhandledException += (_, e) =>
        {
            var logger = Services.GetService<ILoggerFactory>()?.CreateLogger("Glyph.Unhandled");
            logger?.LogError(e.Exception, "Unhandled UI exception");
            System.Diagnostics.Debug.WriteLine("UNHANDLED: " + e.Exception);
        };
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static App CurrentApp => (App)Current;

    /// <summary>Most recently activated Glyph window (used for picker HWND fallbacks).</summary>
    public MainWindow? MainWindowInstance { get; private set; }

    public IReadOnlyList<MainWindow> Windows => _windows;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var settingsStore = Services.GetRequiredService<ISettingsStore>();
        await settingsStore.LoadAsync();

        var window = OpenNewWindow();
        sw.Stop();
        var ms = sw.ElapsedMilliseconds;
        System.Diagnostics.Debug.WriteLine($"Glyph cold start to first window: {ms} ms");
        var logger = Services.GetService<ILoggerFactory>()?.CreateLogger("Glyph.Startup");
        logger?.LogInformation("Cold start to first window: {ElapsedMs} ms", ms);
        // Surface once in the status bar when the window is ready (F57-01).
        window.ReportStartupDuration(ms);
    }

    public MainWindow OpenNewWindow()
    {
        var settingsStore = Services.GetRequiredService<ISettingsStore>();
        var window = Services.GetRequiredService<MainWindow>();
        window.ApplyThemePreference(settingsStore.Current.Theme);
        window.Closed += Window_Closed;
        window.Activated += Window_Activated;
        _windows.Add(window);
        MainWindowInstance = window;
        window.Activate();
        return window;
    }

    public void ApplyThemePreference(ThemePreference preference)
    {
        foreach (var window in _windows)
        {
            window.ApplyThemePreference(preference);
        }
    }

    /// <summary>Re-apply Find Match-case / Any-word / sort prefs across all Glyph windows.</summary>
    public void ApplyFindOptionsToAllWindows(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var window in _windows)
        {
            window.ApplyFindOptionsToOpenDocuments(settings);
        }
    }

    /// <summary>Re-apply annotation author / default highlight+stroke colors / stroke width across all windows.</summary>
    public void ApplyAnnotationDefaultsToAllWindows(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var window in _windows)
        {
            window.ApplyAnnotationDefaultsToOpenDocuments(settings);
        }
    }

    /// <summary>Re-apply F54 toolbar hide/reorder prefs across all Glyph windows.</summary>
    public void ApplyToolbarCustomizationToAllWindows(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var window in _windows)
        {
            window.ApplyToolbarCustomizationToOpenDocuments(settings);
        }
    }

    /// <summary>Re-apply F52 shortcut overrides across all Glyph windows.</summary>
    public void ApplyShellKeyboardShortcutsToAllWindows()
    {
        foreach (var window in _windows)
        {
            window.ApplyShellKeyboardShortcuts();
        }
    }

    /// <summary>Re-apply crash-recovery interval to every Glyph window's autosave timer (F55-21).</summary>
    public void ApplyCrashRecoveryIntervalToAllWindows()
    {
        foreach (var window in _windows)
        {
            window.ApplyCrashRecoveryInterval();
        }
    }

    public void CloseAllWindows()
    {
        foreach (var window in _windows.ToArray())
        {
            window.Close();
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (sender is MainWindow window &&
            args.WindowActivationState != WindowActivationState.Deactivated)
        {
            MainWindowInstance = window;
        }
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (sender is not MainWindow window)
        {
            return;
        }

        window.Closed -= Window_Closed;
        window.Activated -= Window_Activated;
        _windows.Remove(window);
        if (ReferenceEquals(MainWindowInstance, window))
        {
            MainWindowInstance = _windows.LastOrDefault();
        }
    }
}
