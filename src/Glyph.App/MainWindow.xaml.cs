using Glyph.App.Views;
using Glyph.Core.Documents;
using Glyph.Core.IO;
using Glyph.Core.Workspace;
using Glyph.Imaging.Abstractions;
using Glyph.Infrastructure.Documents;
using Glyph.Infrastructure.RecentFiles;
using Glyph.Infrastructure.Settings;
using Glyph.Ocr.Abstractions;
using Glyph.Ocr.Pdf;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Text;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp",
    ];

    private readonly WorkspaceState _workspace;
    private readonly IRecentFilesStore _recentFiles;
    private readonly IDocumentViewStateStore _viewStateStore;
    private readonly ISettingsStore _settingsStore;
    private readonly IPdfDocumentFactory _pdfFactory;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IPdfTextSearchService _pdfSearch;
    private readonly IPdfTextExtractor _pdfText;
    private readonly IPdfOutlineService _pdfOutlines;
    private readonly IPdfLinkService _pdfLinks;
    private readonly IPdfPageEditor _pdfPageEditor;
    private readonly IPdfAnnotationStore _pdfAnnotations;
    private readonly IPdfMetadataService _pdfMetadata;
    private readonly IPdfSecurityService _pdfSecurity;
    private readonly IPdfRedactionService _pdfRedaction;
    private readonly IPdfOptimizationService _pdfOptimization;
    private readonly IImageDecoder _imageDecoder;
    private readonly IImageEncoder _imageEncoder;
    private readonly IImageProcessor _imageProcessor;
    private readonly IImageMetadataService _imageMetadata;
    private readonly IImageColorProfileService _imageColor;
    private readonly IImageBatchService _imageBatch;
    private readonly IScannerService _scanner;
    private readonly IOcrEngine _ocrEngine;
    private readonly PdfPageOcrService _pdfOcr;
    private readonly PageRenderCache _pageCache;
    private readonly ILogger<MainWindow> _logger;
    private readonly Dictionary<DocumentId, IAsyncDisposable> _openEngines = new();

    public MainWindow(
        WorkspaceState workspace,
        IRecentFilesStore recentFiles,
        IDocumentViewStateStore viewStateStore,
        ISettingsStore settingsStore,
        IPdfDocumentFactory pdfFactory,
        IPdfRenderer pdfRenderer,
        IPdfTextSearchService pdfSearch,
        IPdfTextExtractor pdfText,
        IPdfOutlineService pdfOutlines,
        IPdfLinkService pdfLinks,
        IPdfPageEditor pdfPageEditor,
        IPdfAnnotationStore pdfAnnotations,
        IPdfMetadataService pdfMetadata,
        IPdfSecurityService pdfSecurity,
        IPdfRedactionService pdfRedaction,
        IPdfOptimizationService pdfOptimization,
        IImageDecoder imageDecoder,
        IImageEncoder imageEncoder,
        IImageProcessor imageProcessor,
        IImageMetadataService imageMetadata,
        IImageColorProfileService imageColor,
        IImageBatchService imageBatch,
        IScannerService scanner,
        IOcrEngine ocrEngine,
        PdfPageOcrService pdfOcr,
        PageRenderCache pageCache,
        ILogger<MainWindow> logger)
    {
        _workspace = workspace;
        _recentFiles = recentFiles;
        _viewStateStore = viewStateStore;
        _settingsStore = settingsStore;
        _pdfFactory = pdfFactory;
        _pdfRenderer = pdfRenderer;
        _pdfSearch = pdfSearch;
        _pdfText = pdfText;
        _pdfOutlines = pdfOutlines;
        _pdfLinks = pdfLinks;
        _pdfPageEditor = pdfPageEditor;
        _pdfAnnotations = pdfAnnotations;
        _pdfMetadata = pdfMetadata;
        _pdfSecurity = pdfSecurity;
        _pdfRedaction = pdfRedaction;
        _pdfOptimization = pdfOptimization;
        _imageDecoder = imageDecoder;
        _imageEncoder = imageEncoder;
        _imageProcessor = imageProcessor;
        _imageMetadata = imageMetadata;
        _imageColor = imageColor;
        _imageBatch = imageBatch;
        _scanner = scanner;
        _ocrEngine = ocrEngine;
        _pdfOcr = pdfOcr;
        _pageCache = pageCache;
        _logger = logger;

        InitializeComponent();
        ResizeAndCenter(1180, 760);
        RootGrid.Loaded += RootGrid_Loaded;
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
        await Task.CompletedTask;
    }

    private async void OpenMenuItem_Click(object sender, RoutedEventArgs e) => await OpenWithPickerAsync(allowMultiple: false);

    private async void OpenMultipleMenuItem_Click(object sender, RoutedEventArgs e) => await OpenWithPickerAsync(allowMultiple: true);

    private async void NewFromClipboardMenuItem_Click(object sender, RoutedEventArgs e) => await NewFromClipboardAsync();

    private async void CloseTabMenuItem_Click(object sender, RoutedEventArgs e) => await CloseActiveTabAsync();

    private async void CloseAllMenuItem_Click(object sender, RoutedEventArgs e) => await CloseAllAsync();

    private async void ClearRecentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _recentFiles.ClearAsync();
        RefreshRecentList();
        StatusText.Text = "Recent files cleared.";
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private async void ToggleSidebarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var visible = SidebarBorder.Visibility != Visibility.Visible;
        ApplySidebarVisibility(visible);
        var settings = _settingsStore.Current;
        settings.SidebarVisible = visible;
        await _settingsStore.SaveAsync(settings);
    }

    private async void ThemeSystemItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.System);

    private async void ThemeLightItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.Light);

    private async void ThemeDarkItem_Click(object sender, RoutedEventArgs e) => await SetThemeAsync(ThemePreference.Dark);

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
                var tab = new TabViewItem
                {
                    Header = file.Name + "*",
                    Tag = session.Id,
                    IsClosable = true,
                    Content = CreatePlaceholderContent(session),
                };
                DocumentTabs.TabItems.Add(tab);
            }

            SelectTabForActiveDocument();
            UpdateEmptyState();
            StatusText.Text = $"Created image from clipboard: {file.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "New from Clipboard failed");
            StatusText.Text = "Could not create image from clipboard.";
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
                    Header = displayName,
                    Tag = session.Id,
                    IsClosable = true,
                    Content = content,
                };
                DocumentTabs.TabItems.Add(tab);
            }

            SelectTabForActiveDocument();
            await _recentFiles.AddAsync(path);
            RefreshRecentList();
            UpdateEmptyState();

            StatusText.Text = existing is null
                ? kind switch
                {
                    DocumentKind.Pdf => $"Opened PDF: {displayName}",
                    DocumentKind.Image => $"Opened image: {displayName}",
                    _ => $"Opened {displayName}",
                }
                : $"Activated {displayName}";

            _logger.LogInformation("Opened {Kind} document {Path}", kind, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open {Path}", path);
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
                _pdfFactory,
                _pdfOcr,
                session.ViewState,
                _pdfMetadata,
                _pdfSecurity,
                _pdfRedaction,
                _pdfOptimization);
        }

        if (session.Kind == DocumentKind.Image && session.Path is not null)
        {
            var image = await _imageDecoder.OpenAsync(session.Path);
            var saved = await _viewStateStore.TryLoadAsync(session.Path);
            if (saved is not null)
            {
                session.ViewState.Zoom = saved.Zoom;
            }

            _openEngines[session.Id] = image;
            SidebarStatus.Text = $"{image.FormatName} · {image.PixelWidth}×{image.PixelHeight}";
            return new ImageDocumentView(
                image,
                _imageProcessor,
                _imageEncoder,
                _ocrEngine,
                session.ViewState,
                _imageMetadata,
                _imageColor,
                _imageBatch,
                _scanner);
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
                        : "Image document session ready. Viewing/editing lands in Milestone 5.",
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

    private async Task<bool> CloseDocumentAsync(DocumentId id)
    {
        var session = _workspace.Documents.FirstOrDefault(d => d.Id.Equals(id));
        if (session is null)
        {
            return true;
        }

        if (session.IsDirty)
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
        ApplyThemePreference(preference);
        var settings = _settingsStore.Current;
        settings.Theme = preference;
        await _settingsStore.SaveAsync(settings);
        StatusText.Text = $"Theme: {preference}";
    }

    private void ApplySidebarVisibility(bool visible)
    {
        SidebarBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ContentGrid.ColumnDefinitions[0].Width = visible ? new GridLength(220) : new GridLength(0);
        ToggleSidebarMenuItem.Text = visible ? "Hide Sidebar" : "Show Sidebar";
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
}
