using Glyph.Core.Workspace;
using Glyph.Infrastructure.Paths;
using Glyph.Infrastructure.RecentFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Glyph.App.Hosting;

internal static class AppServices
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<WorkspaceState>();
        services.AddSingleton<IRecentFilesStore>(_ =>
            new JsonRecentFilesStore(GlyphPaths.RecentFilesFile));
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
