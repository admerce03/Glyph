using Glyph.App.Capture;
using Glyph.App.Scanning;
using Glyph.App.Sharing;
using Glyph.App.Views;
using Glyph.Core.Documents;
using Glyph.Core.IO;
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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Glyph.App;

public sealed partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions =
    [
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".ico",
        ".heic", ".heif", ".avif", ".jp2", ".j2k",
    ];

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
    private readonly IPdfLinkService _pdfLinks;
    private readonly IPdfPageEditor _pdfPageEditor;
    private readonly IPdfAnnotationService _pdfAnnotations;
    private readonly IPdfRedactionService _pdfRedaction;
    private readonly IPdfDocumentInfoService _pdfInfo;
    private readonly IPdfOptimizeService _pdfOptimize;
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
        IPdfLinkService pdfLinks,
        IPdfPageEditor pdfPageEditor,
        IPdfAnnotationService pdfAnnotations,
        IPdfRedactionService pdfRedaction,
        IPdfDocumentInfoService pdfInfo,
        IPdfOptimizeService pdfOptimize,
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
        _pdfLinks = pdfLinks;
        _pdfPageEditor = pdfPageEditor;
        _pdfAnnotations = pdfAnnotations;
        _pdfRedaction = pdfRedaction;
        _pdfInfo = pdfInfo;
        _pdfOptimize = pdfOptimize;
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
        RefreshRecentList();
        UpdateEmptyState();
        StatusText.Text = "Ready — File → Open or drop files here";
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

    private async void FindAllPdfsMenuItem_Click(object sender, RoutedEventArgs e) => await FindInAllOpenPdfsAsync();

    private async Task FindInAllOpenPdfsAsync()
    {
        var pdfs = _workspace.Documents
            .Where(d => d.Kind == DocumentKind.Pdf && !string.IsNullOrWhiteSpace(d.Path) && File.Exists(d.Path!))
            .ToList();
        if (pdfs.Count == 0)
        {
            StatusText.Text = "No open PDF documents to search.";
            return;
        }

        var box = new TextBox { PlaceholderText = "Search all open PDFs", Width = 360 };
        var dialog = new ContentDialog
        {
            Title = "Find in all open PDFs",
            Content = box,
            PrimaryButtonText = "Search",
            CloseButtonText = "Cancel",
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
            StatusText.Text = "Enter search text.";
            return;
        }

        StatusText.Text = $"Searching {pdfs.Count} PDF(s)…";
        var hits = new List<(DocumentSession Doc, PdfSearchHit Hit)>();
        foreach (var doc in pdfs)
        {
            try
            {
                var result = await _pdfSearch.SearchAsync(doc.Path!, query);
                if (result.Status == PdfSearchStatus.Cancelled)
                {
                    StatusText.Text = "Search cancelled.";
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
            StatusText.Text = "No matches in open PDFs.";
            return;
        }

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
            Title = $"{hits.Count} match(es) across open PDFs",
            Content = list,
            PrimaryButtonText = "Go to",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await results.ShowAsync() != ContentDialogResult.Primary
            || list.SelectedIndex < 0
            || list.SelectedIndex >= hits.Count)
        {
            StatusText.Text = $"{hits.Count} match(es) across open PDFs.";
            return;
        }

        var chosen = hits[list.SelectedIndex];
        _workspace.Activate(chosen.Doc.Id);
        SelectTabForActiveDocument();
        if (DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            await pdfView.RunExternalFindAsync(query, chosen.Hit.PageIndex);
        }

        StatusText.Text = $"Opened {chosen.Doc.DisplayName} p.{chosen.Hit.PageIndex + 1}.";
    }

    private async void CloseTabMenuItem_Click(object sender, RoutedEventArgs e) => await CloseActiveTabAsync();

    private async void CloseAllMenuItem_Click(object sender, RoutedEventArgs e) => await CloseAllAsync();

    private async void ClearRecentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _recentFiles.ClearAsync();
        RefreshRecentList();
        StatusText.Text = "Recent files cleared.";
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

    private void ToggleToolbarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            pdfView.ToggleToolbarVisibility();
            ToggleToolbarMenuItem.Text = pdfView.IsToolbarVisible ? "Hide Toolbar" : "Show Toolbar";
            return;
        }

        if (DocumentTabs.SelectedItem is TabViewItem { Content: ImageDocumentView imageView })
        {
            imageView.ToggleToolbarVisibility();
            ToggleToolbarMenuItem.Text = imageView.IsToolbarVisible ? "Hide Toolbar" : "Show Toolbar";
            return;
        }

        StatusText.Text = "Open a document to toggle the toolbar.";
    }

    private void FullscreenMenuItem_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private async void ThemeSystemItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.System);

    private async void ThemeLightItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.Light);

    private async void ThemeDarkItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.Dark);

    private async void PreferencesMenuItem_Click(object sender, RoutedEventArgs e) => await ShowPreferencesAsync();

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
            StatusText.Text = _workspace.ActiveDocument?.Path ?? _workspace.ActiveDocument?.DisplayName ?? "Ready";
        }

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

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open in Glyph";
        }
    }

    private async void DropHost_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // In-app page drags carry Glyph text payloads; those belong to thumbnail drop targets.
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            try
            {
                var text = await e.DataView.GetTextAsync();
                if (PageDragPayload.TryParse(text, out _))
                {
                    return;
                }
            }
            catch
            {
                // Fall through to storage-item open.
            }
        }

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        foreach (var item in items.OfType<StorageFile>())
        {
            await OpenPathAsync(item.Path);
        }
    }

    private async Task NewFromClipboardAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Bitmap))
            {
                StatusText.Text = "Clipboard does not contain an image.";
                return;
            }

            var bitmapRef = await content.GetBitmapAsync();
            using var stream = await bitmapRef.OpenReadAsync();
            var folder = await StorageFolder.GetFolderFromPathAsync(
                System.IO.Path.GetTempPath());
            var fileName = $"Clipboard-{DateTime.Now:yyyyMMdd-HHmmss}.png";
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
                var content = await CreateDocumentContentAsync(session);
                if (content is null)
                {
                    _workspace.Close(session.Id);
                    StatusText.Text = "Could not open clipboard image.";
                    return;
                }

                var tab = new TabViewItem
                {
                    Header = file.Name + "*",
                    Tag = session.Id,
                    IsClosable = true,
                    Content = content,
                };
                AttachTabContextFlyout(tab);
                DocumentTabs.TabItems.Add(tab);
            }

            SelectTabForActiveDocument();
            UpdateEmptyState();
            await PersistSessionAsync();
            StatusText.Text = $"Created image from clipboard: {file.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "New from Clipboard failed");
            StatusText.Text = "Could not create image from clipboard.";
        }
    }

    private async Task CaptureFromCameraAsync()
    {
        try
        {
            var root = Content?.XamlRoot;
            if (root is null)
            {
                StatusText.Text = "Camera UI unavailable.";
                return;
            }

            StatusText.Text = "Starting camera…";
            var captured = await WebcamCaptureHelper.CaptureAsync(
                root,
                title: "Capture from camera",
                hint: "Frame the document or photo, then Capture. Use Crop after open if needed.");
            if (captured is null)
            {
                StatusText.Text = "Camera capture cancelled or unavailable.";
                return;
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetTempPath());
            var fileName = $"Camera-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            var png = Glyph.Core.Signatures.SignaturePngEncoder.EncodeBgra(
                captured.BgraPixels,
                captured.Width,
                captured.Height);
            await FileIO.WriteBytesAsync(file, png);

            var offerCrop = new ContentDialog
            {
                Title = "Camera capture",
                Content = "Open the photo now. Use Crop… in the image toolbar if you want to trim it.",
                PrimaryButtonText = "Open",
                CloseButtonText = "Discard",
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

                StatusText.Text = "Camera capture discarded.";
                return;
            }

            await OpenPathAsync(file.Path);
            StatusText.Text = $"Opened camera capture: {file.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Camera capture failed");
            StatusText.Text = "Camera capture failed: " + ex.Message;
        }
    }

    private async Task ScanDocumentAsync()
    {
        try
        {
            StatusText.Text = "Looking for scanners…";
            var devices = await ScannerCaptureHelper.DiscoverAsync();
            if (devices.Count == 0)
            {
                StatusText.Text = "No scanners found.";
                var none = new ContentDialog
                {
                    Title = "No scanners",
                    Content = "Windows did not report any image scanners. Connect a scanner and try again.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot,
                };
                await none.ShowAsync();
                return;
            }

            var deviceBox = new ComboBox
            {
                Header = "Scanner",
                Width = 320,
                ItemsSource = devices.Select(d => d.Name).ToList(),
                SelectedIndex = 0,
            };
            var sourceBox = new ComboBox
            {
                Header = "Source",
                Width = 320,
                ItemsSource = new[] { "Flatbed", "Feeder (ADF)", "Auto" },
                SelectedIndex = 0,
            };
            var colorBox = new ComboBox
            {
                Header = "Color",
                Width = 320,
                ItemsSource = new[] { "Color", "Grayscale", "Black and white" },
                SelectedIndex = 0,
            };
            var dpiBox = new ComboBox
            {
                Header = "DPI",
                Width = 320,
                ItemsSource = new[] { "150", "200", "300", "600" },
                SelectedIndex = 2,
            };
            var duplex = new CheckBox { Content = "Duplex (feeder)" };
            var cropBox = new ComboBox
            {
                Header = "Auto crop",
                Width = 320,
                ItemsSource = new[]
                {
                    "Off",
                    "Single region",
                    "Multiple photos (flatbed)",
                },
                SelectedIndex = 1,
            };
            var straighten = new CheckBox { Content = "Straighten (deskew after scan)" };
            var paperBox = new ComboBox
            {
                Header = "Paper size",
                Width = 320,
                ItemsSource = new[]
                {
                    "Device default",
                    "Letter (8.5×11)",
                    "Legal (8.5×14)",
                    "A4",
                    "A5",
                    "A3",
                    "Tabloid (11×17)",
                    "Statement (5.5×8.5)",
                    "Auto-detect (feeder)",
                },
                SelectedIndex = 0,
            };
            var pagesBox = new NumberBox
            {
                Header = "Max pages (feeder)",
                Value = 1,
                Minimum = 1,
                Maximum = 50,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width = 160,
            };
            var destBox = new ComboBox
            {
                Header = "Destination",
                Width = 320,
                ItemsSource = new[]
                {
                    "Open as images",
                    "New PDF",
                    "Insert into current PDF",
                },
                SelectedIndex = 1,
            };
            var brightness = new Slider
            {
                Header = "Brightness (device, if supported)",
                Minimum = -1000,
                Maximum = 1000,
                Value = 0,
                Width = 320,
            };
            var contrast = new Slider
            {
                Header = "Contrast (device, if supported)",
                Minimum = -1000,
                Maximum = 1000,
                Value = 0,
                Width = 320,
            };

            var dialog = new ContentDialog
            {
                Title = "Scan",
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
                PrimaryButtonText = "Scan",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = "Scan cancelled.";
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

            StatusText.Text = $"Scanning with {device.Name}…";
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
                    Brightness: (int)brightness.Value == 0 ? null : (int)brightness.Value,
                    Contrast: (int)contrast.Value == 0 ? null : (int)contrast.Value,
                    MaxPages: (uint)Math.Clamp(pagesBox.Value, 1, 50),
                    PageSize: pageSize,
                    AutoDetectPageSize: autoDetectPaper));

            if (files.Count == 0)
            {
                StatusText.Text = "Scan produced no files.";
                return;
            }

            var paths = files.Select(f => f.Path).ToList();
            if (straighten.IsChecked == true)
            {
                StatusText.Text = $"Straightening {paths.Count} scan(s)…";
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

                    StatusText.Text = $"Opened {paths.Count} scanned image(s).";
                    break;
                case 2:
                    await InsertScansIntoActivePdfAsync(paths);
                    break;
                default:
                    var pdfPath = System.IO.Path.Combine(
                        sessionFolder.Path,
                        "Scan-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".pdf");
                    await _imageEncoder.WriteImagesAsPdfAsync(paths, pdfPath);
                    await OpenPathAsync(pdfPath);
                    StatusText.Text = $"Created PDF from {paths.Count} scan(s).";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            StatusText.Text = "Scan failed: " + ex.Message;
        }
    }

    private async Task InsertScansIntoActivePdfAsync(IReadOnlyList<string> imagePaths)
    {
        var active = _workspace.ActiveDocument;
        if (active is null || active.Kind != DocumentKind.Pdf)
        {
            StatusText.Text = "Open a PDF first to insert scanned pages.";
            return;
        }

        if (!_openEngines.TryGetValue(active.Id, out var engine) || engine is not IPdfDocument pdf)
        {
            StatusText.Text = "PDF engine unavailable for insert.";
            return;
        }

        var insertPdf = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "glyph-scan-insert-" + Guid.NewGuid().ToString("N") + ".pdf");
        await _imageEncoder.WriteImagesAsPdfAsync(imagePaths, insertPdf);
        await using var scanDoc = await _pdfFactory.OpenAsync(insertPdf);
        await _pdfPageEditor.InsertPagesAsync(
            pdf,
            scanDoc,
            Enumerable.Range(0, scanDoc.PageCount).ToList(),
            pdf.PageCount);
        active.MarkDirty();
        StatusText.Text = $"Inserted {imagePaths.Count} scanned page(s) into PDF.";
    }

    private void ShareActiveDocument()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path))
        {
            StatusText.Text = "Nothing to share — open a saved document.";
            return;
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _shareHelper ??= new DocumentShareHelper(hwnd);
            _shareHelper.ShowShareUi(active.DisplayName, active.Path);
            StatusText.Text = "Share UI opened.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Share failed: " + ex.Message;
        }
    }

    private async Task ShowActiveInExplorerAsync()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = "No file path for the active document.";
            return;
        }

        try
        {
            await DocumentShareHelper.OpenContainingFolderAsync(path);
            StatusText.Text = "Opened containing folder.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Show in Explorer failed: " + ex.Message;
        }
    }

    private async Task ShowActivePropertiesAsync()
    {
        if (DocumentTabs.SelectedItem is TabViewItem { Content: PdfDocumentView pdfView })
        {
            await pdfView.ShowPropertiesAsync();
            return;
        }

        if (DocumentTabs.SelectedItem is TabViewItem { Content: ImageDocumentView imageView })
        {
            await imageView.ShowPropertiesAsync();
            return;
        }

        StatusText.Text = "Open a document to view properties.";
    }

    private async Task SaveActiveDocumentAsync(bool saveAs)
    {
        var active = _workspace.ActiveDocument;
        if (active is null)
        {
            StatusText.Text = "Open a document to save.";
            return;
        }

        if (!saveAs
            && !string.IsNullOrWhiteSpace(active.Path)
            && (active.IsReadOnly || IsPathReadOnly(active.Path)))
        {
            var warn = new ContentDialog
            {
                Title = "Read-only file",
                Content = "This file is read-only. Use Save As… to write a writable copy, or remove the read-only attribute in Explorer.",
                PrimaryButtonText = "Save As…",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await warn.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = "Save cancelled — file is read-only.";
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

        StatusText.Text = "Open a document to save.";
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
        active.IsReadOnly = IsPathReadOnly(path);
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

        StatusText.Text = "Saved " + active.DisplayName
            + (active.IsReadOnly ? " · read-only" : string.Empty);
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
            StatusText.Text = "Save the document first to duplicate it.";
            return;
        }

        try
        {
            if (active.IsDirty)
            {
                var prompt = new ContentDialog
                {
                    Title = "Unsaved changes",
                    Content = "Duplicate copies the file on disk. Save first?",
                    PrimaryButtonText = "Save & duplicate",
                    SecondaryButtonText = "Duplicate without saving",
                    CloseButtonText = "Cancel",
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
            var dir = System.IO.Path.GetDirectoryName(source) ?? ".";
            var name = System.IO.Path.GetFileNameWithoutExtension(source);
            var ext = System.IO.Path.GetExtension(source);
            var candidate = System.IO.Path.Combine(dir, name + " copy" + ext);
            var n = 2;
            while (File.Exists(candidate))
            {
                candidate = System.IO.Path.Combine(dir, $"{name} copy {n}{ext}");
                n++;
            }

            File.Copy(source, candidate);
            await OpenPathAsync(candidate);
            StatusText.Text = "Duplicated as " + System.IO.Path.GetFileName(candidate);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Duplicate failed: " + ex.Message;
        }
    }

    private async Task RenameActiveDocumentAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = "Save the document first to rename it.";
            return;
        }

        try
        {
            var currentName = System.IO.Path.GetFileName(active.Path);
            var box = new TextBox
            {
                Header = "New file name",
                Text = currentName,
                Width = 320,
            };
            var dialog = new ContentDialog
            {
                Title = "Rename",
                Content = box,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var newName = (box.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newName)
                || newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0
                || newName.Contains('/')
                || newName.Contains('\\'))
            {
                StatusText.Text = "Invalid file name.";
                return;
            }

            if (string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
            {
                StatusText.Text = "Name unchanged.";
                return;
            }

            var dir = System.IO.Path.GetDirectoryName(active.Path!) ?? ".";
            var dest = System.IO.Path.Combine(dir, newName);
            if (File.Exists(dest))
            {
                StatusText.Text = "A file with that name already exists.";
                return;
            }

            if (active.IsDirty)
            {
                await SaveActiveDocumentAsync(saveAs: false);
            }

            File.Move(active.Path!, dest);
            RetargetActiveDocument(dest);
            StatusText.Text = "Renamed to " + newName;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Rename failed: " + ex.Message;
        }
    }

    private async Task MoveActiveDocumentAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = "Save the document first to move it.";
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
                StatusText.Text = "Move cancelled.";
                return;
            }

            var fileName = System.IO.Path.GetFileName(active.Path);
            var dest = System.IO.Path.Combine(folder.Path, fileName);
            if (string.Equals(
                    System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(active.Path!) ?? string.Empty),
                    System.IO.Path.GetFullPath(folder.Path),
                    StringComparison.OrdinalIgnoreCase))
            {
                StatusText.Text = "Already in that folder.";
                return;
            }

            if (File.Exists(dest))
            {
                var overwrite = new ContentDialog
                {
                    Title = "Replace existing file?",
                    Content = $"“{fileName}” already exists in the destination folder.",
                    PrimaryButtonText = "Replace",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot,
                };
                if (await overwrite.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                if (IsPathReadOnly(dest))
                {
                    StatusText.Text = "Destination file is read-only.";
                    return;
                }
            }

            if (active.IsDirty)
            {
                await SaveActiveDocumentAsync(saveAs: false);
            }

            File.Move(active.Path!, dest, overwrite: true);
            RetargetActiveDocument(dest);
            StatusText.Text = "Moved to " + folder.Path;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Move failed: " + ex.Message;
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
        active.IsReadOnly = IsPathReadOnly(newPath);
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

    private static bool IsPathReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            return File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
        }
        catch
        {
            return false;
        }
    }

    private void CopyActivePath()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = "No file path to copy.";
            return;
        }

        DocumentShareHelper.CopyPathToClipboard(path);
        StatusText.Text = "Copied path.";
    }

    private async Task CopyActiveFileAsync()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = "No file to copy.";
            return;
        }

        try
        {
            await DocumentShareHelper.CopyFileToClipboardAsync(path);
            StatusText.Text = "Copied file to clipboard.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Copy file failed: " + ex.Message;
        }
    }

    private async Task OpenActiveWithDefaultAsync()
    {
        var path = _workspace.ActiveDocument?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = "No file to open.";
            return;
        }

        try
        {
            await DocumentShareHelper.OpenWithDefaultAsync(path);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Open With failed: " + ex.Message;
        }
    }

    private async Task EmailActiveAsync()
    {
        var active = _workspace.ActiveDocument;
        try
        {
            await DocumentShareHelper.SendMailtoAsync(
                subject: active?.DisplayName ?? "Glyph document",
                body: "Shared from Glyph.",
                attachmentPath: active?.Path);
            StatusText.Text = "Mail client opened.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Email failed: " + ex.Message;
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
        foreach (var extension in SupportedExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
    }

    private async Task OpenPathAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                StatusText.Text = "File not found.";
                return;
            }

            if (!FileFormatDetector.IsSupported(path))
            {
                StatusText.Text = "Unsupported file type.";
                return;
            }

            var kind = FileFormatDetector.DetectKind(path);
            var displayName = System.IO.Path.GetFileName(path);
            var existing = _workspace.FindByPath(path);
            var session = _workspace.Open(kind, displayName, path);
            session.IsReadOnly = IsPathReadOnly(path);

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
                    DocumentKind.Pdf => $"Opened PDF: {displayName}",
                    DocumentKind.Image => $"Opened image: {displayName}",
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
            StatusText.Text = "Failed to open file.";
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
                StatusText.Text = "File not found.";
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
                StatusText.Text = "Failed to open image.";
                return;
            }

            tab.Header = displayName;
            tab.Content = content;
            await _recentFiles.AddAsync(path);
            RefreshRecentList();
            StatusText.Text = $"Opened image: {displayName}";
            _logger.LogInformation("Navigated image tab to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to image {Path}", path);
            StatusText.Text = "Failed to open file.";
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

            var saved = await _viewStateStore.TryLoadAsync(session.Path);
            if (saved is not null)
            {
                session.ViewState.Zoom = saved.Zoom;
                session.ViewState.PageLayout = saved.PageLayout;
                session.ViewState.CurrentPageIndex = Math.Clamp(
                    saved.CurrentPageIndex,
                    0,
                    Math.Max(0, pdf.PageCount - 1));
            }

            _openEngines[session.Id] = pdf;
            SidebarStatus.Text = $"{pdf.PageCount} pages — thumbnails and search in the document pane.";
            return new PdfDocumentView(
                pdf,
                _pdfRenderer,
                _pageCache,
                _pdfSearch,
                _pdfText,
                _pdfOutlines,
                _pdfLinks,
                _pdfPageEditor,
                _pdfAnnotations,
                _pdfRedaction,
                _pdfInfo,
                _pdfOptimize,
                _imageEncoder,
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
            var saved = await _viewStateStore.TryLoadAsync(session.Path);
            if (saved is not null && saved.Zoom > 0)
            {
                session.ViewState.Zoom = saved.Zoom;
            }

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

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var password = await PromptForPdfPasswordAsync(
                System.IO.Path.GetFileName(path),
                isRetry: attempt > 0);
            if (password is null)
            {
                StatusText.Text = "PDF open cancelled — password required.";
                return null;
            }

            try
            {
                return await _pdfFactory.OpenAsync(path, password);
            }
            catch (PdfPasswordRequiredException)
            {
                StatusText.Text = "Incorrect PDF password.";
            }
        }

        StatusText.Text = "Could not open password-protected PDF.";
        return null;
    }

    private async Task<string?> PromptForPdfPasswordAsync(string fileName, bool isRetry)
    {
        var box = new PasswordBox { Width = 280, PlaceholderText = "Password" };
        var dialog = new ContentDialog
        {
            Title = isRetry ? "Incorrect password" : "Password required",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Enter the password for “{fileName}”.",
                        TextWrapping = TextWrapping.WrapWholeWords,
                    },
                    box,
                },
            },
            PrimaryButtonText = "Open",
            CloseButtonText = "Cancel",
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
        var closeItem = new MenuFlyoutItem { Text = "Close Tab" };
        closeItem.Click += async (_, _) =>
        {
            if (tab.Tag is DocumentId id)
            {
                await CloseDocumentAsync(id);
            }
        };
        var tearItem = new MenuFlyoutItem { Text = "Move to New Window" };
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

        if (string.IsNullOrWhiteSpace(session.Path) || !File.Exists(session.Path))
        {
            StatusText.Text = "Save the document before moving it to a new window.";
            return;
        }

        var tab = DocumentTabs.TabItems.OfType<TabViewItem>()
            .FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(id));
        var pathToOpen = session.Path!;
        var dirty = session.IsDirty || TabHasUnsavedEdits(id);
        if (dirty)
        {
            var dialog = new ContentDialog
            {
                Title = "Unsaved changes",
                Content = $"“{session.DisplayName}” has unsaved changes. Save before moving to a new window?",
                PrimaryButtonText = "Save & move",
                SecondaryButtonText = "Move recovery copy",
                CloseButtonText = "Cancel",
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
                    StatusText.Text = "Save cancelled — tab not moved.";
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
                    StatusText.Text = "Could not create recovery copy for move.";
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
        StatusText.Text = "Moved tab to a new window.";
    }

    private async Task<bool> CloseDocumentAsync(DocumentId id, bool skipDirtyPrompt = false)
    {
        var session = _workspace.Documents.FirstOrDefault(d => d.Id.Equals(id));
        if (session is null)
        {
            return true;
        }

        if (!skipDirtyPrompt && (session.IsDirty || TabHasUnsavedEdits(id)))
        {
            var dialog = new ContentDialog
            {
                Title = "Unsaved changes",
                Content = $"“{session.DisplayName}” has unsaved changes. Close anyway?",
                PrimaryButtonText = "Close",
                CloseButtonText = "Cancel",
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
        StatusText.Text = _workspace.ActiveDocument is null ? "Ready" : $"Active: {_workspace.ActiveDocument.DisplayName}";
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
        StatusText.Text = $"Theme: {preference}";
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
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            StatusText.Text = "Exited fullscreen.";
        }
        else
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            StatusText.Text = "Fullscreen — press Fullscreen again or Esc via window chrome to exit.";
        }
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

            StatusText.Text = $"Restoring {state.Paths.Count} tab(s)…";
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

            StatusText.Text = $"Restored {state.Paths.Count} tab(s) from previous session.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore previous session");
            StatusText.Text = "Session restore failed.";
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

            var names = string.Join(", ", entries.Take(5).Select(e => e.DisplayName));
            if (entries.Count > 5)
            {
                names += $" (+{entries.Count - 5} more)";
            }

            var dialog = new ContentDialog
            {
                Title = "Recover unsaved work?",
                Content = $"Glyph found crash-recovery copies for: {names}.",
                PrimaryButtonText = "Recover",
                SecondaryButtonText = "Keep for later",
                CloseButtonText = "Discard",
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

                StatusText.Text = $"Opened {entries.Count} recovered document(s).";
            }
            else if (result == ContentDialogResult.None)
            {
                await _recoveryStore.DiscardAllAsync();
                StatusText.Text = "Discarded crash-recovery copies.";
            }
            else
            {
                StatusText.Text = "Crash-recovery copies kept on disk.";
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

            if (autoSave && !session.IsReadOnly && !IsPathReadOnly(session.Path))
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
            Content = "Restore previously open tabs on startup",
            IsChecked = settings.RestorePreviousSession,
        };
        var autoSaveBox = new CheckBox
        {
            Content = "Automatically save changes to the original file",
            IsChecked = settings.AutoSaveToOriginal,
        };
        var intervalBox = new NumberBox
        {
            Header = "Crash recovery interval (seconds, 0 = off)",
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
            Header = "Recent file list capacity",
            Value = settings.RecentFileCapacity,
            Minimum = 1,
            Maximum = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var snapshotsBox = new CheckBox
        {
            Content = "Keep local version snapshots on Save",
            IsChecked = settings.VersionSnapshotsEnabled,
        };
        var snapshotCapBox = new NumberBox
        {
            Header = "Snapshots kept per file",
            Value = settings.VersionSnapshotCapacity,
            Minimum = 1,
            Maximum = 50,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children = { restoreBox, autoSaveBox, intervalBox, recentBox, snapshotsBox, snapshotCapBox },
        };
        var dialog = new ContentDialog
        {
            Title = "Preferences",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        settings.RestorePreviousSession = restoreBox.IsChecked == true;
        settings.AutoSaveToOriginal = autoSaveBox.IsChecked == true;
        settings.CrashRecoveryIntervalSeconds = (int)Math.Clamp(intervalBox.Value, 0, 3600);
        settings.RecentFileCapacity = (int)Math.Clamp(recentBox.Value, 1, 100);
        settings.VersionSnapshotsEnabled = snapshotsBox.IsChecked == true;
        settings.VersionSnapshotCapacity = (int)Math.Clamp(snapshotCapBox.Value, 1, 50);
        await _settingsStore.SaveAsync(settings);
        ConfigureRecoveryTimer();
        await PersistSessionAsync();
        StatusText.Text = "Preferences saved.";
    }

    private async Task ShowVersionSnapshotsAsync()
    {
        var active = _workspace.ActiveDocument;
        if (active is null || string.IsNullOrWhiteSpace(active.Path) || !File.Exists(active.Path))
        {
            StatusText.Text = "Open a saved document to manage version snapshots.";
            return;
        }

        var path = active.Path!;
        var entries = (await _snapshotStore.ListAsync(path)).ToList();
        if (entries.Count == 0)
        {
            StatusText.Text = _settingsStore.Current.VersionSnapshotsEnabled
                ? "No version snapshots yet — they are created on Save."
                : "No snapshots. Enable “Keep local version snapshots on Save” in Preferences.";
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 460,
            MaxHeight = 280,
            ItemsSource = entries
                .Select(e => $"{e.SavedAtUtc.ToLocalTime():g} · {FormatBytes(e.ByteLength)}")
                .ToList(),
        };
        list.SelectedIndex = 0;

        string? action = null;
        var openCopy = new Button { Content = "Open as copy", Margin = new Thickness(0, 0, 8, 0) };
        var restore = new Button { Content = "Restore", Margin = new Thickness(0, 0, 8, 0) };
        var delete = new Button { Content = "Delete" };
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
            Title = "Version snapshots — " + active.DisplayName,
            Content = panel,
            CloseButtonText = "Close",
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
            StatusText.Text = "Snapshot file is missing.";
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
            StatusText.Text = "Opened snapshot copy: " + System.IO.Path.GetFileName(copyPath);
            return;
        }

        if (action == "delete")
        {
            await _snapshotStore.DeleteAsync(chosen.Id, path);
            StatusText.Text = "Deleted snapshot from " + chosen.SavedAtUtc.ToLocalTime().ToString("g");
            return;
        }

        if (action == "restore")
        {
            var confirm = new ContentDialog
            {
                Title = "Restore snapshot?",
                Content = "Replace the current file on disk with this snapshot? A new snapshot of the current file will be kept first when snapshots are enabled.",
                PrimaryButtonText = "Restore",
                CloseButtonText = "Cancel",
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
            StatusText.Text = "Restored snapshot from " + chosen.SavedAtUtc.ToLocalTime().ToString("g");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }
}
