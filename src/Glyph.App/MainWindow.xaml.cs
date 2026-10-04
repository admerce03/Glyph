using Glyph.Core.Documents;
using Glyph.Core.IO;
using Glyph.Core.Workspace;
using Glyph.Infrastructure.RecentFiles;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Glyph.App;

public sealed partial class MainWindow : Window
{
    private readonly WorkspaceState _workspace;
    private readonly IRecentFilesStore _recentFiles;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        WorkspaceState workspace,
        IRecentFilesStore recentFiles,
        ILogger<MainWindow> logger)
    {
        _workspace = workspace;
        _recentFiles = recentFiles;
        _logger = logger;

        InitializeComponent();
        ResizeAndCenter(1180, 760);
        RootGrid.Loaded += RootGrid_Loaded;
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRecentList();
        UpdateEmptyState();
        StatusText.Text = "Ready — drop files or use Open";
        await Task.CompletedTask;
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".tif");
        picker.FileTypeFilter.Add(".tiff");
        picker.FileTypeFilter.Add(".webp");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await OpenPathAsync(file.Path);
        }
    }

    private async void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        await CloseActiveTabAsync();
    }

    private async void DocumentTabs_AddTabButtonClick(TabView sender, object args)
    {
        OpenButton_Click(sender, new RoutedEventArgs());
        await Task.CompletedTask;
    }

    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is DocumentId id)
        {
            CloseDocument(id);
        }

        await Task.CompletedTask;
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
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open in Glyph";
        }
    }

    private async void DropHost_Drop(object sender, DragEventArgs e)
    {
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
            var session = _workspace.Open(kind, displayName, path);

            var tab = new TabViewItem
            {
                Header = displayName,
                Tag = session.Id,
                IsClosable = true,
                Content = CreatePlaceholderContent(session),
            };

            DocumentTabs.TabItems.Add(tab);
            DocumentTabs.SelectedItem = tab;

            await _recentFiles.AddAsync(path);
            RefreshRecentList();
            UpdateEmptyState();

            StatusText.Text = kind switch
            {
                DocumentKind.Pdf => $"Opened PDF (viewer arrives in Milestone 2): {displayName}",
                DocumentKind.Image => $"Opened image (viewer arrives in Milestone 5): {displayName}",
                _ => $"Opened {displayName}",
            };

            _logger.LogInformation("Opened {Kind} document {Path}", kind, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open {Path}", path);
            StatusText.Text = "Failed to open file.";
        }
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
                        ? "PDF document session ready. Rendering lands in Milestone 2."
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
        if (DocumentTabs.SelectedItem is TabViewItem { Tag: DocumentId id })
        {
            CloseDocument(id);
        }

        await Task.CompletedTask;
    }

    private void CloseDocument(DocumentId id)
    {
        _workspace.Close(id);

        var tab = DocumentTabs.TabItems.OfType<TabViewItem>().FirstOrDefault(t => t.Tag is DocumentId d && d.Equals(id));
        if (tab is not null)
        {
            DocumentTabs.TabItems.Remove(tab);
        }

        UpdateEmptyState();
        StatusText.Text = _workspace.ActiveDocument is null ? "Ready" : $"Active: {_workspace.ActiveDocument.DisplayName}";
    }

    private void RefreshRecentList()
    {
        RecentList.ItemsSource = _recentFiles.GetRecent();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = DocumentTabs.TabItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

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
