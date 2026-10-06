using Glyph.App.Ocr;
using Glyph.App.Pdf;
using Glyph.Core.Signatures;
using Glyph.Core.Workspace;
using Glyph.Imaging.Abstractions;
using Glyph.Imaging.Magick;
using Glyph.Infrastructure.Documents;
using Glyph.Infrastructure.Forms;
using Glyph.Infrastructure.Paths;
using Glyph.Infrastructure.RecentFiles;
using Glyph.Infrastructure.Session;
using Glyph.Infrastructure.Settings;
using Glyph.Infrastructure.Signatures;
using Glyph.Ocr.Abstractions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Pdfium;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Security;
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

        // Per-window document workspace so each Glyph window has independent tabs.
        services.AddTransient<WorkspaceState>();
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(GlyphPaths.SettingsFile));
        services.AddSingleton<IRecentFilesStore>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>();
            // Read capacity live so Preferences → recent file count applies without restart (F55-03).
            return new JsonRecentFilesStore(
                GlyphPaths.RecentFilesFile,
                () => settings.Current.RecentFileCapacity);
        });
        services.AddSingleton<IDocumentViewStateStore>(_ =>
            new JsonDocumentViewStateStore(GlyphPaths.DocumentViewStateFile));
        services.AddSingleton<ISessionStore>(_ => new JsonSessionStore(GlyphPaths.SessionFile));
        services.AddSingleton<ICrashRecoveryStore>(_ =>
            new FileCrashRecoveryStore(GlyphPaths.RecoveryDirectory));
        services.AddSingleton<IVersionSnapshotStore>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>();
            // Read capacity live so Preferences → snapshot count applies without restart (F51).
            return new FileVersionSnapshotStore(
                GlyphPaths.SnapshotsDirectory,
                () => settings.Current.VersionSnapshotCapacity);
        });
        services.AddSingleton<ISignatureLibrary>(_ => new FileSignatureLibrary(GlyphPaths.SignaturesDirectory));
        services.AddSingleton<IFormValueHistory>(_ => new JsonFormValueHistory(GlyphPaths.FormValueHistoryFile));
        services.AddSingleton<IFormAutofillProfileStore>(_ =>
            new JsonFormAutofillProfileStore(GlyphPaths.FormAutofillProfileFile));
        services.AddSingleton<IPdfDocumentFactory, PdfiumDocumentFactory>();
        services.AddSingleton<IPdfRenderer, PdfiumRenderer>();
        services.AddSingleton<IPdfTextExtractor, PdfiumTextExtractor>();
        services.AddSingleton<IPdfOutlineService, PdfiumOutlineService>();
        services.AddSingleton<IPdfOutlineExportService, PdfiumOutlineExportService>();
        services.AddSingleton<IPdfLinkService, PdfiumLinkService>();
        services.AddSingleton<IPdfPageEditor, PdfiumPageEditor>();
        services.AddSingleton<IPdfAnnotationService, PdfiumAnnotationService>();
        services.AddSingleton<IPdfRedactionService, PdfiumRedactionService>();
        services.AddSingleton<IPdfDocumentInfoService, PdfiumDocumentInfoService>();
        services.AddSingleton<IPdfSecurityService, PdfSharpSecurityService>();
        services.AddSingleton<IPdfImageJpegEncoder, MagickPdfImageJpegEncoder>();
        services.AddSingleton<IPdfExportService, PdfPageImageExportService>();
        services.AddSingleton<IPdfOptimizeService>(sp =>
            new PdfiumOptimizeService(sp.GetRequiredService<IPdfImageJpegEncoder>()));
        services.AddSingleton<IPdfFormStore, PdfiumFormStore>();
        services.AddSingleton<IPdfTextSearchService, PdfPigTextSearchService>();
        services.AddSingleton<IImageDecoder, MagickImageDecoder>();
        services.AddSingleton<IImageEncoder, MagickImageEncoder>();
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
        services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
        services.AddSingleton<PageRenderCache>(_ => new PageRenderCache(PageRenderCache.DefaultCapacity));
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
