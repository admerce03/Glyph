using System.Runtime.InteropServices;
using Glyph.App.Capture;
using Glyph.App.Scanning;
using Glyph.App.Sharing;
using Glyph.App.Views;
using Glyph.Core.Documents;
using Glyph.Core.IO;
using Glyph.Core.Pdf;
using Glyph.Core.Signatures;
using Glyph.Core.Workspace;
using Glyph.Imaging.Abstractions;
using Glyph.Infrastructure.Documents;
using Glyph.Infrastructure.Forms;
using Glyph.Infrastructure.RecentFiles;
using Glyph.Infrastructure.Session;
using Glyph.Infrastructure.Settings;
using Glyph.Ocr.Abstractions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Text;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;

namespace Glyph.App;

public sealed partial class MainWindow : Window
{
    private static bool _startupSessionHandled;

    private DocumentShareHelper? _shareHelper;
    private DispatcherTimer? _recoveryTimer;
    private bool _recoveryTickRunning;
    private bool _sidebarResizing;
    private double _sidebarResizeStartX;
    private double _sidebarResizeStartWidth;

    private readonly WorkspaceState _workspace;
    private readonly IRecentFilesStore _recentFiles;
    private readonly IDocumentViewStateStore _viewStateStore;
    private readonly ISessionStore _sessionStore;
    private readonly ICrashRecoveryStore _recoveryStore;
    private readonly IVersionSnapshotStore _snapshotStore;
    private readonly ISettingsStore _settingsStore;
    private readonly IPdfDocumentFactory _pdfFactory;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IPdfTextSearchService _pdfSearch;
    private readonly IPdfTextExtractor _pdfText;
    private readonly IPdfOutlineService _pdfOutlines;
    private readonly IPdfOutlineExportService _pdfOutlineExport;
    private readonly IPdfLinkService _pdfLinks;
    private readonly IPdfPageEditor _pdfPageEditor;
    private readonly IPdfAnnotationService _pdfAnnotations;
    private readonly IPdfRedactionService _pdfRedaction;
    private readonly IPdfDocumentInfoService _pdfInfo;
    private readonly IPdfOptimizeService _pdfOptimize;
    private readonly IPdfSecurityService _pdfSecurity;
    private readonly IPdfExportService _pdfExport;
    private readonly ISignatureLibrary _signatures;
    private readonly IFormValueHistory _formValueHistory;
    private readonly IFormAutofillProfileStore _formProfile;
    private readonly IPdfFormStore _pdfForms;
    private readonly IImageDecoder _imageDecoder;
    private readonly IImageEncoder _imageEncoder;
    private readonly IImageProcessor _imageProcessor;
    private readonly IOcrEngine _ocr;
    private readonly PageRenderCache _pageCache;
    private readonly ILogger<MainWindow> _logger;
    private readonly Dictionary<DocumentId, IAsyncDisposable> _openEngines = new();

    public MainWindow(
        WorkspaceState workspace,
        IRecentFilesStore recentFiles,
        IDocumentViewStateStore viewStateStore,
        ISessionStore sessionStore,
        ICrashRecoveryStore recoveryStore,
        IVersionSnapshotStore snapshotStore,
        ISettingsStore settingsStore,
        IPdfDocumentFactory pdfFactory,
        IPdfRenderer pdfRenderer,
        IPdfTextSearchService pdfSearch,
        IPdfTextExtractor pdfText,
        IPdfOutlineService pdfOutlines,
        IPdfOutlineExportService pdfOutlineExport,
        IPdfLinkService pdfLinks,
        IPdfPageEditor pdfPageEditor,
        IPdfAnnotationService pdfAnnotations,
        IPdfRedactionService pdfRedaction,
        IPdfDocumentInfoService pdfInfo,
        IPdfOptimizeService pdfOptimize,
        IPdfSecurityService pdfSecurity,
        IPdfExportService pdfExport,
        ISignatureLibrary signatures,
        IFormValueHistory formValueHistory,
        IFormAutofillProfileStore formProfile,
        IPdfFormStore pdfForms,
        IImageDecoder imageDecoder,
        IImageEncoder imageEncoder,
        IImageProcessor imageProcessor,
        IOcrEngine ocr,
        PageRenderCache pageCache,
        ILogger<MainWindow> logger)
    {
        _workspace = workspace;
        _recentFiles = recentFiles;
        _viewStateStore = viewStateStore;
        _sessionStore = sessionStore;
        _recoveryStore = recoveryStore;
        _snapshotStore = snapshotStore;
        _settingsStore = settingsStore;
        _pdfFactory = pdfFactory;
        _pdfRenderer = pdfRenderer;
        _pdfSearch = pdfSearch;
        _pdfText = pdfText;
        _pdfOutlines = pdfOutlines;
        _pdfOutlineExport = pdfOutlineExport;
        _pdfLinks = pdfLinks;
        _pdfPageEditor = pdfPageEditor;
        _pdfAnnotations = pdfAnnotations;
        _pdfRedaction = pdfRedaction;
        _pdfInfo = pdfInfo;
        _pdfOptimize = pdfOptimize;
        _pdfSecurity = pdfSecurity;
        _pdfExport = pdfExport;
        _signatures = signatures;
        _formValueHistory = formValueHistory;
        _formProfile = formProfile;
        _pdfForms = pdfForms;
        _imageDecoder = imageDecoder;
        _imageEncoder = imageEncoder;
        _imageProcessor = imageProcessor;
        _ocr = ocr;
        _pageCache = pageCache;
        _logger = logger;

        InitializeComponent();
        ApplyShellKeyboardShortcuts();
        ResizeAndCenter(1180, 760);
        RootGrid.Loaded += RootGrid_Loaded;
        Closed += MainWindow_Closed;
    }

    public void ApplyThemePreference(ThemePreference preference)
    {
        RootGrid.RequestedTheme = preference switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ThemeSystemItem.IsChecked = preference == ThemePreference.System;
        ThemeLightItem.IsChecked = preference == ThemePreference.Light;
        ThemeDarkItem.IsChecked = preference == ThemePreference.Dark;
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyThemePreference(_settingsStore.Current.Theme);
        ApplySidebarVisibility(_settingsStore.Current.SidebarVisible);
        ToggleToolbarMenuItem.Text = ToolbarVisibilityLabel.For(_settingsStore.Current.ToolbarVisible);
        ApplyShellKeyboardShortcuts();
        RefreshRecentList();
        UpdateEmptyState();
        StatusText.Text = AppShellStatus.FormatReady(OpenEntryPoints.Catalog[0]);
        ConfigureRecoveryTimer();

        if (!_startupSessionHandled)
        {
            _startupSessionHandled = true;
            await PromptForCrashRecoveryAsync();
            await RestorePreviousSessionAsync();
        }
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Closed -= MainWindow_Closed;
        if (_recoveryTimer is not null)
        {
            _recoveryTimer.Stop();
            _recoveryTimer.Tick -= RecoveryTimer_Tick;
            _recoveryTimer = null;
        }

        await PersistSessionAsync();
    }

    private async void OpenMenuItem_Click(object sender, RoutedEventArgs e) => await OpenWithPickerAsync(allowMultiple: false);

    private async void OpenMultipleMenuItem_Click(object sender, RoutedEventArgs e) => await OpenWithPickerAsync(allowMultiple: true);

    private async void NewFromClipboardMenuItem_Click(object sender, RoutedEventArgs e) => await NewFromClipboardAsync();

    private async void CaptureCameraMenuItem_Click(object sender, RoutedEventArgs e) => await CaptureFromCameraAsync();

    private async void ScanMenuItem_Click(object sender, RoutedEventArgs e) => await ScanDocumentAsync();

    private async void SaveMenuItem_Click(object sender, RoutedEventArgs e) => await SaveActiveDocumentAsync(saveAs: false);

    private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e) => await SaveActiveDocumentAsync(saveAs: true);

    private async void DuplicateMenuItem_Click(object sender, RoutedEventArgs e) => await DuplicateActiveDocumentAsync();

    private async void RenameMenuItem_Click(object sender, RoutedEventArgs e) => await RenameActiveDocumentAsync();

    private async void MoveMenuItem_Click(object sender, RoutedEventArgs e) => await MoveActiveDocumentAsync();

    private void ShareMenuItem_Click(object sender, RoutedEventArgs e) => ShareActiveDocument();

    private async void ShowInExplorerMenuItem_Click(object sender, RoutedEventArgs e) => await ShowActiveInExplorerAsync();

    private async void PropertiesMenuItem_Click(object sender, RoutedEventArgs e) => await ShowActivePropertiesAsync();

    private async void VersionSnapshotsMenuItem_Click(object sender, RoutedEventArgs e) => await ShowVersionSnapshotsAsync();

    private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e) => CopyActivePath();

    private async void CopyFileMenuItem_Click(object sender, RoutedEventArgs e) => await CopyActiveFileAsync();

    private async void OpenWithMenuItem_Click(object sender, RoutedEventArgs e) => await OpenActiveWithDefaultAsync();

    private async void EmailMenuItem_Click(object sender, RoutedEventArgs e) => await EmailActiveAsync();

    private void NewWindowMenuItem_Click(object sender, RoutedEventArgs e) => App.CurrentApp.OpenNewWindow();

    private void MoveToNextMonitorMenuItem_Click(object sender, RoutedEventArgs e) => MoveWindowToNextMonitor();

    private void MoveWindowToNextMonitor()
    {
        try
        {
            var areas = EnumerateDisplayAreas();
            if (areas.Count < 2)
            {
                StatusText.Text = MonitorCyclePolicy.SingleMonitorStatus;
                return;
            }

            var current = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            var index = 0;
            for (var i = 0; i < areas.Count; i++)
            {
                if (areas[i].DisplayId.Value == current.DisplayId.Value)
                {
                    index = i;
                    break;
                }
            }

            var nextIndex = MonitorCyclePolicy.NextIndex(index, areas.Count);
            if (nextIndex < 0)
            {
                StatusText.Text = MonitorCyclePolicy.SingleMonitorStatus;
                return;
            }

            var next = areas[nextIndex];
            var work = next.WorkArea;
            var size = AppWindow.Size;
            var (x, y, width, height) = MonitorCyclePolicy.FitInWorkArea(
                work.X, work.Y, work.Width, work.Height, size.Width, size.Height);
            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
            StatusText.Text = MonitorCyclePolicy.MovedStatus(nextIndex, areas.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = AppShellStatus.FormatFailed(AppShellStatus.MoveToMonitorFailedPrefix, ex.Message);
        }
    }

    private static List<DisplayArea> EnumerateDisplayAreas()
    {
        var areas = new List<DisplayArea>();
        var seen = new HashSet<ulong>();
        MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
        {
            var centerX = rect.Left + Math.Max(1, (rect.Right - rect.Left) / 2);
            var centerY = rect.Top + Math.Max(1, (rect.Bottom - rect.Top) / 2);
            var area = DisplayArea.GetFromPoint(new PointInt32(centerX, centerY), DisplayAreaFallback.Nearest);
            if (area is not null && seen.Add(area.DisplayId.Value))
            {
                areas.Add(area);
            }

            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        if (areas.Count == 0 && DisplayArea.Primary is { } primary)
        {
            areas.Add(primary);
        }

        return areas;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private async void FindAllPdfsMenuItem_Click(object sender, RoutedEventArgs e) => await FindInAllOpenPdfsAsync();

    private async Task FindInAllOpenPdfsAsync()
    {
        var pdfs = _workspace.Documents
            .Where(d => d.Kind == DocumentKind.Pdf && !string.IsNullOrWhiteSpace(d.Path) && File.Exists(d.Path!))
            .ToList();
        if (pdfs.Count == 0)
        {
            StatusText.Text = FindAllOpenPdfsStatus.NoDocuments;
            return;
        }

        var box = new TextBox { PlaceholderText = FindAllOpenPdfsStatus.QueryPlaceholder, Width = 360 };
        var dialog = new ContentDialog
        {
            Title = FindAllOpenPdfsStatus.DialogTitle,
            Content = box,
            PrimaryButtonText = FindAllOpenPdfsStatus.SearchButton,
            CloseButtonText = PreferencesDialogUi.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var query = (box.Text ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            StatusText.Text = FindAllOpenPdfsStatus.EmptyQuery;
            return;
        }

        StatusText.Text = FindAllOpenPdfsStatus.Searching(pdfs.Count);
        var hits = new List<(DocumentSession Doc, PdfSearchHit Hit)>();
        foreach (var doc in pdfs)
        {
            try
            {
                var result = await _pdfSearch.SearchAsync(doc.Path!, query);
                if (result.Status == PdfSearchStatus.Cancelled)
                {
                    StatusText.Text = FindAllOpenPdfsStatus.Cancelled;
                    return;
                }

                foreach (var hit in result.Hits)
                {
                    hits.Add((doc, hit));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search failed for {Path}", doc.Path);
            }
        }

        if (hits.Count == 0)
        {
            StatusText.Text = FindAllOpenPdfsStatus.NoMatches;
            return;
        }

        // F06-12: page order within each document; documents keep workspace tab order.
        var docOrder = _workspace.Documents
            .Select((d, i) => (d.Id, i))
            .ToDictionary(t => t.Id, t => t.i);
        hits = PdfSearchHitOrder.ByPageThenOccurrence(
            hits,
            h => docOrder.GetValueOrDefault(h.Doc.Id, int.MaxValue),
            h => h.Hit.PageIndex).ToList();

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 480,
            MaxHeight = 360,
            ItemsSource = hits
                .Select(h => $"{h.Doc.DisplayName} · p.{h.Hit.PageIndex + 1}: {h.Hit.Snippet}")
                .ToList(),
        };
        var results = new ContentDialog
        {
            Title = FindAllOpenPdfsStatus.MatchCount(hits.Count),
            Content = list,
            PrimaryButtonText = DialogButtons.GoTo,
            CloseButtonText = DialogButtons.Close,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await results.ShowAsync() != ContentDialogResult.Primary
            || list.SelectedIndex < 0
            || list.SelectedIndex >= hits.Count)
        {
            StatusText.Text = FindAllOpenPdfsStatus.MatchCount(hits.Count);
            return;
        }

        var chosen = hits[list.SelectedIndex];
        _workspace.Activate(chosen.Doc.Id);
        SelectTabForActiveDocument();
        if (DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            await pdfView.RunExternalFindAsync(query, chosen.Hit.PageIndex);
        }

        StatusText.Text = FindAllOpenPdfsStatus.OpenedHit(chosen.Doc.DisplayName, chosen.Hit.PageIndex);
    }

    private async void CloseTabMenuItem_Click(object sender, RoutedEventArgs e) => await CloseActiveTabAsync();

    private async void CloseAllMenuItem_Click(object sender, RoutedEventArgs e) => await CloseAllAsync();

    private async void ClearRecentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _recentFiles.ClearAsync();
        RefreshRecentList();
        StatusText.Text = AppShellStatus.RecentFilesCleared;
    }

    private async void PasteMenuItem_Click(object sender, RoutedEventArgs e) => await HandlePasteAsync();

    private async Task HandlePasteAsync()
    {
        if (DocumentTabs.SelectedItem is TabViewItem { Content: ImageDocumentView imageView })
        {
            await imageView.PasteFromClipboardAsync();
            return;
        }

        if (DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            await pdfView.PasteFromClipboardAsync();
            return;
        }

        await PasteWhenNoDocumentAsync();
    }

    private async Task PasteWhenNoDocumentAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Bitmap))
            {
                await NewFromClipboardAsync();
                return;
            }

            if (content.Contains(StandardDataFormats.StorageItems))
            {
                var items = await content.GetStorageItemsAsync();
                var opened = 0;
                foreach (var item in items.OfType<StorageFile>())
                {
                    await OpenPathAsync(item.Path);
                    opened++;
                }

                if (opened > 0)
                {
                    StatusText.Text = opened == 1
                        ? AppShellStatus.OpenedFileFromClipboard
                        : AppShellStatus.FormatOpenedFilesFromClipboard(opened);
                    return;
                }
            }

            if (content.Contains(StandardDataFormats.Text))
            {
                var text = (await content.GetTextAsync())?.Trim();
                if (!string.IsNullOrWhiteSpace(text)
                    && text.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0
                    && (File.Exists(text) || Directory.Exists(text)))
                {
                    if (File.Exists(text))
                    {
                        await OpenPathAsync(text);
                        StatusText.Text = AppShellStatus.OpenedPathFromClipboard;
                        return;
                    }
                }
            }

            StatusText.Text = AppShellStatus.ClipboardNoImageOrPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paste when empty failed");
            StatusText.Text = AppShellStatus.PasteFailed;
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => App.CurrentApp.CloseAllWindows();

    private async void ToggleSidebarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var visible = SidebarBorder.Visibility != Visibility.Visible;
        ApplySidebarVisibility(visible);
        var settings = _settingsStore.Current;
        settings.SidebarVisible = visible;
        await _settingsStore.SaveAsync(settings);
    }

    private async void ToggleToolbarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentTabs.TabItems.Count == 0)
        {
            StatusText.Text = AppShellStatus.OpenDocumentToToggleToolbar;
            return;
        }

        var visible = !_settingsStore.Current.ToolbarVisible;
        ApplyToolbarVisibilityToOpenDocuments(visible);
        var settings = _settingsStore.Current;
        settings.ToolbarVisible = visible;
        await _settingsStore.SaveAsync(settings);
        ToggleToolbarMenuItem.Text = ToolbarVisibilityLabel.For(visible);
    }

    private void ApplyToolbarVisibilityToOpenDocuments(bool visible)
    {
        foreach (var tab in DocumentTabs.TabItems.OfType<TabViewItem>())
        {
            switch (tab.Content)
            {
                case PdfDocumentView pdfView:
                    pdfView.SetToolbarVisible(visible);
                    break;
                case ImageDocumentView imageView:
                    imageView.SetToolbarVisible(visible);
                    break;
            }
        }
    }

    private void FullscreenMenuItem_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private async void ThemeSystemItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.System);

    private async void ThemeLightItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.Light);

    private async void ThemeDarkItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.Dark);

    private async void PreferencesMenuItem_Click(object sender, RoutedEventArgs e) => await ShowPreferencesAsync();

    private async void CheckForUpdatesMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ShowUpdateCheckAsync();

    private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "About Glyph",
            Content = $"Glyph {AppUpdateCheckPolicy.ShippedVersion}\n\nNative Windows document preview and light editing.",
            PrimaryButtonText = ThirdPartyNoticesPolicy.AboutButton,
            CloseButtonText = AppUpdateCheckPolicy.CloseButton,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ShowThirdPartyNoticesAsync();
        }
    }

    private async Task ShowThirdPartyNoticesAsync()
    {
        var path = ResolveThirdPartyNoticesPath();
        string body;
        if (path is null)
        {
            body = ThirdPartyNoticesPolicy.MissingFile;
        }
        else
        {
            try
            {
                body = await File.ReadAllTextAsync(path);
            }
            catch (Exception ex)
            {
                body = ThirdPartyNoticesPolicy.MissingFile + "\n\n" + ex.Message;
            }
        }

        var box = new TextBox
        {
            Text = body,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = 560,
            Height = 420,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
        };
        var notices = new ContentDialog
        {
            Title = ThirdPartyNoticesPolicy.DialogTitle,
            Content = box,
            CloseButtonText = AppUpdateCheckPolicy.CloseButton,
            XamlRoot = RootGrid.XamlRoot,
        };
        await notices.ShowAsync();
    }

    private static string? ResolveThirdPartyNoticesPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ThirdPartyNoticesPolicy.RepoRelativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ThirdPartyNoticesPolicy.RepoRelativePath)),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task ShowUpdateCheckAsync()
    {
        string? latestTag = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Glyph-UpdateCheck/0.1");
            var json = await http.GetStringAsync(AppUpdateCheckPolicy.LatestReleaseApiUrl);
            latestTag = TryReadGitHubReleaseTag(json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Update check could not reach GitHub Releases API");
        }

        var dialog = new ContentDialog
        {
            Title = AppUpdateCheckPolicy.DialogTitle,
            Content = AppUpdateCheckPolicy.FormatWithLatest(latestTag),
            PrimaryButtonText = AppUpdateCheckPolicy.OpenReleasesButton,
            CloseButtonText = AppUpdateCheckPolicy.CloseButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await Launcher.LaunchUriAsync(new Uri(AppUpdateCheckPolicy.ReleasesUrl));
    }

    private static string? TryReadGitHubReleaseTag(string json)
    {
        // Avoid a JSON dependency in the shell: pick "tag_name":"…"
        const string key = "\"tag_name\"";
        var idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var colon = json.IndexOf(':', idx + key.Length);
        if (colon < 0)
        {
            return null;
        }

        var firstQuote = json.IndexOf('"', colon + 1);
        if (firstQuote < 0)
        {
            return null;
        }

        var secondQuote = json.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
        {
            return null;
        }

        return json[(firstQuote + 1)..secondQuote];
    }

    private void NextTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace.ActivateNext())
        {
            SelectTabForActiveDocument();
        }
    }

    private void PreviousTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace.ActivatePrevious())
        {
            SelectTabForActiveDocument();
        }
    }

    private async void MoveTabToNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace.ActiveDocument is { } active)
        {
            await TearTabToNewWindowAsync(active.Id);
        }
    }

    private async void DocumentTabs_AddTabButtonClick(TabView sender, object args) => await OpenWithPickerAsync(allowMultiple: false);

    private async void DocumentTabs_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
    {
        if (args.Tab is not TabViewItem { Tag: DocumentId id })
        {
            return;
        }

        await TearTabToNewWindowAsync(id);
    }

    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is DocumentId id)
        {
            await CloseDocumentAsync(id);
        }
    }

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem { Tag: DocumentId id })
        {
            _workspace.Activate(id);
            StatusText.Text = _workspace.ActiveDocument?.Path ?? _workspace.ActiveDocument?.DisplayName ?? AppShellStatus.Ready;
        }

        ToggleToolbarMenuItem.Text = ToolbarVisibilityLabel.For(_settingsStore.Current.ToolbarVisible);
        UpdateEmptyState();
    }

    private void DocumentTabs_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args)
    {
        var ordered = DocumentTabs.TabItems
            .OfType<TabViewItem>()
            .Select(t => t.Tag)
            .OfType<DocumentId>()
            .ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        _workspace.Reorder(ordered);
        _ = PersistSessionAsync();
    }

    private async void RecentList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentFileEntry entry)
        {
            await OpenPathAsync(entry.Path);
        }
    }

    private void DropHost_DragOver(object sender, DragEventArgs e)
    {
        // Thumbnail sidebars handle page-insert drops themselves; shell opens files.
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            return;
        }

        if (ExplorerFileDropPolicy.ShouldOpenDroppedFiles(
                e.DataView.Contains(StandardDataFormats.StorageItems),
                textLooksLikePageDrag: false))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = ExplorerFileDropPolicy.DragCaption;
        }
    }

    private async void DropHost_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // In-app page drags carry Glyph text payloads; those belong to thumbnail drop targets.
        var pageDragText = false;
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            try
            {
                var text = await e.DataView.GetTextAsync();
                if (PageDragPayload.TryParse(text, out _))
                {
                    pageDragText = true;
                }
            }
            catch
            {
                // Fall through to storage-item open.
            }
        }

        if (!ExplorerFileDropPolicy.ShouldOpenDroppedFiles(
                e.DataView.Contains(StandardDataFormats.StorageItems),
                pageDragText))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = FileFormatDetector.FilterSupportedPaths(
            items.OfType<StorageFile>().Select(f => f.Path));
        foreach (var path in paths)
        {
            await OpenPathAsync(path);
        }
    }

    private async Task NewFromClipboardAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Bitmap))
            {
                StatusText.Text = AppShellStatus.ClipboardNoImage;
                return;
            }

            var bitmapRef = await content.GetBitmapAsync();
            using var stream = await bitmapRef.OpenReadAsync();
            var folder = await StorageFolder.GetFolderFromPathAsync(
                System.IO.Path.GetTempPath());
            var fileName = ClipboardImageFileName.ForTimestamp(DateTime.Now);
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

            using (var outStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAndCloseAsync(stream.GetInputStreamAt(0), outStream.GetOutputStreamAt(0));
            }

            var existing = _workspace.FindByPath(file.Path);
            var session = _workspace.Open(DocumentKind.Image, file.Name, file.Path);
            session.MarkDirty();

            if (existing is null)
            {
                var documentContent = await CreateDocumentContentAsync(session);
                if (documentContent is null)
                {
                    _workspace.Close(session.Id);
                    StatusText.Text = AppShellStatus.CouldNotOpenClipboardImage;
                    return;
                }

                var tab = new TabViewItem
                {
                    Header = file.Name + "*",
                    Tag = session.Id,
                    IsClosable = true,
                    Content = documentContent,
                };
                AttachTabContextFlyout(tab);
                DocumentTabs.TabItems.Add(tab);
            }

            SelectTabForActiveDocument();
            UpdateEmptyState();
            await PersistSessionAsync();
            StatusText.Text = AppShellStatus.FormatCreatedClipboardImage(file.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "New from Clipboard failed");
            StatusText.Text = AppShellStatus.CouldNotCreateClipboardImage;
        }
    }

    private async Task CaptureFromCameraAsync()
    {
        try
        {
            var root = Content?.XamlRoot;
            if (root is null)
            {
                StatusText.Text = WebcamCaptureUi.CameraUiUnavailable;
                return;
            }

            StatusText.Text = WebcamCaptureUi.StartingCamera;
            var captured = await WebcamCaptureHelper.CaptureAsync(
                root,
                title: WebcamCaptureUi.CaptureFromCameraTitle,
                hint: WebcamCaptureUi.CaptureFromCameraHint);
            if (captured is null)
            {
                StatusText.Text = WebcamCaptureUi.CaptureCancelledOrUnavailable;
                return;
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetTempPath());
            var fileName = CaptureFileName.CameraPng(DateTime.Now);
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            var png = Glyph.Core.Signatures.SignaturePngEncoder.EncodeBgra(
                captured.BgraPixels,
                captured.Width,
                captured.Height);
            await FileIO.WriteBytesAsync(file, png);

            var offerCrop = new ContentDialog
            {
                Title = WebcamCaptureUi.CaptureDialogTitle,
                Content = WebcamCaptureUi.CaptureDialogBody,
                PrimaryButtonText = WebcamCaptureUi.OpenNowButton,
                CloseButtonText = WebcamCaptureUi.DiscardButton,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = root,
            };
            if (await offerCrop.ShowAsync() != ContentDialogResult.Primary)
            {
                try
                {
                    await file.DeleteAsync();
                }
                catch
                {
                    // ignore
                }

                StatusText.Text = WebcamCaptureUi.CaptureDiscarded;
                return;
            }

            await OpenPathAsync(file.Path);
            StatusText.Text = WebcamCaptureUi.FormatOpenedCapture(file.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, WebcamCaptureUi.CaptureFailedLog);
            StatusText.Text = WebcamCaptureUi.FormatCaptureFailed(ex.Message);
        }
    }

    private async Task ScanDocumentAsync()
    {
        try
        {
            StatusText.Text = ScanDialogUi.LookingForScanners;
            var devices = await ScannerCaptureHelper.DiscoverAsync();
            if (devices.Count == 0)
            {
                StatusText.Text = ScanDialogUi.NoScannersFound;
                var none = new ContentDialog
                {
                    Title = ScanDialogUi.NoScannersDialogTitle,
                    Content = ScanDialogUi.NoScannersDialogBody,
                    CloseButtonText = ScanDialogUi.OkButton,
                    XamlRoot = Content.XamlRoot,
                };
                await none.ShowAsync();
                return;
            }

            var deviceBox = new ComboBox
            {
                Header = ScanDialogUi.ScannerHeader,
                Width = 320,
                ItemsSource = devices.Select(d => d.Name).ToList(),
                SelectedIndex = 0,
            };
            var sourceBox = new ComboBox
            {
                Header = ScanDialogUi.SourceHeader,
                Width = 320,
                ItemsSource = ScanDialogUi.SourceLabels.ToList(),
                SelectedIndex = 0,
            };
            var colorBox = new ComboBox
            {
                Header = ScanDialogUi.ColorHeader,
                Width = 320,
                ItemsSource = ScanDialogUi.ColorModeLabels.ToList(),
                SelectedIndex = 0,
            };
            var dpiBox = new ComboBox
            {
                Header = ScanDialogUi.DpiHeader,
                Width = 320,
                ItemsSource = new[] { "150", "200", "300", "600" },
                SelectedIndex = 2,
            };
            var duplex = new CheckBox { Content = ScanDialogUi.DuplexLabel };
            var cropBox = new ComboBox
            {
                Header = ScanDialogUi.AutoCropHeader,
                Width = 320,
                ItemsSource = ScanDialogUi.AutoCropLabels.ToList(),
                SelectedIndex = 1,
            };
            var straighten = new CheckBox { Content = ScanDialogUi.StraightenLabel };
            var paperBox = new ComboBox
            {
                Header = ScanDialogUi.PaperSizeHeader,
                Width = 320,
                ItemsSource = ScanDialogUi.PaperSizeLabels.ToList(),
                SelectedIndex = 0,
            };
            var pagesBox = new NumberBox
            {
                Header = ScanDialogUi.MaxPagesHeader,
                Value = ScanDialogUi.MinMaxPages,
                Minimum = ScanDialogUi.MinMaxPages,
                Maximum = ScanDialogUi.MaxMaxPages,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width = 160,
            };
            var destBox = new ComboBox
            {
                Header = ScanDialogUi.DestinationHeader,
                Width = 320,
                ItemsSource = ScanDialogUi.DestinationLabels.ToList(),
                SelectedIndex = 1,
            };
            var brightness = new Slider
            {
                Header = ScanDialogUi.BrightnessHeader,
                Minimum = ScanDialogUi.ToneMin,
                Maximum = ScanDialogUi.ToneMax,
                Value = ScanDialogUi.ToneNeutral,
                Width = 320,
            };
            var contrast = new Slider
            {
                Header = ScanDialogUi.ContrastHeader,
                Minimum = ScanDialogUi.ToneMin,
                Maximum = ScanDialogUi.ToneMax,
                Value = ScanDialogUi.ToneNeutral,
                Width = 320,
            };

            var dialog = new ContentDialog
            {
                Title = ScanDialogUi.Title,
                Content = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            deviceBox, sourceBox, colorBox, dpiBox, paperBox, duplex, cropBox, straighten, pagesBox, destBox, brightness, contrast,
                        },
                    },
                    MaxHeight = 480,
                },
                PrimaryButtonText = ScanDialogUi.PrimaryButton,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = ScanDialogUi.Cancelled;
                return;
            }

            var device = devices[Math.Clamp(deviceBox.SelectedIndex, 0, devices.Count - 1)];
            var source = sourceBox.SelectedIndex switch
            {
                1 => Windows.Devices.Scanners.ImageScannerScanSource.Feeder,
                2 => Windows.Devices.Scanners.ImageScannerScanSource.AutoConfigured,
                _ => Windows.Devices.Scanners.ImageScannerScanSource.Flatbed,
            };
            var color = colorBox.SelectedIndex switch
            {
                1 => Windows.Devices.Scanners.ImageScannerColorMode.Grayscale,
                2 => Windows.Devices.Scanners.ImageScannerColorMode.Monochrome,
                _ => Windows.Devices.Scanners.ImageScannerColorMode.Color,
            };
            uint.TryParse(dpiBox.SelectedItem as string, out var dpi);
            if (dpi == 0)
            {
                dpi = 300;
            }

            dpi = (uint)ScanDialogUi.ClampDpi((int)dpi);

            var scanRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GlyphScans");
            Directory.CreateDirectory(scanRoot);
            var folder = await StorageFolder.GetFolderFromPathAsync(scanRoot);
            var sessionFolder = await folder.CreateFolderAsync(
                DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                CreationCollisionOption.GenerateUniqueName);

            var (pageSize, autoDetectPaper) = paperBox.SelectedIndex switch
            {
                1 => (Windows.Graphics.Printing.PrintMediaSize.NorthAmericaLetter, false),
                2 => (Windows.Graphics.Printing.PrintMediaSize.NorthAmericaLegal, false),
                3 => (Windows.Graphics.Printing.PrintMediaSize.IsoA4, false),
                4 => (Windows.Graphics.Printing.PrintMediaSize.IsoA5, false),
                5 => (Windows.Graphics.Printing.PrintMediaSize.IsoA3, false),
                6 => (Windows.Graphics.Printing.PrintMediaSize.NorthAmericaTabloid, false),
                7 => (Windows.Graphics.Printing.PrintMediaSize.NorthAmericaStatement, false),
                8 => (Windows.Graphics.Printing.PrintMediaSize.Default, true),
                _ => (Windows.Graphics.Printing.PrintMediaSize.Default, false),
            };

            StatusText.Text = ScanDialogUi.FormatScanningWith(device.Name);
            var files = await ScannerCaptureHelper.ScanToFolderAsync(
                device.Id,
                sessionFolder,
                new ScannerOptions(
                    Source: source,
                    ColorMode: color,
                    Dpi: dpi,
                    Duplex: duplex.IsChecked == true,
                    AutoCrop: cropBox.SelectedIndex > 0,
                    MultiPhoto: cropBox.SelectedIndex == 2,
                    Brightness: ScanDialogUi.ToneOrNull((int)brightness.Value),
                    Contrast: ScanDialogUi.ToneOrNull((int)contrast.Value),
                    MaxPages: (uint)ScanDialogUi.ClampMaxPages(pagesBox.Value),
                    PageSize: pageSize,
                    AutoDetectPageSize: autoDetectPaper));

            if (files.Count == 0)
            {
                StatusText.Text = ScanDialogUi.ProducedNoFiles;
                return;
            }

            var paths = files.Select(f => f.Path).ToList();
            if (straighten.IsChecked == true)
            {
                StatusText.Text = ScanDialogUi.FormatStraightening(paths.Count);
                foreach (var path in paths)
                {
                    try
                    {
                        await using var doc = await _imageDecoder.OpenAsync(path);
                        await _imageProcessor.DeskewAsync(doc, thresholdPercent: 40, crop: true);
                        await _imageEncoder.SaveAsync(doc, path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Deskew failed for {Path}", path);
                    }
                }
            }
            switch (destBox.SelectedIndex)
            {
                case 0:
                    foreach (var path in paths)
                    {
                        await OpenPathAsync(path);
                    }

                    StatusText.Text = ScanDialogUi.FormatOpenedImages(paths.Count);
                    break;
                case 2:
                    await InsertScansIntoActivePdfAsync(paths);
                    break;
                default:
                    var pdfPath = System.IO.Path.Combine(
                        sessionFolder.Path,
                        CaptureFileName.ScanPdf(DateTime.Now));
                    await _imageEncoder.WriteImagesAsPdfAsync(paths, pdfPath);
                    await OpenPathAsync(pdfPath);
                    StatusText.Text = ScanDialogUi.FormatCreatedPdf(paths.Count);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            StatusText.Text = ScanDialogUi.FormatFailed(ex.Message);
        }
    }

    private async Task InsertScansIntoActivePdfAsync(IReadOnlyList<string> imagePaths)
    {
        var active = _workspace.ActiveDocument;
        if (active is null || active.Kind != DocumentKind.Pdf)
        {
            StatusText.Text = ScanDialogUi.OpenPdfFirstToInsert;
            return;
        }

        if (!_openEngines.TryGetValue(active.Id, out var engine) || engine is not IPdfDocument pdf)
        {
            StatusText.Text = ScanDialogUi.PdfEngineUnavailable;
            return;
        }

        var insertPdf = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            CaptureFileName.ScanInsertTempPdf());
        await _imageEncoder.WriteImagesAsPdfAsync(imagePaths, insertPdf);
        await using var scanDoc = await _pdfFactory.OpenAsync(insertPdf);
        await _pdfPageEditor.InsertPagesAsync(
            pdf,
            scanDoc,
            Enumerable.Range(0, scanDoc.PageCount).ToList(),
            pdf.PageCount);
        active.MarkDirty();
        StatusText.Text = ScanDialogUi.FormatInsertedPages(imagePaths.Count);
    }

    private void ShareActiveDocument()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path))
        {
            StatusText.Text = DocumentShareStatus.NothingToShare;
            return;
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _shareHelper ??= new DocumentShareHelper(hwnd);
            _shareHelper.ShowShareUi(active.DisplayName, active.Path);
            StatusText.Text = DocumentShareStatus.ShareOpened;
        }
        catch (Exception ex)
        {
            StatusText.Text = DocumentShareStatus.ShareFailed(ex.Message);
        }
    }

    /// <summary>Toolbar Share button entry (F54-16).</summary>
    public void ShareActiveDocumentFromToolbar() => ShareActiveDocument();

    /// <summary>Toolbar Sidebar toggle entry (F54-01).</summary>
    public async Task ToggleSidebarFromToolbarAsync()
    {
        var visible = SidebarBorder.Visibility != Visibility.Visible;
        ApplySidebarVisibility(visible);
        var settings = _settingsStore.Current;
        settings.SidebarVisible = visible;
        await _settingsStore.SaveAsync(settings);
    }

    private async Task ShowActiveInExplorerAsync()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = DocumentShareStatus.NoPathForExplorer;
            return;
        }

        try
        {
            await DocumentShareHelper.OpenContainingFolderAsync(path);
            StatusText.Text = DocumentShareStatus.OpenedContainingFolder;
        }
        catch (Exception ex)
        {
            StatusText.Text = DocumentShareStatus.ShowInExplorerFailedMessage(ex.Message);
        }
    }

    private async Task ShowActivePropertiesAsync()
    {
        var kind = DocumentPropertiesRouting.For(_workspace.ActiveDocument?.Kind ?? DocumentKind.Unknown);
        if (kind == DocumentPropertiesKind.PdfInfo
            && DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            await pdfView.ShowPropertiesAsync();
            return;
        }

        if (kind == DocumentPropertiesKind.ImageMetadata
            && DocumentTabs.SelectedItem is TabViewItem { Content: ImageDocumentView imageView })
        {
            await imageView.ShowPropertiesAsync();
            return;
        }

        StatusText.Text = DocumentPropertiesRouting.NoDocumentStatus;
    }

    private async Task SaveActiveDocumentAsync(bool saveAs)
    {
        var active = _workspace.ActiveDocument;
        if (active is null)
        {
            StatusText.Text = DocumentSaveStatus.NoDocument;
            return;
        }

        if (!saveAs
            && !string.IsNullOrWhiteSpace(active.Path)
            && ReadOnlySavePolicy.RequiresSaveAs(active.IsReadOnly, PathUtilities.IsPathReadOnly(active.Path)))
        {
            var warn = new ContentDialog
            {
                Title = ReadOnlySavePolicy.DialogTitle,
                Content = ReadOnlySavePolicy.DialogMessage,
                PrimaryButtonText = DialogButtons.SaveAsEllipsis,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await warn.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = ReadOnlySavePolicy.CancelledStatus;
                return;
            }

            saveAs = true;
        }

        if (DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            await pdfView.SaveDocumentAsync(saveAs);
            return;
        }

        if (DocumentTabs.SelectedItem is TabViewItem { Content: ImageDocumentView imageView })
        {
            await imageView.SaveDocumentAsync(saveAs);
            return;
        }

        StatusText.Text = DocumentSaveStatus.NoDocument;
    }

    /// <summary>Mark the active document session clean after a successful Save / Save As.</summary>
    public void NotifyActiveDocumentSaved(string path)
    {
        var active = _workspace.ActiveDocument;
        if (active is null)
        {
            return;
        }

        var previousPath = active.Path;
        active.Path = path;
        active.DisplayName = System.IO.Path.GetFileName(path);
        active.IsReadOnly = PathUtilities.IsPathReadOnly(path);
        active.MarkClean();
        if (DocumentTabs.SelectedItem is TabViewItem tab)
        {
            tab.Header = active.DisplayName + (active.IsReadOnly ? " (read-only)" : string.Empty);
            if (tab.Content is PdfDocumentView pdfView)
            {
                pdfView.ClearUnsavedEdits();
            }
            else if (tab.Content is ImageDocumentView imageView)
            {
                imageView.ClearUnsavedEdits();
            }
        }

        StatusText.Text = DocumentSaveStatus.Saved(active.DisplayName, active.IsReadOnly);
        _ = _recentFiles.AddAsync(path);
        RefreshRecentList();
        _ = DiscardRecoveryAsync(previousPath);
        if (!string.Equals(previousPath, path, StringComparison.OrdinalIgnoreCase))
        {
            _ = DiscardRecoveryAsync(path);
        }

        if (_settingsStore.Current.VersionSnapshotsEnabled)
        {
            _ = CaptureVersionSnapshotAsync(path);
        }

        _ = PersistSessionAsync();
    }

    private async Task CaptureVersionSnapshotAsync(string path)
    {
        try
        {
            var entry = await _snapshotStore.CaptureAsync(path);
            if (entry is not null)
            {
                _logger.LogInformation("Version snapshot saved for {Path} → {Snapshot}", path, entry.SnapshotPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Version snapshot failed for {Path}", path);
        }
    }

    private async Task DuplicateActiveDocumentAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = AppShellStatus.SaveFirstToDuplicate;
            return;
        }

        try
        {
            if (active.IsDirty)
            {
                var prompt = new ContentDialog
                {
                    Title = DocumentClosePolicy.UnsavedTitle,
                    Content = DocumentClosePolicy.DuplicateSaveFirstPrompt,
                    PrimaryButtonText = DialogButtons.SaveAndDuplicate,
                    SecondaryButtonText = DialogButtons.DuplicateWithoutSaving,
                    CloseButtonText = DialogButtons.Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot,
                };
                var result = await prompt.ShowAsync();
                if (result == ContentDialogResult.None)
                {
                    return;
                }

                if (result == ContentDialogResult.Primary)
                {
                    await SaveActiveDocumentAsync(saveAs: false);
                }
            }

            var source = active.Path!;
            var candidate = DocumentFileNamePolicy.SuggestDuplicatePath(source);

            File.Copy(source, candidate);
            await OpenPathAsync(candidate);
            StatusText.Text = AppShellStatus.FormatDuplicatedAs(System.IO.Path.GetFileName(candidate));
        }
        catch (Exception ex)
        {
            StatusText.Text = AppShellStatus.FormatFailed(AppShellStatus.DuplicateFailedPrefix, ex.Message);
        }
    }

    private async Task RenameActiveDocumentAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = AppShellStatus.SaveFirstToRename;
            return;
        }

        try
        {
            var currentName = System.IO.Path.GetFileName(active.Path);
            var box = new TextBox
            {
                Header = AppShellStatus.NewFileNameHeader,
                Text = currentName,
                Width = 320,
            };
            var dialog = new ContentDialog
            {
                Title = AppShellStatus.RenameDialogTitle,
                Content = box,
                PrimaryButtonText = AppShellStatus.RenameButton,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var newName = (box.Text ?? string.Empty).Trim();
            var rename = DocumentFileNamePolicy.EvaluateRename(active.Path!, newName);
            if (rename.Status == RenamePathStatus.Invalid)
            {
                StatusText.Text = AppShellStatus.InvalidFileName;
                return;
            }

            if (rename.Status == RenamePathStatus.Unchanged)
            {
                StatusText.Text = AppShellStatus.NameUnchanged;
                return;
            }

            if (rename.Status == RenamePathStatus.Conflict)
            {
                StatusText.Text = AppShellStatus.FileNameExists;
                return;
            }

            var dest = rename.DestinationPath!;
            if (active.IsDirty)
            {
                await SaveActiveDocumentAsync(saveAs: false);
            }

            File.Move(active.Path!, dest);
            RetargetActiveDocument(dest);
            StatusText.Text = AppShellStatus.FormatRenamedTo(newName);
        }
        catch (Exception ex)
        {
            StatusText.Text = AppShellStatus.FormatFailed(AppShellStatus.RenameFailedPrefix, ex.Message);
        }
    }

    private async Task MoveActiveDocumentAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = AppShellStatus.SaveFirstToMove;
            return;
        }

        try
        {
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                StatusText.Text = AppShellStatus.MoveCancelled;
                return;
            }

            var fileName = System.IO.Path.GetFileName(active.Path);
            var dest = DocumentMovePolicy.DestinationPath(active.Path!, folder.Path);
            if (DocumentMovePolicy.IsSameFolder(active.Path!, folder.Path))
            {
                StatusText.Text = AppShellStatus.AlreadyInFolder;
                return;
            }

            var destExists = File.Exists(dest);
            var userConfirmed = false;
            if (destExists)
            {
                var overwrite = new ContentDialog
                {
                    Title = AppShellStatus.ReplaceExistingTitle,
                    Content = $"“{fileName}” already exists in the destination folder.",
                    PrimaryButtonText = AppShellStatus.ReplaceButton,
                    CloseButtonText = DialogButtons.Cancel,
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot,
                };
                userConfirmed = await overwrite.ShowAsync() == ContentDialogResult.Primary;
            }

            switch (DocumentMovePolicy.EvaluateOverwrite(
                        destExists,
                        PathUtilities.IsPathReadOnly(dest),
                        userConfirmed))
            {
                case MoveOverwriteDecision.Cancelled:
                    return;
                case MoveOverwriteDecision.BlockedReadOnly:
                    StatusText.Text = AppShellStatus.DestinationReadOnly;
                    return;
            }

            if (active.IsDirty)
            {
                await SaveActiveDocumentAsync(saveAs: false);
            }

            File.Move(active.Path!, dest, overwrite: true);
            RetargetActiveDocument(dest);
            StatusText.Text = AppShellStatus.FormatMovedTo(folder.Path);
        }
        catch (Exception ex)
        {
            StatusText.Text = AppShellStatus.FormatFailed(AppShellStatus.MoveFailedPrefix, ex.Message);
        }
    }

    private void RetargetActiveDocument(string newPath)
    {
        var active = _workspace.ActiveDocument;
        if (active is null)
        {
            return;
        }

        active.Path = newPath;
        active.DisplayName = System.IO.Path.GetFileName(newPath);
        active.IsReadOnly = PathUtilities.IsPathReadOnly(newPath);
        if (_openEngines.TryGetValue(active.Id, out var engine))
        {
            switch (engine)
            {
                case IPdfDocument pdf:
                    pdf.Path = newPath;
                    break;
                case IImageDocument image:
                    image.Path = newPath;
                    break;
            }
        }

        if (DocumentTabs.SelectedItem is TabViewItem tab)
        {
            tab.Header = active.DisplayName + (active.IsReadOnly ? " (read-only)" : string.Empty);
        }

        _ = _recentFiles.AddAsync(newPath);
        RefreshRecentList();
    }

    private void CopyActivePath()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = DocumentShareStatus.NoPath;
            return;
        }

        DocumentShareHelper.CopyPathToClipboard(path);
        StatusText.Text = DocumentShareStatus.PathCopied;
    }

    private async Task CopyActiveFileAsync()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = DocumentShareStatus.NoFile;
            return;
        }

        try
        {
            await DocumentShareHelper.CopyFileToClipboardAsync(path);
            StatusText.Text = DocumentShareStatus.FileCopied;
        }
        catch (Exception ex)
        {
            StatusText.Text = DocumentShareStatus.CopyFileFailed(ex.Message);
        }
    }

    private async Task OpenActiveWithDefaultAsync()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = DocumentShareStatus.NoFileToOpen;
            return;
        }

        try
        {
            await DocumentShareHelper.OpenWithDefaultAsync(path);
        }
        catch (Exception ex)
        {
            StatusText.Text = DocumentShareStatus.OpenWithFailed(ex.Message);
        }
    }

    private async Task EmailActiveAsync()
    {
        var active = _workspace.ActiveDocument;
        try
        {
            await DocumentShareHelper.SendMailtoAsync(
                subject: DocumentShareStatus.MailtoSubject(active?.DisplayName),
                body: DocumentShareStatus.MailtoBody,
                attachmentPath: active?.Path);
            StatusText.Text = DocumentShareStatus.MailOpened;
        }
        catch (Exception ex)
        {
            StatusText.Text = DocumentShareStatus.EmailFailed(ex.Message);
        }
    }

    private async Task OpenWithPickerAsync(bool allowMultiple)
    {
        if (allowMultiple)
        {
            var multiPicker = new FileOpenPicker();
            InitializePicker(multiPicker);
            var pickedFiles = await multiPicker.PickMultipleFilesAsync();
            foreach (var pickedFile in pickedFiles)
            {
                await OpenPathAsync(pickedFile.Path);
            }

            return;
        }

        var picker = new FileOpenPicker();
        InitializePicker(picker);
        var singleFile = await picker.PickSingleFileAsync();
        if (singleFile is not null)
        {
            await OpenPathAsync(singleFile.Path);
        }
    }

    private void InitializePicker(FileOpenPicker picker)
    {
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        foreach (var extension in FileFormatDetector.SupportedExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
    }

    private async Task OpenPathAsync(string path)
    {
        try
        {
            path = PathUtilities.NormalizeOpenPath(path);
            if (!PathUtilities.FileExists(path))
            {
                StatusText.Text = AppShellStatus.FileNotFound;
                return;
            }

            if (!FileFormatDetector.IsSupported(path))
            {
                StatusText.Text = AppShellStatus.UnsupportedFileType;
                return;
            }

            // Prefer a new window when configured and this window already has tabs (F01-03).
            if (_settingsStore.Current.OpenFilesInSeparateWindows
                && _workspace.Documents.Count > 0
                && _workspace.FindByPath(path) is null)
            {
                var window = App.CurrentApp.OpenNewWindow();
                await window.OpenDocumentPathAsync(path);
                StatusText.Text = AppShellStatus.FormatOpenedInNewWindow(System.IO.Path.GetFileName(path));
                return;
            }

            var kind = FileFormatDetector.DetectKind(path);
            var displayName = System.IO.Path.GetFileName(path);
            var existing = _workspace.FindByPath(path);
            var session = _workspace.Open(kind, displayName, path);
            session.IsReadOnly = PathUtilities.IsPathReadOnly(path);

            if (existing is null)
            {
                var content = await CreateDocumentContentAsync(session);
                if (content is null)
                {
                    _workspace.Close(session.Id);
                    return;
                }

                var tab = new TabViewItem
                {
                    Header = displayName + (session.IsReadOnly ? " (read-only)" : string.Empty),
                    Tag = session.Id,
                    IsClosable = true,
                    Content = content,
                };
                AttachTabContextFlyout(tab);
                DocumentTabs.TabItems.Add(tab);
            }

            SelectTabForActiveDocument();
            await _recentFiles.AddAsync(path);
            RefreshRecentList();
            UpdateEmptyState();

            var openedLabel = existing is null
                ? kind switch
                {
                    DocumentKind.Pdf => AppShellStatus.FormatOpenedPdf(displayName),
                    DocumentKind.Image => AppShellStatus.FormatOpenedImage(displayName),
                    _ => $"Opened {displayName}",
                }
                : $"Activated {displayName}";
            StatusText.Text = session.IsReadOnly ? openedLabel + " · read-only" : openedLabel;

            _logger.LogInformation("Opened {Kind} document {Path}", kind, path);
            await PersistSessionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open {Path}", path);
            StatusText.Text = AppShellStatus.FailedToOpenFile;
        }
    }

    /// <summary>
    /// Folder prev/next: reuse the active image tab when possible instead of stacking tabs.
    /// </summary>
    private async Task OpenImageSiblingAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                StatusText.Text = AppShellStatus.FileNotFound;
                return;
            }

            if (_workspace.FindByPath(path) is not null)
            {
                await OpenPathAsync(path);
                return;
            }

            var active = _workspace.ActiveDocument;
            var tab = active is null
                ? null
                : DocumentTabs.TabItems.OfType<TabViewItem>()
                    .FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(active.Id));

            if (active is null
                || active.Kind != DocumentKind.Image
                || tab is null
                || active.IsDirty)
            {
                await OpenPathAsync(path);
                return;
            }

            if (active.Path is not null)
            {
                try
                {
                    await _viewStateStore.SaveAsync(active.Path, active.ViewState);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist view state for {Path}", active.Path);
                }
            }

            if (_openEngines.Remove(active.Id, out var engine))
            {
                await engine.DisposeAsync();
            }

            var displayName = System.IO.Path.GetFileName(path);
            active.Path = path;
            active.DisplayName = displayName;
            active.MarkClean();

            var content = await CreateDocumentContentAsync(active);
            if (content is null)
            {
                StatusText.Text = AppShellStatus.FailedToOpenImage;
                return;
            }

            tab.Header = displayName;
            tab.Content = content;
            await _recentFiles.AddAsync(path);
            RefreshRecentList();
            StatusText.Text = AppShellStatus.FormatOpenedImage(displayName);
            _logger.LogInformation("Navigated image tab to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to image {Path}", path);
            StatusText.Text = AppShellStatus.FailedToOpenFile;
        }
    }

    private async Task<FrameworkElement?> CreateDocumentContentAsync(DocumentSession session)
    {
        if (session.Kind == DocumentKind.Pdf && session.Path is not null)
        {
            var pdf = await OpenPdfWithPasswordAsync(session.Path);
            if (pdf is null)
            {
                return null;
            }

            var settings = _settingsStore.Current;
            var saved = await _viewStateStore.TryLoadAsync(session.Path);
            DocumentViewRestorePolicy.ApplyPdf(
                session.ViewState,
                saved,
                pdf.PageCount,
                DocumentViewRestorePolicy.EffectiveRememberLastPage(settings.RememberLastPage),
                DocumentViewRestorePolicy.EffectiveRememberZoom(settings.RememberZoom),
                settings.DefaultZoom,
                DocumentViewRestorePolicy.ResolveDefaultPageLayout(settings.DefaultPageLayout));

            _openEngines[session.Id] = pdf;
            SidebarStatus.Text = $"{pdf.PageCount} pages — thumbnails and search in the document pane.";
            return new PdfDocumentView(
                pdf,
                _pdfRenderer,
                _pageCache,
                _pdfSearch,
                _pdfText,
                _pdfOutlines,
                _pdfOutlineExport,
                _pdfLinks,
                _pdfPageEditor,
                _pdfAnnotations,
                _pdfRedaction,
                _pdfInfo,
                _pdfOptimize,
                _pdfSecurity,
                _pdfExport,
                _signatures,
                _pdfForms,
                _formValueHistory,
                _formProfile,
                _pdfFactory,
                session.ViewState,
                ownerWindow: this,
                ocr: _ocr,
                onEdited: () => session.MarkDirty());
        }

        if (session.Kind == DocumentKind.Image && session.Path is not null)
        {
            var image = await _imageDecoder.OpenAsync(session.Path);
            image.ColorManagedDisplay = _settingsStore.Current.ColorManagedDisplayDefault;
            var settings = _settingsStore.Current;
            var saved = await _viewStateStore.TryLoadAsync(session.Path);
            DocumentViewRestorePolicy.ApplyImage(
                session.ViewState,
                saved,
                DocumentViewRestorePolicy.EffectiveRememberZoom(settings.RememberZoom));

            _openEngines[session.Id] = image;
            SidebarStatus.Text = $"{image.FormatName} · {image.PixelWidth}×{image.PixelHeight}";
            return new ImageDocumentView(
                image,
                _imageProcessor,
                _imageEncoder,
                session.ViewState,
                openSibling: OpenImageSiblingAsync,
                ocr: _ocr,
                signatures: _signatures,
                decoder: _imageDecoder,
                onEdited: () => session.MarkDirty());
        }

        return CreatePlaceholderContent(session);
    }

    private async Task<IPdfDocument?> OpenPdfWithPasswordAsync(string path)
    {
        try
        {
            return await _pdfFactory.OpenAsync(path);
        }
        catch (PdfPasswordRequiredException)
        {
            // Fall through to password prompt.
        }

        for (var attempt = 0; attempt < PdfPasswordPromptUi.MaxAttempts; attempt++)
        {
            var password = await PromptForPdfPasswordAsync(
                System.IO.Path.GetFileName(path),
                isRetry: attempt > 0);
            if (password is null)
            {
                StatusText.Text = PdfPasswordPromptUi.CancelledStatus;
                return null;
            }

            try
            {
                return await _pdfFactory.OpenAsync(path, password);
            }
            catch (PdfPasswordRequiredException)
            {
                StatusText.Text = PdfPasswordPromptUi.IncorrectStatus;
            }
        }

        StatusText.Text = PdfPasswordPromptUi.FailedStatus;
        return null;
    }

    private async Task<string?> PromptForPdfPasswordAsync(string fileName, bool isRetry)
    {
        var box = new PasswordBox { Width = 280, PlaceholderText = PdfPasswordPromptUi.Placeholder };
        var dialog = new ContentDialog
        {
            Title = PdfPasswordPromptUi.DialogTitle(isRetry),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = PdfPasswordPromptUi.PromptBody(fileName),
                        TextWrapping = TextWrapping.WrapWholeWords,
                    },
                    box,
                },
            },
            PrimaryButtonText = PdfPasswordPromptUi.PrimaryButton,
            CloseButtonText = PreferencesDialogUi.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return box.Password;
    }

    private static FrameworkElement CreatePlaceholderContent(DocumentSession session)
    {
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = session.DisplayName,
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                new TextBlock
                {
                    Text = session.Kind == DocumentKind.Pdf
                        ? "PDF document session ready."
                        : "Image document session ready.",
                    Opacity = 0.75,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
                new TextBlock
                {
                    Text = session.Path ?? string.Empty,
                    Opacity = 0.55,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
    }

    private async Task CloseActiveTabAsync()
    {
        if (_workspace.ActiveDocument is { } active)
        {
            await CloseDocumentAsync(active.Id);
        }
    }

    private async Task CloseAllAsync()
    {
        var ids = _workspace.Documents.Select(d => d.Id).ToList();
        foreach (var id in ids)
        {
            if (!await CloseDocumentAsync(id))
            {
                break;
            }
        }
    }

    /// <summary>Open a path from another window (e.g. tear-off tab).</summary>
    public Task OpenDocumentPathAsync(string path) => OpenPathAsync(path);

    private void AttachTabContextFlyout(TabViewItem tab)
    {
        var flyout = new MenuFlyout();
        var closeItem = new MenuFlyoutItem { Text = AppShellTextLabels.CloseTab };
        closeItem.Click += async (_, _) =>
        {
            if (tab.Tag is DocumentId id)
            {
                await CloseDocumentAsync(id);
            }
        };
        var tearItem = new MenuFlyoutItem { Text = AppShellTextLabels.MoveToNewWindow };
        tearItem.Click += async (_, _) =>
        {
            if (tab.Tag is DocumentId id)
            {
                await TearTabToNewWindowAsync(id);
            }
        };
        flyout.Items.Add(closeItem);
        flyout.Items.Add(tearItem);
        tab.ContextFlyout = flyout;
    }

    private async Task TearTabToNewWindowAsync(DocumentId id)
    {
        var session = _workspace.Documents.FirstOrDefault(d => d.Id.Equals(id));
        if (session is null)
        {
            return;
        }

        if (!TabTearOffPolicy.CanTearOff(session.Path))
        {
            StatusText.Text = TabTearOffPolicy.NeedsSaveStatus;
            return;
        }

        var tab = DocumentTabs.TabItems.OfType<TabViewItem>()
            .FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(id));
        var pathToOpen = session.Path!;
        var dirty = TabTearOffPolicy.RequiresDirtyResolution(session.IsDirty, TabHasUnsavedEdits(id));
        if (dirty)
        {
            var dialog = new ContentDialog
            {
                Title = DocumentClosePolicy.UnsavedTitle,
                Content = TabTearOffPolicy.UnsavedChangesPrompt(session.DisplayName),
                PrimaryButtonText = DialogButtons.SaveAndMove,
                SecondaryButtonText = DialogButtons.MoveRecoveryCopy,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                return;
            }

            if (result == ContentDialogResult.Primary)
            {
                _workspace.Activate(id);
                SelectTabForActiveDocument();
                await SaveActiveDocumentAsync(saveAs: false);
                if (session.IsDirty || TabHasUnsavedEdits(id))
                {
                    StatusText.Text = TabTearOffPolicy.SaveCancelledStatus;
                    return;
                }

                pathToOpen = session.Path!;
            }
            else if (tab is not null)
            {
                await WriteTabRecoveryAsync(tab, session.Path!);
                var entries = await _recoveryStore.ListAsync();
                var match = entries.FirstOrDefault(e =>
                    string.Equals(e.OriginalPath, System.IO.Path.GetFullPath(session.Path!), StringComparison.OrdinalIgnoreCase));
                if (match is null || !File.Exists(match.RecoveryPath))
                {
                    StatusText.Text = TabTearOffPolicy.RecoveryFailedStatus;
                    return;
                }

                pathToOpen = match.RecoveryPath;
            }
        }

        if (!await CloseDocumentAsync(id, skipDirtyPrompt: true))
        {
            return;
        }

        var window = App.CurrentApp.OpenNewWindow();
        await window.OpenDocumentPathAsync(pathToOpen);
        StatusText.Text = TabTearOffPolicy.MovedStatus;
    }

    private async Task<bool> CloseDocumentAsync(DocumentId id, bool skipDirtyPrompt = false)
    {
        var session = _workspace.Documents.FirstOrDefault(d => d.Id.Equals(id));
        if (session is null)
        {
            return true;
        }

        if (DocumentClosePolicy.RequiresDirtyPrompt(session.IsDirty, TabHasUnsavedEdits(id), skipDirtyPrompt))
        {
            var dialog = new ContentDialog
            {
                Title = DocumentClosePolicy.UnsavedTitle,
                Content = DocumentClosePolicy.UnsavedClosePrompt(session.DisplayName),
                PrimaryButtonText = DocumentClosePolicy.CloseButton,
                CloseButtonText = DocumentClosePolicy.CancelButton,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return false;
            }
        }

        if (session.Kind == DocumentKind.Pdf && session.Path is not null)
        {
            try
            {
                await _viewStateStore.SaveAsync(session.Path, session.ViewState);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist view state for {Path}", session.Path);
            }
        }

        // Keep recovery when closing dirty without saving (F50-06); discard when clean (F50-07).
        if (!session.IsDirty && !TabHasUnsavedEdits(id) && session.Path is not null)
        {
            await DiscardRecoveryAsync(session.Path);
        }

        if (_openEngines.Remove(id, out var engine))
        {
            await engine.DisposeAsync();
        }

        _workspace.Close(id);
        var tab = DocumentTabs.TabItems.OfType<TabViewItem>().FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(id));
        if (tab is not null)
        {
            DocumentTabs.TabItems.Remove(tab);
        }

        SelectTabForActiveDocument();
        UpdateEmptyState();
        StatusText.Text = _workspace.ActiveDocument is null ? AppShellStatus.Ready : AppShellStatus.FormatActive(_workspace.ActiveDocument.DisplayName);
        await PersistSessionAsync();
        return true;
    }

    private void SelectTabForActiveDocument()
    {
        if (_workspace.ActiveDocument is null)
        {
            return;
        }

        var tab = DocumentTabs.TabItems.OfType<TabViewItem>()
            .FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(_workspace.ActiveDocument.Id));
        if (tab is not null)
        {
            DocumentTabs.SelectedItem = tab;
        }
    }

    private async Task SetThemeAsync(ThemePreference preference)
    {
        App.CurrentApp.ApplyThemePreference(preference);
        var settings = _settingsStore.Current;
        settings.Theme = preference;
        await _settingsStore.SaveAsync(settings);
        StatusText.Text = AppShellStatus.FormatTheme(preference.ToString());
    }

    private void ApplySidebarVisibility(bool visible)
    {
        var width = Math.Clamp(_settingsStore.Current.SidebarWidth, 140, 480);
        SidebarBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarBorder.Width = width;
        ToggleSidebarMenuItem.Text = visible ? "Hide Sidebar" : "Show Sidebar";
    }

    private void SidebarSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _sidebarResizing = true;
        _sidebarResizeStartX = e.GetCurrentPoint(ContentGrid).Position.X;
        _sidebarResizeStartWidth = SidebarBorder.Width;
        SidebarSplitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void SidebarSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_sidebarResizing)
        {
            return;
        }

        var delta = e.GetCurrentPoint(ContentGrid).Position.X - _sidebarResizeStartX;
        SidebarBorder.Width = Math.Clamp(_sidebarResizeStartWidth + delta, 140, 480);
        e.Handled = true;
    }

    private async void SidebarSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_sidebarResizing)
        {
            return;
        }

        _sidebarResizing = false;
        SidebarSplitter.ReleasePointerCapture(e.Pointer);
        var settings = _settingsStore.Current;
        settings.SidebarWidth = SidebarBorder.Width;
        await _settingsStore.SaveAsync(settings);
        e.Handled = true;
    }

    private void SidebarSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _sidebarResizing = false;
    }

    private void RefreshRecentList() => RecentList.ItemsSource = _recentFiles.GetRecent();

    private void UpdateEmptyState() =>
        EmptyState.Visibility = DocumentTabs.TabItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ResizeAndCenter(int width, int height)
    {
        AppWindow.Resize(new SizeInt32(width, height));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (area is null)
        {
            return;
        }

        var work = area.WorkArea;
        AppWindow.Move(new PointInt32(
            work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2));
    }

    public void ToggleFullscreen()
    {
        var currentlyFullscreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        AppWindow.SetPresenter(
            FullscreenTogglePolicy.ShouldExitFullscreen(currentlyFullscreen)
                ? AppWindowPresenterKind.Overlapped
                : AppWindowPresenterKind.FullScreen);
        StatusText.Text = FullscreenTogglePolicy.StatusAfterToggle(currentlyFullscreen);
    }

    /// <summary>One-shot cold-start timing from <see cref="App.OnLaunched"/> (F57-01).</summary>
    public void ReportStartupDuration(long elapsedMilliseconds)
    {
        StatusText.Text = StartupReadyStatus.Format(elapsedMilliseconds);
    }

    private bool TabHasUnsavedEdits(DocumentId id)
    {
        var tab = DocumentTabs.TabItems.OfType<TabViewItem>()
            .FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(id));
        return tab?.Content switch
        {
            PdfDocumentView pdf => pdf.HasUnsavedEdits,
            ImageDocumentView image => image.HasUnsavedEdits,
            _ => false,
        };
    }

    private async Task PersistSessionAsync()
    {
        try
        {
            if (!_settingsStore.Current.RestorePreviousSession)
            {
                await _sessionStore.ClearAsync();
                return;
            }

            var paths = _workspace.Documents
                .Select(d => d.Path)
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p!))
                .Cast<string>()
                .ToList();
            var active = _workspace.ActiveDocument?.Path;
            var activeIndex = 0;
            if (active is not null)
            {
                var idx = paths.FindIndex(p =>
                    string.Equals(p, active, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    activeIndex = idx;
                }
            }

            await _sessionStore.SaveAsync(new SessionState
            {
                Paths = paths,
                ActiveIndex = activeIndex,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist session");
        }
    }

    private async Task RestorePreviousSessionAsync()
    {
        if (!_settingsStore.Current.RestorePreviousSession)
        {
            return;
        }

        try
        {
            var state = await _sessionStore.TryLoadAsync();
            if (state is null || state.Paths.Count == 0)
            {
                return;
            }

            StatusText.Text = AppShellStatus.FormatRestoringTabs(state.Paths.Count);
            string? activePath = null;
            if (state.ActiveIndex >= 0 && state.ActiveIndex < state.Paths.Count)
            {
                activePath = state.Paths[state.ActiveIndex];
            }

            foreach (var path in state.Paths)
            {
                await OpenPathAsync(path);
            }

            if (activePath is not null)
            {
                var existing = _workspace.FindByPath(activePath);
                if (existing is not null)
                {
                    _workspace.Activate(existing.Id);
                    SelectTabForActiveDocument();
                }
            }

            StatusText.Text = AppShellStatus.FormatRestoredTabs(state.Paths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore previous session");
            StatusText.Text = AppShellStatus.SessionRestoreFailed;
        }
    }

    private async Task PromptForCrashRecoveryAsync()
    {
        try
        {
            var entries = await _recoveryStore.ListAsync();
            if (entries.Count == 0)
            {
                return;
            }

            var names = CrashRecoveryPromptUi.NamesSummary(
                entries.Select(e => e.DisplayName).ToList());

            var dialog = new ContentDialog
            {
                Title = CrashRecoveryPromptUi.Title,
                Content = CrashRecoveryPromptUi.Content(names),
                PrimaryButtonText = CrashRecoveryPromptUi.RecoverButton,
                SecondaryButtonText = CrashRecoveryPromptUi.KeepButton,
                CloseButtonText = CrashRecoveryPromptUi.DiscardButton,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                foreach (var entry in entries)
                {
                    if (File.Exists(entry.RecoveryPath))
                    {
                        await OpenPathAsync(entry.RecoveryPath);
                    }
                }

                StatusText.Text = CrashRecoveryPromptUi.OpenedStatus(entries.Count);
            }
            else if (result == ContentDialogResult.None)
            {
                await _recoveryStore.DiscardAllAsync();
                StatusText.Text = CrashRecoveryPromptUi.DiscardedStatus;
            }
            else
            {
                StatusText.Text = CrashRecoveryPromptUi.KeptStatus;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Crash recovery prompt failed");
        }
    }

    private void ConfigureRecoveryTimer()
    {
        var seconds = _settingsStore.Current.CrashRecoveryIntervalSeconds;
        if (_recoveryTimer is not null)
        {
            _recoveryTimer.Stop();
            _recoveryTimer.Tick -= RecoveryTimer_Tick;
            _recoveryTimer = null;
        }

        if (seconds <= 0)
        {
            return;
        }

        _recoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 15, 3600)),
        };
        _recoveryTimer.Tick += RecoveryTimer_Tick;
        _recoveryTimer.Start();
    }

    private async void RecoveryTimer_Tick(object? sender, object e)
    {
        if (_recoveryTickRunning)
        {
            return;
        }

        _recoveryTickRunning = true;
        try
        {
            await RunRecoveryAndAutosavePassAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recovery timer pass failed");
        }
        finally
        {
            _recoveryTickRunning = false;
        }
    }

    private async Task RunRecoveryAndAutosavePassAsync()
    {
        var autoSave = _settingsStore.Current.AutoSaveToOriginal;
        foreach (var session in _workspace.Documents.ToList())
        {
            if (string.IsNullOrWhiteSpace(session.Path) || !File.Exists(session.Path))
            {
                continue;
            }

            var tab = DocumentTabs.TabItems.OfType<TabViewItem>()
                .FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(session.Id));
            if (tab is null)
            {
                continue;
            }

            var needsRecovery = session.IsDirty || TabHasUnsavedEdits(session.Id);
            if (!needsRecovery && tab.Content is PdfDocumentView pdfProbe)
            {
                needsRecovery = await pdfProbe.DiffersFromDiskAsync();
                if (needsRecovery)
                {
                    session.MarkDirty();
                }
            }

            if (!needsRecovery)
            {
                continue;
            }

            if (autoSave && !session.IsReadOnly && !PathUtilities.IsPathReadOnly(session.Path))
            {
                if (ReferenceEquals(DocumentTabs.SelectedItem, tab))
                {
                    await SaveActiveDocumentAsync(saveAs: false);
                }
                else if (tab.Content is PdfDocumentView or ImageDocumentView)
                {
                    // Autosave only the active tab via the public save path; others get a recovery snapshot.
                    await WriteTabRecoveryAsync(tab, session.Path);
                }

                continue;
            }

            await WriteTabRecoveryAsync(tab, session.Path);
        }

        await PersistSessionAsync();
    }

    private async Task WriteTabRecoveryAsync(TabViewItem tab, string originalPath)
    {
        try
        {
            if (tab.Content is PdfDocumentView pdfView)
            {
                await pdfView.WriteRecoverySnapshotAsync(_recoveryStore, originalPath);
                return;
            }

            if (tab.Content is ImageDocumentView imageView)
            {
                await imageView.WriteRecoverySnapshotAsync(_recoveryStore, originalPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write recovery snapshot for {Path}", originalPath);
        }
    }

    private async Task DiscardRecoveryAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await _recoveryStore.DiscardAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discard recovery for {Path}", path);
        }
    }

    private async Task ShowPreferencesAsync()
    {
        var settings = _settingsStore.Current;
        var restoreBox = new CheckBox
        {
            Content = PreferencesDialogUi.RestoreTabs,
            IsChecked = settings.RestorePreviousSession,
        };
        var autoSaveBox = new CheckBox
        {
            Content = PreferencesDialogUi.AutoSaveToOriginal,
            IsChecked = settings.AutoSaveToOriginal,
        };
        var intervalBox = new NumberBox
        {
            Header = PreferencesDialogUi.CrashRecoveryIntervalHeader,
            Value = settings.CrashRecoveryIntervalSeconds,
            Minimum = 0,
            Maximum = 3600,
            SmallChange = 30,
            LargeChange = 60,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var recentBox = new NumberBox
        {
            Header = PreferencesDialogUi.RecentFileCapacityHeader,
            Value = settings.RecentFileCapacity,
            Minimum = 1,
            Maximum = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var snapshotsBox = new CheckBox
        {
            Content = PreferencesDialogUi.VersionSnapshots,
            IsChecked = settings.VersionSnapshotsEnabled,
        };
        var snapshotCapBox = new NumberBox
        {
            Header = PreferencesDialogUi.SnapshotCapacityHeader,
            Value = settings.VersionSnapshotCapacity,
            Minimum = 1,
            Maximum = 50,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var separateWindowsBox = new CheckBox
        {
            Content = PreferencesDialogUi.SeparateWindows,
            IsChecked = settings.OpenFilesInSeparateWindows,
        };
        var authorBox = new TextBox
        {
            Header = PreferencesDialogUi.AnnotationAuthorHeader,
            Text = settings.AnnotationAuthor,
            PlaceholderText = Environment.UserName,
            Width = 280,
        };
        var compactToolbarBox = new CheckBox
        {
            Content = PreferencesDialogUi.CompactToolbar,
            IsChecked = settings.CompactToolbar,
        };
        var toolbarHidden = new HashSet<string>(
            settings.ToolbarHiddenCommands ?? [],
            StringComparer.OrdinalIgnoreCase);
        var toolbarOrder = ToolbarOrderPolicy.Normalize(settings.ToolbarCommandOrder).ToList();
        var toolbarChecks = new List<CheckBox>();
        var toolbarPanel = new StackPanel { Spacing = 2 };
        var toolbarReset = new Button { Content = PreferencesDialogUi.ResetToolbar, Margin = new Thickness(0, 4, 0, 0) };

        void SyncHiddenFromChecks()
        {
            toolbarHidden.Clear();
            foreach (var box in toolbarChecks)
            {
                if (box.IsChecked != true && box.Tag is string id)
                {
                    toolbarHidden.Add(id);
                }
            }
        }

        void RebuildToolbarRows()
        {
            toolbarPanel.Children.Clear();
            toolbarPanel.Children.Add(new TextBlock
            {
                Text = PreferencesDialogUi.ToolbarCommandsHeader,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4),
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 360,
            });
            toolbarChecks.Clear();
            foreach (var id in toolbarOrder)
            {
                var label = ToolbarCommands.Catalog.First(c => c.Id == id).Label;
                var box = new CheckBox
                {
                    Content = label,
                    Tag = id,
                    IsChecked = !toolbarHidden.Contains(id),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 200,
                };
                var up = new Button
                {
                    Content = PreferencesDialogUi.MoveToolbarCommandUp,
                    Tag = id,
                    Width = 36,
                    Padding = new Thickness(4, 0, 4, 0),
                };
                var down = new Button
                {
                    Content = PreferencesDialogUi.MoveToolbarCommandDown,
                    Tag = id,
                    Width = 36,
                    Padding = new Thickness(4, 0, 4, 0),
                };
                up.Click += (_, _) =>
                {
                    SyncHiddenFromChecks();
                    toolbarOrder = ToolbarOrderPolicy.Move(toolbarOrder, id, -1).ToList();
                    RebuildToolbarRows();
                };
                down.Click += (_, _) =>
                {
                    SyncHiddenFromChecks();
                    toolbarOrder = ToolbarOrderPolicy.Move(toolbarOrder, id, 1).ToList();
                    RebuildToolbarRows();
                };
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Margin = new Thickness(0, 2, 0, 2),
                    Children = { box, up, down },
                };
                toolbarChecks.Add(box);
                toolbarPanel.Children.Add(row);
            }

            toolbarPanel.Children.Add(toolbarReset);
        }

        toolbarReset.Click += (_, _) =>
        {
            toolbarHidden.Clear();
            toolbarOrder = ToolbarOrderPolicy.Normalize(null).ToList();
            RebuildToolbarRows();
        };
        RebuildToolbarRows();

        var shortcutBoxes = new List<(string Command, TextBox Box)>();
        var shortcutPanel = new StackPanel { Spacing = 4 };
        shortcutPanel.Children.Add(new TextBlock
        {
            Text = PreferencesDialogUi.ShortcutsHeader,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4),
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 360,
        });
        foreach (var (command, defaultGesture) in ShortcutCustomizationPolicy.DefaultCatalog)
        {
            var box = new TextBox
            {
                Header = command,
                Text = ShortcutCustomizationPolicy.Resolve(command, settings.ShortcutOverrides),
                PlaceholderText = defaultGesture,
                Width = 280,
                Margin = new Thickness(0, 2, 0, 2),
            };
            shortcutBoxes.Add((command, box));
            shortcutPanel.Children.Add(box);
        }

        var shortcutReset = new Button
        {
            Content = PreferencesDialogUi.ResetShortcuts,
            Margin = new Thickness(0, 4, 0, 0),
        };
        shortcutReset.Click += (_, _) =>
        {
            foreach (var (command, box) in shortcutBoxes)
            {
                var def = ShortcutCustomizationPolicy.DefaultCatalog
                    .First(c => string.Equals(c.Command, command, StringComparison.OrdinalIgnoreCase))
                    .DefaultGesture;
                box.Text = def;
            }
        };
        shortcutPanel.Children.Add(shortcutReset);

        var highlightColorBox = new ComboBox
        {
            Header = PreferencesDialogUi.DefaultHighlightColorHeader,
            Width = 280,
            ItemsSource = PdfAnnotationColor.HighlightPresets.Select(p => p.Name).ToList(),
            SelectedItem = PdfAnnotationColor.HighlightPresets
                .Select(p => p.Name)
                .FirstOrDefault(n => string.Equals(n, settings.DefaultHighlightColor, StringComparison.OrdinalIgnoreCase))
                ?? "Yellow",
        };
        var strokeColorBox = new ComboBox
        {
            Header = PreferencesDialogUi.DefaultStrokeColorHeader,
            Width = 280,
            ItemsSource = PdfAnnotationColor.StrokePresets.Select(p => p.Name).ToList(),
            SelectedItem = PdfAnnotationColor.StrokePresets
                .Select(p => p.Name)
                .FirstOrDefault(n => string.Equals(n, settings.DefaultStrokeColor, StringComparison.OrdinalIgnoreCase))
                ?? "Red",
        };
        var stickyColorBox = new ComboBox
        {
            Header = PreferencesDialogUi.DefaultStickyColorHeader,
            Width = 280,
            ItemsSource = PdfAnnotationColor.StickyNotePresets.Select(p => p.Name).ToList(),
            SelectedItem = PdfAnnotationColor.StickyNotePresets
                .Select(p => p.Name)
                .FirstOrDefault(n => string.Equals(n, settings.DefaultStickyNoteColor, StringComparison.OrdinalIgnoreCase))
                ?? "Yellow",
        };
        var strokeWidthBox = new NumberBox
        {
            Header = PreferencesDialogUi.DefaultStrokeWidthHeader,
            Value = settings.DefaultStrokeWidthPoints,
            Minimum = 0.5,
            Maximum = 12,
            SmallChange = 0.5,
            LargeChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var animationAutoplayBox = new CheckBox
        {
            Content = PreferencesDialogUi.AnimationAutoplay,
            IsChecked = settings.AnimationAutoplay,
        };
        var stripMetadataBox = new CheckBox
        {
            Content = PreferencesDialogUi.StripMetadataByDefault,
            IsChecked = settings.StripMetadataByDefault,
        };
        var layoutNames = PreferencesDialogUi.PdfLayoutNames;
        var layoutBox = new ComboBox
        {
            Header = PreferencesDialogUi.DefaultPdfLayoutHeader,
            Width = 280,
            ItemsSource = PreferencesDialogUi.PdfLayoutLabels.ToList(),
            SelectedIndex = Math.Max(0, layoutNames.ToList().IndexOf(settings.DefaultPageLayout)),
        };
        var defaultZoomBox = new NumberBox
        {
            Header = PreferencesDialogUi.DefaultPdfZoomHeader,
            Value = settings.DefaultZoom,
            Minimum = 0.1,
            Maximum = 8,
            SmallChange = 0.25,
            LargeChange = 0.5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var rememberLastPageBox = new CheckBox
        {
            Content = PreferencesDialogUi.RememberLastPage,
            IsChecked = DocumentViewRestorePolicy.EffectiveRememberLastPage(settings.RememberLastPage),
        };
        var rememberZoomBox = new CheckBox
        {
            Content = PreferencesDialogUi.RememberZoom,
            IsChecked = DocumentViewRestorePolicy.EffectiveRememberZoom(settings.RememberZoom),
        };
        var zoom100Box = new ComboBox
        {
            Header = PreferencesDialogUi.ImageZoom100Header,
            Width = 280,
            ItemsSource = PreferencesDialogUi.ImageZoom100Labels.ToList(),
            SelectedIndex = PreferencesDialogUi.Zoom100Index(settings.Zoom100Meaning),
        };
        var interpolationBox = new ComboBox
        {
            Header = PreferencesDialogUi.DefaultInterpolationHeader,
            Width = 280,
            ItemsSource = PreferencesDialogUi.InterpolationLabels.ToList(),
            SelectedIndex = PreferencesDialogUi.InterpolationIndex(settings.DefaultInterpolation),
        };
        var colorManagedBox = new CheckBox
        {
            Content = PreferencesDialogUi.ColorManagedDisplay,
            IsChecked = settings.ColorManagedDisplayDefault,
        };
        var localOcrNote = new TextBlock
        {
            Text = PreferencesDialogUi.LocalOcrNote,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 360,
        };
        var ocrLanguageBox = new TextBox
        {
            Header = PreferencesDialogUi.OcrLanguageHeader,
            Text = settings.OcrLanguageTag,
            PlaceholderText = PreferencesDialogUi.OcrLanguagePlaceholder,
            Width = 280,
        };

        var clearRecentButton = new Button
        {
            Content = PreferencesDialogUi.ClearRecentFiles,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(clearRecentButton, PreferencesDialogUi.ClearRecentFiles);
        clearRecentButton.Click += async (_, _) =>
        {
            await _recentFiles.ClearAsync();
            RefreshRecentList();
            StatusText.Text = AppShellStatus.RecentFilesCleared;
        };

        var clearSignaturesButton = new Button
        {
            Content = PreferencesDialogUi.ClearSavedSignatures,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(clearSignaturesButton, PreferencesDialogUi.ClearSavedSignatures);
        clearSignaturesButton.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = PreferencesDialogUi.ClearSignaturesTitle,
                Content = PreferencesDialogUi.ClearSignaturesBody,
                PrimaryButtonText = PreferencesDialogUi.ClearButton,
                CloseButtonText = PreferencesDialogUi.CancelButton,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await _signatures.ClearAllAsync();
            StatusText.Text = AppShellStatus.SavedSignaturesCleared;
        };

        var checkUpdatesButton = new Button
        {
            Content = PreferencesDialogUi.CheckForUpdates,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(checkUpdatesButton, PreferencesDialogUi.CheckForUpdates);
        checkUpdatesButton.Click += async (_, _) => await ShowUpdateCheckAsync();

        var privacyHeader = new TextBlock
        {
            Text = PreferencesDialogUi.PrivacyHeader,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                restoreBox, autoSaveBox, intervalBox, recentBox, snapshotsBox, snapshotCapBox,
                separateWindowsBox, authorBox, compactToolbarBox, toolbarPanel, shortcutPanel,
                highlightColorBox, strokeColorBox, stickyColorBox, strokeWidthBox,
                animationAutoplayBox, stripMetadataBox, layoutBox, defaultZoomBox,
                rememberLastPageBox, rememberZoomBox,
                zoom100Box, interpolationBox, colorManagedBox, localOcrNote, ocrLanguageBox,
                privacyHeader, clearRecentButton, clearSignaturesButton, checkUpdatesButton,
            },
        };
        var dialog = new ContentDialog
        {
            Title = PreferencesDialogUi.DialogTitle,
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = PreferencesDialogUi.SaveButton,
            CloseButtonText = PreferencesDialogUi.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var shortcutDraft = shortcutBoxes.ToDictionary(
            x => x.Command,
            x => x.Box.Text?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        var shortcutError = ShortcutCustomizationPolicy.ValidationError(shortcutDraft);
        if (shortcutError is not null)
        {
            var err = new ContentDialog
            {
                Title = PreferencesDialogUi.DialogTitle,
                Content = shortcutError,
                CloseButtonText = PreferencesDialogUi.CancelButton,
                XamlRoot = RootGrid.XamlRoot,
            };
            await err.ShowAsync();
            return;
        }

        settings.RestorePreviousSession = restoreBox.IsChecked == true;
        settings.AutoSaveToOriginal = autoSaveBox.IsChecked == true;
        settings.CrashRecoveryIntervalSeconds = (int)Math.Clamp(intervalBox.Value, 0, 3600);
        settings.RecentFileCapacity = (int)Math.Clamp(recentBox.Value, 1, 100);
        settings.VersionSnapshotsEnabled = snapshotsBox.IsChecked == true;
        settings.VersionSnapshotCapacity = (int)Math.Clamp(snapshotCapBox.Value, 1, 50);
        settings.OpenFilesInSeparateWindows = separateWindowsBox.IsChecked == true;
        settings.AnnotationAuthor = authorBox.Text?.Trim() ?? string.Empty;
        settings.CompactToolbar = compactToolbarBox.IsChecked == true;
        settings.ToolbarHiddenCommands = toolbarChecks
            .Where(b => b.IsChecked != true && b.Tag is string id)
            .Select(b => (string)b.Tag!)
            .ToList();
        settings.ToolbarCommandOrder = ToolbarOrderPolicy.IsDefault(toolbarOrder)
            ? []
            : toolbarOrder.ToList();
        settings.ShortcutOverrides = ShortcutCustomizationPolicy.NormalizeOverrides(shortcutDraft);
        settings.DefaultHighlightColor = highlightColorBox.SelectedItem as string ?? "Yellow";
        settings.DefaultStrokeColor = strokeColorBox.SelectedItem as string ?? "Red";
        settings.DefaultStickyNoteColor = stickyColorBox.SelectedItem as string ?? "Yellow";
        settings.DefaultStrokeWidthPoints = Math.Clamp(strokeWidthBox.Value, 0.5, 12);
        settings.AnimationAutoplay = animationAutoplayBox.IsChecked == true;
        settings.StripMetadataByDefault = stripMetadataBox.IsChecked == true;
        settings.DefaultPageLayout = PreferencesDialogUi.PdfLayoutNames[Math.Clamp(layoutBox.SelectedIndex, 0, PreferencesDialogUi.PdfLayoutNames.Count - 1)];
        settings.DefaultZoom = Math.Clamp(defaultZoomBox.Value, 0.1, 8);
        settings.RememberLastPage = rememberLastPageBox.IsChecked == true;
        settings.RememberZoom = rememberZoomBox.IsChecked == true;
        settings.Zoom100Meaning = PreferencesDialogUi.Zoom100Setting(zoom100Box.SelectedIndex);
        settings.DefaultInterpolation = PreferencesDialogUi.InterpolationSetting(interpolationBox.SelectedIndex);
        settings.ColorManagedDisplayDefault = colorManagedBox.IsChecked == true;
        settings.LocalOnlyOcr = true;
        settings.OcrLanguageTag = ocrLanguageBox.Text?.Trim() ?? string.Empty;
        await _settingsStore.SaveAsync(settings);
        ApplyShellKeyboardShortcuts();
        ApplyToolbarCustomizationToOpenDocuments(settings);
        ConfigureRecoveryTimer();
        await PersistSessionAsync();
        StatusText.Text = AppShellStatus.PreferencesSaved;
    }

    private void ApplyToolbarCustomizationToOpenDocuments(AppSettings settings)
    {
        foreach (var tab in DocumentTabs.TabItems.OfType<TabViewItem>())
        {
            switch (tab.Content)
            {
                case PdfDocumentView pdfView:
                    pdfView.ApplyToolbarCustomization(settings);
                    break;
                case ImageDocumentView imageView:
                    imageView.ApplyToolbarCustomization(settings);
                    break;
            }
        }
    }

    private void ApplyShellKeyboardShortcuts()
    {
        var overrides = _settingsStore.Current.ShortcutOverrides;
        (string Command, MenuFlyoutItem Item)[] targets =
        [
            ("Open", OpenMenuItem),
            ("New from Clipboard", NewFromClipboardMenuItem),
            ("Save", SaveMenuItem),
            ("Save As", SaveAsMenuItem),
            ("Duplicate", DuplicateMenuItem),
            ("Properties", PropertiesMenuItem),
            ("New Window", NewWindowMenuItem),
            ("Close Tab", CloseTabMenuItem),
            ("Exit", ExitMenuItem),
            ("Paste", PasteMenuItem),
            ("Find in all open PDFs", FindAllPdfsMenuItem),
            ("Full Screen", FullscreenMenuItem),
            ("Toggle Sidebar", ToggleSidebarMenuItem),
            ("Toggle Toolbar", ToggleToolbarMenuItem),
            ("Preferences", PreferencesMenuItem),
            ("Next Tab", NextTabMenuItem),
            ("Previous Tab", PreviousTabMenuItem),
        ];

        foreach (var (command, item) in targets)
        {
            item.KeyboardAccelerators.Clear();
            var gesture = ShortcutCustomizationPolicy.Resolve(command, overrides);
            if (WinUiKeyboardGestures.TryCreateAccelerator(gesture, out var accelerator))
            {
                item.KeyboardAccelerators.Add(accelerator);
            }
        }
    }

    private async Task ShowVersionSnapshotsAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = AppShellStatus.OpenSavedForSnapshots;
            return;
        }

        var path = active.Path!;
        var entries = (await _snapshotStore.ListAsync(path)).ToList();
        if (entries.Count == 0)
        {
            StatusText.Text = _settingsStore.Current.VersionSnapshotsEnabled
                ? AppShellStatus.NoVersionSnapshotsYet
                : AppShellStatus.NoVersionSnapshotsDisabled;
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 460,
            MaxHeight = 280,
            ItemsSource = entries
                .Select(e => $"{e.SavedAtUtc.ToLocalTime():g} · {ByteSizeFormat.Format(e.ByteLength)}")
                .ToList(),
        };
        list.SelectedIndex = 0;

        string? action = null;
        var openCopy = new Button { Content = AppShellStatus.OpenAsCopyButton, Margin = new Thickness(0, 0, 8, 0) };
        var restore = new Button { Content = AppShellStatus.RestoreButton, Margin = new Thickness(0, 0, 8, 0) };
        var delete = new Button { Content = AppShellStatus.DeleteButton };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { openCopy, restore, delete },
        };
        var panel = new StackPanel { Children = { list, buttons } };
        var dialog = new ContentDialog
        {
            Title = AppShellStatus.FormatVersionSnapshotsTitle(active.DisplayName),
            Content = panel,
            CloseButtonText = DialogButtons.Close,
            XamlRoot = RootGrid.XamlRoot,
        };
        openCopy.Click += (_, _) =>
        {
            action = "copy";
            dialog.Hide();
        };
        restore.Click += (_, _) =>
        {
            action = "restore";
            dialog.Hide();
        };
        delete.Click += (_, _) =>
        {
            action = "delete";
            dialog.Hide();
        };

        await dialog.ShowAsync();
        if (action is null || list.SelectedIndex < 0 || list.SelectedIndex >= entries.Count)
        {
            return;
        }

        var chosen = entries[list.SelectedIndex];
        if (!File.Exists(chosen.SnapshotPath))
        {
            StatusText.Text = AppShellStatus.SnapshotMissing;
            return;
        }

        if (action == "copy")
        {
            var dir = System.IO.Path.GetDirectoryName(path) ?? System.IO.Path.GetTempPath();
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var ext = System.IO.Path.GetExtension(path);
            var stamp = chosen.SavedAtUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss");
            var copyPath = System.IO.Path.Combine(dir, $"{name} (snapshot {stamp}){ext}");
            var n = 2;
            while (File.Exists(copyPath))
            {
                copyPath = System.IO.Path.Combine(dir, $"{name} (snapshot {stamp}) {n}{ext}");
                n++;
            }

            File.Copy(chosen.SnapshotPath, copyPath);
            await OpenPathAsync(copyPath);
            StatusText.Text = AppShellStatus.FormatOpenedSnapshotCopy(System.IO.Path.GetFileName(copyPath));
            return;
        }

        if (action == "delete")
        {
            await _snapshotStore.DeleteAsync(chosen.Id, path);
            StatusText.Text = AppShellStatus.FormatDeletedSnapshot(chosen.SavedAtUtc.ToLocalTime().ToString("g"));
            return;
        }

        if (action == "restore")
        {
            var confirm = new ContentDialog
            {
                Title = AppShellStatus.RestoreSnapshotTitle,
                Content = AppShellStatus.RestoreSnapshotBody,
                PrimaryButtonText = AppShellStatus.RestoreButton,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            if (_settingsStore.Current.VersionSnapshotsEnabled)
            {
                await CaptureVersionSnapshotAsync(path);
            }

            if (active.IsDirty || TabHasUnsavedEdits(active.Id))
            {
                if (!await CloseDocumentAsync(active.Id, skipDirtyPrompt: true))
                {
                    return;
                }
            }
            else
            {
                await CloseDocumentAsync(active.Id, skipDirtyPrompt: true);
            }

            File.Copy(chosen.SnapshotPath, path, overwrite: true);
            await OpenPathAsync(path);
            StatusText.Text = AppShellStatus.FormatRestoredSnapshot(chosen.SavedAtUtc.ToLocalTime().ToString("g"));
        }
    }
}
