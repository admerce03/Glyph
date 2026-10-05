using Glyph.Core.Workspace;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using Glyph.Infrastructure.Documents;
using Glyph.Infrastructure.Paths;
using Glyph.Infrastructure.RecentFiles;
using Glyph.Infrastructure.Settings;
using Glyph.Ocr.Abstractions;
using Glyph.Ocr.Pdf;
using Glyph.Ocr.Tesseract;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Text;
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
        services.AddSingleton<IDocumentViewStateStore>(_ =>
            new JsonDocumentViewStateStore(GlyphPaths.DocumentViewStateFile));
        services.AddSingleton<IPdfDocumentFactory, PdfiumDocumentFactory>();
        services.AddSingleton<IPdfRenderer, PdfiumRenderer>();
        services.AddSingleton<IPdfTextExtractor, PdfiumTextExtractor>();
        services.AddSingleton<IPdfOutlineService, PdfiumOutlineService>();
        services.AddSingleton<IPdfLinkService, PdfiumLinkService>();
        services.AddSingleton<IPdfPageEditor, PdfiumPageEditor>();
        services.AddSingleton<IPdfAnnotationStore, PdfiumAnnotationStore>();
        services.AddSingleton<IPdfMetadataService, PdfiumMetadataService>();
        services.AddSingleton<IPdfSecurityInfoService, PdfiumSecurityInfoService>();
        services.AddSingleton<IPdfSecurityService, PdfiumSecurityService>();
        services.AddSingleton<IPdfRedactionService, PdfiumRedactionService>();
        services.AddSingleton<IPdfOptimizationService, PdfiumOptimizationService>();
        services.AddSingleton<IPdfTextSearchService, PdfPigTextSearchService>();
        services.AddSingleton<IImageDecoder, MagickImageDecoder>();
        services.AddSingleton<IImageEncoder, MagickImageEncoder>();
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
        services.AddSingleton<IOcrEngine, TesseractCliOcrEngine>();
        services.AddSingleton<PdfPageOcrService>();
        services.AddSingleton<PageRenderCache>(_ => new PageRenderCache(capacity: 48));
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
