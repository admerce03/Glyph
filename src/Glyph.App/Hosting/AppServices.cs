using Glyph.Core.Workspace;
using Glyph.Infrastructure.Paths;
using Glyph.Infrastructure.RecentFiles;
using Glyph.Infrastructure.Settings;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Rendering;
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
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(GlyphPaths.SettingsFile));
        services.AddSingleton<IRecentFilesStore>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>().Current;
            return new JsonRecentFilesStore(GlyphPaths.RecentFilesFile, settings.RecentFileCapacity);
        });
        services.AddSingleton<IPdfDocumentFactory, PdfiumDocumentFactory>();
        services.AddSingleton<IPdfRenderer, PdfiumRenderer>();
        services.AddSingleton<PageRenderCache>(_ => new PageRenderCache(capacity: 48));
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
