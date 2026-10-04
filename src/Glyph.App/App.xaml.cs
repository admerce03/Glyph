using Glyph.App.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Glyph.App;

public partial class App : Application
{
    private Window? _window;

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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
