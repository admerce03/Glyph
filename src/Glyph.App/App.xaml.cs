using Glyph.App.Hosting;
using Glyph.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Glyph.App;

public partial class App : Application
{
    private MainWindow? _window;

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

    public MainWindow? MainWindowInstance => _window;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settingsStore = Services.GetRequiredService<ISettingsStore>();
        await settingsStore.LoadAsync();

        _window = Services.GetRequiredService<MainWindow>();
        _window.ApplyThemePreference(settingsStore.Current.Theme);
        _window.Activate();
    }

    public void ApplyThemePreference(ThemePreference preference)
    {
        _window?.ApplyThemePreference(preference);
    }
}
