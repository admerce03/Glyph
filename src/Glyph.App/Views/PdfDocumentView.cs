using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Glyph.App.Views;

/// <summary>
/// Milestone 2 PDF viewer: layout modes, zoom, page nav, bitmap thumbnails, Find.
/// Visible pages render on demand; distant bitmaps stay outside the LRU cache.
/// </summary>
public sealed class PdfDocumentView : UserControl
{
    private const double ThumbnailWidth = 108;

    private readonly IPdfDocument _document;
    private readonly IPdfRenderer _renderer;
    private readonly PageRenderCache _cache;
    private readonly PdfSearchCoordinator _searchCoordinator;
    private readonly IPdfTextExtractor _textExtractor;
    private readonly IPdfOutlineService _outlineService;
    private readonly IPdfLinkService _linkService;
    private readonly DocumentViewState _viewState;
    private readonly DocumentNavigationHistory _history = new();
    private readonly string _documentKey;
    private readonly string _thumbnailKey;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _continuousHost;
    private readonly StackPanel _spreadHost;
    private readonly StackPanel _thumbnailHost;
    private readonly ScrollViewer _thumbnailScroll;
    private readonly TreeView _outlineTree;
    private readonly ListView _searchResults;
    private readonly TextBox _searchBox;
    private readonly TextBox _gotoBox;
    private readonly CheckBox _caseSensitiveBox;
    private readonly ComboBox _layoutBox;
    private readonly TextBlock _status;
    private readonly Dictionary<int, IReadOnlyList<PdfTextChar>> _pageChars = new();
    private readonly Dictionary<int, IReadOnlyList<PdfLink>> _pageLinks = new();
    private string _selectedText = string.Empty;
    private readonly Dictionary<int, Image> _pageImages = new();
    private readonly Dictionary<int, Canvas> _pageOverlays = new();
    private readonly Dictionary<int, Image> _thumbnailImages = new();
    private readonly Dictionary<int, Border> _thumbnailBorders = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private double _scale = 1.25;
    private PageLayoutMode _layoutMode = PageLayoutMode.Continuous;
    private int _renderGeneration;
    private bool _loaded;
    private bool _suppressThumbnailNav;
    private IReadOnlyList<PdfSearchHit> _hits = [];
    private int _activeHitIndex = -1;
    private bool _dragSelecting;
    private Windows.Foundation.Point _dragStart;
    private int _dragPageIndex = -1;
    private string _searchQuery = string.Empty;
    private bool _searchCaseSensitive;

    public PdfDocumentView(
        IPdfDocument document,
        IPdfRenderer renderer,
        PageRenderCache cache,
        IPdfTextSearchService searchService,
        IPdfTextExtractor textExtractor,
        IPdfOutlineService outlineService,
        IPdfLinkService linkService,
        DocumentViewState? viewState = null)
    {
        _document = document;
        _renderer = renderer;
        _cache = cache;
        _searchCoordinator = new PdfSearchCoordinator(searchService);
        _textExtractor = textExtractor;
        _outlineService = outlineService;
        _linkService = linkService;
        _viewState = viewState ?? new DocumentViewState();
        _scale = PdfZoomCalculator.Clamp(_viewState.Zoom <= 0 ? 1.25 : _viewState.Zoom);
        _layoutMode = _viewState.PageLayout;
        CurrentPageIndex = Math.Clamp(_viewState.CurrentPageIndex, 0, Math.Max(0, document.PageCount - 1));
        _documentKey = document.Path ?? document.GetHashCode().ToString("X");
        _thumbnailKey = _documentKey + "|thumb";

        _continuousHost = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
        _spreadHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _scrollViewer = new ScrollViewer
        {
            Content = _continuousHost,
            ZoomMode = ZoomMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        _scrollViewer.PointerWheelChanged += ScrollViewer_PointerWheelChanged;
        // Precision-touchpad pinch often arrives as Ctrl+wheel; Manipulation Scale covers direct pinch.
        _scrollViewer.ManipulationMode = ManipulationModes.Scale;
        _scrollViewer.ManipulationDelta += ScrollViewer_ManipulationDelta;

        _thumbnailHost = new StackPanel { Spacing = 8, Padding = new Thickness(8) };
        _thumbnailScroll = new ScrollViewer
        {
            Content = _thumbnailHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _outlineTree = new TreeView { SelectionMode = TreeViewSelectionMode.Single };
        _outlineTree.ItemInvoked += OutlineTree_ItemInvoked;

        _searchBox = new TextBox { PlaceholderText = "Find in document", Width = 160 };
        _searchBox.KeyDown += SearchBox_KeyDown;
        _caseSensitiveBox = new CheckBox { Content = "Aa", VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(_caseSensitiveBox, "Match case");
        var searchButton = new Button { Content = "Find" };
        searchButton.Click += async (_, _) => await RunSearchAsync();
        var prevMatch = new Button { Content = "◁" };
        var nextMatch = new Button { Content = "▷" };
        ToolTipService.SetToolTip(prevMatch, "Previous match");
        ToolTipService.SetToolTip(nextMatch, "Next match");
        prevMatch.Click += async (_, _) => await GoToHitAsync(_activeHitIndex - 1);
        nextMatch.Click += async (_, _) => await GoToHitAsync(_activeHitIndex + 1);
        _searchResults = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Height = 160,
        };
        _searchResults.SelectionChanged += SearchResults_SelectionChanged;

        var sidePanel = new Grid
        {
            Width = 180,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(120) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(140) },
            },
        };
        sidePanel.Children.Add(new TextBlock { Text = "Pages", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(8, 8, 8, 4) });
        Grid.SetRow(_thumbnailScroll, 1);
        sidePanel.Children.Add(_thumbnailScroll);
        var tocHeader = new TextBlock { Text = "Contents", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(8, 8, 8, 4) };
        Grid.SetRow(tocHeader, 2);
        sidePanel.Children.Add(tocHeader);
        Grid.SetRow(_outlineTree, 3);
        sidePanel.Children.Add(_outlineTree);
        var searchHeader = new TextBlock { Text = "Search", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(8, 8, 8, 4) };
        Grid.SetRow(searchHeader, 4);
        sidePanel.Children.Add(searchHeader);
        Grid.SetRow(_searchResults, 5);
        sidePanel.Children.Add(_searchResults);

        _status = new TextBlock { Opacity = 0.75, FontSize = 12, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        _gotoBox = new TextBox { PlaceholderText = "#", Width = 48 };
        _gotoBox.KeyDown += GotoBox_KeyDown;
        _layoutBox = new ComboBox
        {
            Width = 150,
            ItemsSource = new[] { "Continuous", "Single", "Two-page", "Two-page + cover" },
            SelectedIndex = LayoutToComboIndex(_layoutMode),
        };
        _layoutBox.SelectionChanged += async (_, _) => await SetLayoutModeAsync(SelectedLayout());

        var first = new Button { Content = "First" };
        var prev = new Button { Content = "Prev" };
        var next = new Button { Content = "Next" };
        var last = new Button { Content = "Last" };
        var back = new Button { Content = "Back" };
        var forward = new Button { Content = "Fwd" };
        var zoomOut = new Button { Content = "−", Width = 36 };
        var zoomIn = new Button { Content = "+", Width = 36 };
        var fitWidth = new Button { Content = "Fit width" };
        var fitPage = new Button { Content = "Fit page" };
        var actual = new Button { Content = "100%" };
        var copy = new Button { Content = "Copy" };
        ToolTipService.SetToolTip(copy, "Copy selected text, or the current page text if nothing is selected");

        first.Click += async (_, _) => await GoToPageAsync(0, recordHistory: true);
        last.Click += async (_, _) => await GoToPageAsync(_document.PageCount - 1, recordHistory: true);
        prev.Click += async (_, _) => await GoToPageAsync(
            PageLayoutCalculator.PreviousPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount),
            recordHistory: true);
        next.Click += async (_, _) => await GoToPageAsync(
            PageLayoutCalculator.NextPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount),
            recordHistory: true);
        back.Click += async (_, _) =>
        {
            if (_history.GoBack() is int page)
            {
                await GoToPageAsync(page, recordHistory: false);
            }
        };
        forward.Click += async (_, _) =>
        {
            if (_history.GoForward() is int page)
            {
                await GoToPageAsync(page, recordHistory: false);
            }
        };
        zoomOut.Click += async (_, _) => await SetScaleAsync(PdfZoomCalculator.ZoomOut(_scale));
        zoomIn.Click += async (_, _) => await SetScaleAsync(PdfZoomCalculator.ZoomIn(_scale));
        fitWidth.Click += async (_, _) => await FitWidthAsync();
        fitPage.Click += async (_, _) => await FitPageAsync();
        actual.Click += async (_, _) => await SetScaleAsync(PdfZoomCalculator.ActualSize());
        copy.Click += async (_, _) => await CopyTextAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Thickness(8),
            Children =
            {
                first, prev, _gotoBox, next, last, back, forward,
                zoomOut, zoomIn, fitWidth, fitPage, actual, _layoutBox, copy,
                _searchBox, _caseSensitiveBox, searchButton, prevMatch, nextMatch, _status,
            },
        };

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(170) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        body.Children.Add(sidePanel);
        Grid.SetColumn(_scrollViewer, 1);
        body.Children.Add(_scrollViewer);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        root.Children.Add(toolbar);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Content = root;

        KeyDown += PdfDocumentView_KeyDown;
        Loaded += PdfDocumentView_Loaded;
        Unloaded += PdfDocumentView_Unloaded;
        _history.NavigateTo(CurrentPageIndex);
    }

    public int CurrentPageIndex { get; private set; }

    private async void PdfDocumentView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        BuildPagePlaceholders();
        BuildThumbnailPlaceholders();
        SyncViewState();
        UpdateStatus();
        HighlightThumbnail(CurrentPageIndex);
        await RenderVisibleAsync();
        _ = RenderThumbnailsAsync();
        _ = LoadOutlineAsync();
    }

    private void PdfDocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        _searchCoordinator.Cancel();
        _cache.ClearDocument(_documentKey);
        _cache.ClearDocument(_thumbnailKey);
    }

    private PageLayoutMode SelectedLayout() => _layoutBox.SelectedIndex switch
    {
        1 => PageLayoutMode.SinglePage,
        2 => PageLayoutMode.TwoPage,
        3 => PageLayoutMode.TwoPageWithCover,
        _ => PageLayoutMode.Continuous,
    };

    private static int LayoutToComboIndex(PageLayoutMode mode) => mode switch
    {
        PageLayoutMode.SinglePage => 1,
        PageLayoutMode.TwoPage => 2,
        PageLayoutMode.TwoPageWithCover => 3,
        _ => 0,
    };

    private async Task SetLayoutModeAsync(PageLayoutMode mode)
    {
        _layoutMode = mode;
        if (_layoutBox.SelectedIndex != LayoutToComboIndex(mode))
        {
            _layoutBox.SelectedIndex = LayoutToComboIndex(mode);
        }

        CurrentPageIndex = PageLayoutCalculator.NormalizePageIndex(mode, CurrentPageIndex, _document.PageCount);
        _scrollViewer.Content = mode == PageLayoutMode.Continuous ? _continuousHost : _spreadHost;
        BuildPagePlaceholders();
        SyncViewState();
        UpdateStatus();
        HighlightThumbnail(CurrentPageIndex);
        await RenderVisibleAsync();
    }

    private void BuildPagePlaceholders()
    {
        _continuousHost.Children.Clear();
        _spreadHost.Children.Clear();
        _pageImages.Clear();
        _pageOverlays.Clear();

        var host = _layoutMode == PageLayoutMode.Continuous ? _continuousHost : _spreadHost;
        var (first, last) = PageLayoutCalculator.VisibleRange(_layoutMode, CurrentPageIndex, _document.PageCount);
        if (last < first)
        {
            return;
        }

        for (var i = first; i <= last; i++)
        {
            var page = _document.GetPage(i);
            var width = Math.Max(1, page.WidthPoints * _scale);
            var height = Math.Max(1, page.HeightPoints * _scale);
            var image = new Image
            {
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
            };
            _pageImages[i] = image;

            var overlay = new Canvas
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false,
            };
            _pageOverlays[i] = overlay;

            var layer = new Grid { Width = width, Height = height };
            layer.Children.Add(image);
            layer.Children.Add(overlay);

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Child = layer,
                Tag = i,
                Background = new SolidColorBrush(Colors.Transparent),
            };
            border.PointerPressed += PageBorder_PointerPressed;
            border.PointerMoved += PageBorder_PointerMoved;
            border.PointerReleased += PageBorder_PointerReleased;
            border.PointerCaptureLost += (_, _) => _dragSelecting = false;
            host.Children.Add(border);
        }

        _ = RefreshSearchHighlightsAsync();
    }

    private void BuildThumbnailPlaceholders()
    {
        _thumbnailHost.Children.Clear();
        _thumbnailImages.Clear();
        _thumbnailBorders.Clear();

        for (var i = 0; i < _document.PageCount; i++)
        {
            var page = _document.GetPage(i);
            var thumbScale = ThumbnailWidth / Math.Max(1, page.WidthPoints);
            var image = new Image
            {
                Width = ThumbnailWidth,
                Height = Math.Max(1, page.HeightPoints * thumbScale),
                Stretch = Stretch.Uniform,
            };
            _thumbnailImages[i] = image;

            var label = new TextBlock
            {
                Text = $"{i + 1}",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.75,
            };

            var stack = new StackPanel { Spacing = 2, Children = { image, label } };
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(2),
                Child = stack,
                Tag = i,
            };
            border.PointerPressed += Thumbnail_PointerPressed;
            _thumbnailBorders[i] = border;
            _thumbnailHost.Children.Add(border);
        }
    }

    private async void Thumbnail_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border { Tag: int index })
        {
            await GoToPageAsync(index, recordHistory: true);
        }
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await RunSearchAsync();
        }
    }

    private async void GotoBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (int.TryParse(_gotoBox.Text, out var pageNumber))
        {
            await GoToPageAsync(pageNumber - 1, recordHistory: true);
        }
    }

    private async void PdfDocumentView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrlDown = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrlDown && e.Key == VirtualKey.C)
        {
            await CopyTextAsync();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.PageDown:
                await GoToPageAsync(
                    PageLayoutCalculator.NextPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount),
                    recordHistory: true);
                e.Handled = true;
                break;
            case VirtualKey.PageUp:
                await GoToPageAsync(
                    PageLayoutCalculator.PreviousPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount),
                    recordHistory: true);
                e.Handled = true;
                break;
            case VirtualKey.Home:
                await GoToPageAsync(0, recordHistory: true);
                e.Handled = true;
                break;
            case VirtualKey.End:
                await GoToPageAsync(_document.PageCount - 1, recordHistory: true);
                e.Handled = true;
                break;
        }
    }

    private async Task LoadOutlineAsync()
    {
        try
        {
            var nodes = await _outlineService.GetOutlineAsync(_document);
            var roots = new List<TreeViewNode>();
            foreach (var node in nodes)
            {
                roots.Add(ToTreeNode(node));
            }

            _outlineTree.RootNodes.Clear();
            foreach (var root in roots)
            {
                _outlineTree.RootNodes.Add(root);
            }
        }
        catch
        {
            // Outline is optional; viewing must continue without it.
        }
    }

    private static TreeViewNode ToTreeNode(PdfOutlineNode node)
    {
        var tree = new TreeViewNode
        {
            Content = new OutlineItem(node.Title, node.DestinationPageIndex),
            IsExpanded = true,
        };
        foreach (var child in node.Children)
        {
            tree.Children.Add(ToTreeNode(child));
        }

        return tree;
    }

    private async void OutlineTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        OutlineItem? item = args.InvokedItem switch
        {
            OutlineItem direct => direct,
            TreeViewNode { Content: OutlineItem nested } => nested,
            _ => null,
        };

        if (item?.PageIndex is int page)
        {
            await GoToPageAsync(page, recordHistory: true);
        }
    }

    private async Task CopyTextAsync()
    {
        if (string.IsNullOrEmpty(_selectedText))
        {
            _selectedText = await _textExtractor.GetTextAsync(_document, CurrentPageIndex);
        }

        if (string.IsNullOrEmpty(_selectedText))
        {
            _status.Text = "No extractable text to copy.";
            return;
        }

        var package = new DataPackage();
        package.SetText(_selectedText);
        Clipboard.SetContent(package);
        _status.Text = $"Copied {_selectedText.Length} characters.";
    }

    private void PageBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int pageIndex } border)
        {
            return;
        }

        _dragSelecting = true;
        _dragPageIndex = pageIndex;
        _dragStart = e.GetCurrentPoint(border).Position;
        border.CapturePointer(e.Pointer);
    }

    private void PageBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragSelecting || sender is not Border { Tag: int pageIndex } border || pageIndex != _dragPageIndex)
        {
            return;
        }

        if (!_pageOverlays.TryGetValue(pageIndex, out var overlay))
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        overlay.Children.Clear();
        var left = Math.Min(_dragStart.X, current.X);
        var top = Math.Min(_dragStart.Y, current.Y);
        var width = Math.Abs(current.X - _dragStart.X);
        var height = Math.Abs(current.Y - _dragStart.Y);
        if (width < 2 || height < 2)
        {
            return;
        }

        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 30, 144, 255)),
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        overlay.Children.Add(rect);
    }

    private async void PageBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int pageIndex } border)
        {
            return;
        }

        var point = e.GetCurrentPoint(border);
        var page = _document.GetPage(pageIndex);
        var wasDragging = _dragSelecting;
        var dragStart = _dragStart;
        _dragSelecting = false;
        border.ReleasePointerCapture(e.Pointer);

        var pdfX = point.Position.X / _scale;
        var pdfY = page.HeightPoints - (point.Position.Y / _scale);

        if (!_pageLinks.ContainsKey(pageIndex))
        {
            _pageLinks[pageIndex] = await _linkService.GetPageLinksAsync(_document, pageIndex);
        }

        var dragDistance = Math.Abs(point.Position.X - dragStart.X) + Math.Abs(point.Position.Y - dragStart.Y);
        if (!wasDragging || dragDistance < 4)
        {
            var link = _pageLinks[pageIndex].FirstOrDefault(l => l.Bounds.ContainsPoint(pdfX, pdfY));
            if (link?.DestinationPageIndex is int dest)
            {
                await GoToPageAsync(dest, recordHistory: true);
                _status.Text = $"Followed link to page {dest + 1}.";
                return;
            }
        }

        if (!_pageChars.ContainsKey(pageIndex))
        {
            _pageChars[pageIndex] = await _textExtractor.GetCharsAsync(_document, pageIndex);
        }

        var chars = _pageChars[pageIndex];
        if (wasDragging && dragDistance >= 4)
        {
            var left = Math.Min(dragStart.X, point.Position.X) / _scale;
            var right = Math.Max(dragStart.X, point.Position.X) / _scale;
            var topUi = Math.Min(dragStart.Y, point.Position.Y);
            var bottomUi = Math.Max(dragStart.Y, point.Position.Y);
            var top = page.HeightPoints - (bottomUi / _scale);
            var bottom = page.HeightPoints - (topUi / _scale);
            var selection = new PdfRect(left, bottom, right, top);
            _selectedText = PdfTextSelection.CopyCharsInRect(chars, selection);
            await RefreshSearchHighlightsAsync();
            DrawSelectionOverlay(pageIndex, chars, selection);
            _status.Text = string.IsNullOrEmpty(_selectedText)
                ? "No text in selection."
                : $"Selected “{TrimForStatus(_selectedText)}”";
            return;
        }

        // Click selects nearest character word-ish: expand to nearby chars on the same line.
        var hit = chars
            .Select((c, idx) => (c, idx, dist: Math.Abs(c.Bounds.Left - pdfX) + Math.Abs(c.Bounds.Bottom - pdfY)))
            .OrderBy(x => x.dist)
            .FirstOrDefault();
        if (hit.c is null)
        {
            return;
        }

        var start = hit.idx;
        var end = hit.idx;
        while (start > 0 && !char.IsWhiteSpace(chars[start - 1].Value.FirstOrDefault()))
        {
            start--;
        }

        while (end + 1 < chars.Count && !char.IsWhiteSpace(chars[end + 1].Value.FirstOrDefault()))
        {
            end++;
        }

        _selectedText = PdfTextSelection.CopyText(chars, start, end);
        await RefreshSearchHighlightsAsync();
        if (start <= end)
        {
            var union = chars[start].Bounds;
            for (var i = start; i <= end; i++)
            {
                var b = chars[i].Bounds;
                union = new PdfRect(
                    Math.Min(union.Left, b.Left),
                    Math.Min(union.Bottom, b.Bottom),
                    Math.Max(union.Right, b.Right),
                    Math.Max(union.Top, b.Top));
            }

            DrawSelectionOverlay(pageIndex, chars, union);
        }

        _status.Text = string.IsNullOrEmpty(_selectedText)
            ? $"Page {pageIndex + 1}"
            : $"Selected “{TrimForStatus(_selectedText)}”";
    }

    private void DrawSelectionOverlay(int pageIndex, IReadOnlyList<PdfTextChar> chars, PdfRect selection)
    {
        if (!_pageOverlays.TryGetValue(pageIndex, out var overlay))
        {
            return;
        }

        var page = _document.GetPage(pageIndex);
        foreach (var ch in PdfTextSelection.CharsInRect(chars, selection))
        {
            AddHighlightRect(overlay, page.HeightPoints, ch.Bounds, Windows.UI.Color.FromArgb(70, 30, 144, 255));
        }
    }

    private async Task RefreshSearchHighlightsAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            return;
        }

        foreach (var (pageIndex, overlay) in _pageOverlays)
        {
            overlay.Children.Clear();
            if (!_pageChars.ContainsKey(pageIndex))
            {
                _pageChars[pageIndex] = await _textExtractor.GetCharsAsync(_document, pageIndex);
            }

            var chars = _pageChars[pageIndex];
            var pageText = string.Concat(chars.Select(c => c.Value));
            var comparison = _searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var searchFrom = 0;
            var page = _document.GetPage(pageIndex);
            while (searchFrom < pageText.Length)
            {
                var found = pageText.IndexOf(_searchQuery, searchFrom, comparison);
                if (found < 0)
                {
                    break;
                }

                var end = Math.Min(chars.Count - 1, found + _searchQuery.Length - 1);
                for (var i = found; i <= end && i < chars.Count; i++)
                {
                    AddHighlightRect(overlay, page.HeightPoints, chars[i].Bounds, Windows.UI.Color.FromArgb(90, 255, 215, 0));
                }

                searchFrom = found + Math.Max(1, _searchQuery.Length);
            }
        }
    }

    private void AddHighlightRect(Canvas overlay, double pageHeightPoints, PdfRect bounds, Windows.UI.Color color)
    {
        var left = bounds.Left * _scale;
        var width = Math.Max(1, bounds.Width * _scale);
        var height = Math.Max(1, bounds.Height * _scale);
        var top = (pageHeightPoints - bounds.Top) * _scale;
        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(color),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        overlay.Children.Add(rect);
    }

    private static string TrimForStatus(string text)
    {
        var flat = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flat.Length <= 42 ? flat : flat[..42] + "…";
    }

    private sealed record OutlineItem(string Title, int? PageIndex)
    {
        public override string ToString() => Title;
    }

    private async void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            return;
        }

        var delta = e.GetCurrentPoint(_scrollViewer).Properties.MouseWheelDelta;
        await SetScaleAsync(PdfZoomCalculator.ApplyWheelZoom(_scale, delta));
        e.Handled = true;
    }

    private async void ScrollViewer_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (Math.Abs(e.Delta.Scale - 1.0) < 0.001)
        {
            return;
        }

        await SetScaleAsync(_scale * e.Delta.Scale);
        e.Handled = true;
    }

    private async Task RunSearchAsync()
    {
        if (_document.Path is null)
        {
            ClearSearchResults("Document path is unavailable for search.");
            return;
        }

        var query = _searchBox.Text ?? string.Empty;
        _status.Text = "Searching…";
        _searchQuery = query.Trim();
        _searchCaseSensitive = _caseSensitiveBox.IsChecked == true;

        var options = new PdfSearchOptions(
            CaseSensitive: _searchCaseSensitive,
            ExactPhrase: true);

        var result = await _searchCoordinator.SearchAsync(_document.Path, query, options);
        if (result.Status == PdfSearchStatus.Cancelled)
        {
            return;
        }

        _hits = result.Hits;
        _activeHitIndex = _hits.Count > 0 ? 0 : -1;
        if (result.Status is PdfSearchStatus.EmptyQuery or PdfSearchStatus.NoMatches)
        {
            _searchQuery = string.Empty;
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Clear();
            }
        }
        _searchResults.ItemsSource = _hits
            .Select(h => $"p.{h.PageIndex + 1}: {h.Snippet}")
            .ToList();

        _status.Text = result.Status switch
        {
            PdfSearchStatus.EmptyQuery => result.Message ?? "Enter search text.",
            PdfSearchStatus.NoMatches => result.Message ?? "No matches.",
            PdfSearchStatus.NoExtractableText => result.Message ?? "OCR required.",
            PdfSearchStatus.DocumentEncrypted => result.Message ?? "Password required.",
            PdfSearchStatus.Failed => result.Message ?? "Search failed.",
            PdfSearchStatus.Success => $"{_hits.Count} match{(_hits.Count == 1 ? string.Empty : "es")}",
            _ => result.Message ?? _status.Text,
        };

        if (_activeHitIndex >= 0)
        {
            _searchResults.SelectedIndex = _activeHitIndex;
            await GoToPageAsync(_hits[_activeHitIndex].PageIndex, recordHistory: true);
        }

        await RefreshSearchHighlightsAsync();
    }

    private void ClearSearchResults(string status)
    {
        _searchCoordinator.Cancel();
        _hits = [];
        _activeHitIndex = -1;
        _searchResults.ItemsSource = null;
        _status.Text = status;
    }

    private async Task GoToHitAsync(int hitIndex)
    {
        if (_hits.Count == 0)
        {
            return;
        }

        var wrapped = (hitIndex % _hits.Count + _hits.Count) % _hits.Count;
        _activeHitIndex = wrapped;
        _searchResults.SelectedIndex = wrapped;
        await GoToPageAsync(_hits[wrapped].PageIndex, recordHistory: true);
        _status.Text = $"Match {wrapped + 1} / {_hits.Count} · p.{_hits[wrapped].PageIndex + 1}";
    }

    private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_searchResults.SelectedIndex >= 0 && _searchResults.SelectedIndex < _hits.Count)
        {
            _activeHitIndex = _searchResults.SelectedIndex;
            await GoToPageAsync(_hits[_activeHitIndex].PageIndex, recordHistory: true);
        }
    }

    private async Task SetScaleAsync(double scale)
    {
        _scale = PdfZoomCalculator.Clamp(scale);
        _cache.ClearDocument(_documentKey);
        BuildPagePlaceholders();
        SyncViewState();
        UpdateStatus();
        await RenderVisibleAsync();
    }

    private async Task FitWidthAsync()
    {
        if (_document.PageCount == 0)
        {
            return;
        }

        var page = _document.GetPage(CurrentPageIndex);
        var scale = PdfZoomCalculator.FitWidth(_scrollViewer.ViewportWidth, page.WidthPoints);
        await SetScaleAsync(scale);
    }

    private async Task FitPageAsync()
    {
        if (_document.PageCount == 0)
        {
            return;
        }

        var page = _document.GetPage(CurrentPageIndex);
        var scale = PdfZoomCalculator.FitPage(
            _scrollViewer.ViewportWidth,
            _scrollViewer.ViewportHeight,
            page.WidthPoints,
            page.HeightPoints);
        await SetScaleAsync(scale);
    }

    private async Task GoToPageAsync(int pageIndex, bool recordHistory)
    {
        if (_document.PageCount == 0)
        {
            return;
        }

        pageIndex = PageLayoutCalculator.NormalizePageIndex(_layoutMode, pageIndex, _document.PageCount);
        if (pageIndex < 0 || pageIndex >= _document.PageCount)
        {
            return;
        }

        var pageChanged = pageIndex != CurrentPageIndex;
        CurrentPageIndex = pageIndex;
        if (recordHistory && pageChanged)
        {
            _history.NavigateTo(pageIndex);
        }

        if (_layoutMode != PageLayoutMode.Continuous)
        {
            BuildPagePlaceholders();
        }
        else if (_pageImages.TryGetValue(pageIndex, out _)
                 && _continuousHost.Children.OfType<FrameworkElement>().FirstOrDefault(fe => fe.Tag is int tag && tag == pageIndex) is { } element)
        {
            element.StartBringIntoView();
        }

        HighlightThumbnail(pageIndex);
        SyncViewState();
        UpdateStatus();
        await RenderVisibleAsync();
    }

    private void HighlightThumbnail(int pageIndex)
    {
        _suppressThumbnailNav = true;
        try
        {
            foreach (var (index, border) in _thumbnailBorders)
            {
                border.BorderBrush = new SolidColorBrush(index == pageIndex ? Colors.DodgerBlue : Colors.Transparent);
            }

            if (_thumbnailBorders.TryGetValue(pageIndex, out var selected))
            {
                selected.StartBringIntoView();
            }
        }
        finally
        {
            _suppressThumbnailNav = false;
        }
    }

    private async void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (!e.IsIntermediate && _layoutMode == PageLayoutMode.Continuous)
        {
            UpdateCurrentPageFromScroll();
            await RenderVisibleAsync();
        }
    }

    private void UpdateCurrentPageFromScroll()
    {
        if (_suppressThumbnailNav)
        {
            return;
        }

        var offset = _scrollViewer.VerticalOffset;
        double accumulated = 0;
        for (var i = 0; i < _continuousHost.Children.Count; i++)
        {
            if (_continuousHost.Children[i] is FrameworkElement fe)
            {
                var next = accumulated + fe.ActualHeight + 12;
                if (offset < next || i == _continuousHost.Children.Count - 1)
                {
                    if (CurrentPageIndex != i)
                    {
                        CurrentPageIndex = i;
                        HighlightThumbnail(i);
                        SyncViewState();
                        UpdateStatus();
                    }

                    return;
                }

                accumulated = next;
            }
        }
    }

    private async Task RenderVisibleAsync()
    {
        var generation = Interlocked.Increment(ref _renderGeneration);
        await _renderGate.WaitAsync();
        try
        {
            if (generation != _renderGeneration)
            {
                return;
            }

            var (first, last) = _layoutMode == PageLayoutMode.Continuous
                ? (Math.Max(0, CurrentPageIndex - 1), Math.Min(_document.PageCount - 1, CurrentPageIndex + 2))
                : PageLayoutCalculator.VisibleRange(_layoutMode, CurrentPageIndex, _document.PageCount);

            for (var i = first; i <= last; i++)
            {
                if (generation != _renderGeneration)
                {
                    return;
                }

                await RenderPageAsync(i);
            }
        }
        finally
        {
            _renderGate.Release();
        }
    }

    private async Task RenderThumbnailsAsync()
    {
        for (var i = 0; i < _document.PageCount; i++)
        {
            try
            {
                await RenderThumbnailAsync(i);
            }
            catch
            {
                // Thumbnail failures must not break viewing.
            }
        }
    }

    private async Task RenderPageAsync(int pageIndex)
    {
        if (!_pageImages.TryGetValue(pageIndex, out var image))
        {
            return;
        }

        if (_cache.TryGet(_documentKey, pageIndex, _scale, out var cached) && cached is not null)
        {
            image.Source = await ToWriteableBitmapAsync(cached);
            return;
        }

        var result = await _renderer.RenderPageAsync(
            _document,
            pageIndex,
            new PdfRenderRequest(_scale));

        _cache.Set(_documentKey, pageIndex, _scale, result);
        image.Width = result.Width;
        image.Height = result.Height;
        image.Source = await ToWriteableBitmapAsync(result);
    }

    private async Task RenderThumbnailAsync(int pageIndex)
    {
        if (!_thumbnailImages.TryGetValue(pageIndex, out var image))
        {
            return;
        }

        var page = _document.GetPage(pageIndex);
        var thumbScale = ThumbnailWidth / Math.Max(1, page.WidthPoints);

        if (_cache.TryGet(_thumbnailKey, pageIndex, thumbScale, out var cached) && cached is not null)
        {
            image.Source = await ToWriteableBitmapAsync(cached);
            return;
        }

        var result = await _renderer.RenderPageAsync(
            _document,
            pageIndex,
            new PdfRenderRequest(thumbScale, MaxWidthPixels: (int)ThumbnailWidth));

        _cache.Set(_thumbnailKey, pageIndex, thumbScale, result);
        image.Source = await ToWriteableBitmapAsync(result);
    }

    private static async Task<WriteableBitmap> ToWriteableBitmapAsync(PdfRenderResult result)
    {
        var bitmap = new WriteableBitmap(result.Width, result.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            var pixels = result.Pixels.ToArray();
            await stream.WriteAsync(pixels, 0, pixels.Length);
        }

        bitmap.Invalidate();
        return bitmap;
    }

    private void SyncViewState()
    {
        _viewState.Zoom = _scale;
        _viewState.CurrentPageIndex = CurrentPageIndex;
        _viewState.PageLayout = _layoutMode;
    }

    private void UpdateStatus()
    {
        _gotoBox.Text = (CurrentPageIndex + 1).ToString();
        _status.Text =
            $"Page {CurrentPageIndex + 1} / {_document.PageCount}    Zoom {(int)Math.Round(_scale * 100)}%    {_layoutMode}";
    }
}
