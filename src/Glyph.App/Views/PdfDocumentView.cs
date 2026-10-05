using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Editing;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Storage;
using Windows.System;

namespace Glyph.App.Views;

/// <summary>
/// PDF viewer: layout modes, zoom, page nav, bitmap thumbnails, Find, and page edits.
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
    private readonly IPdfPageEditor _pageEditor;
    private readonly IPdfAnnotationService _annotations;
    private readonly IPdfDocumentFactory _documentFactory;
    private readonly Window? _ownerWindow;
    private readonly DocumentViewState _viewState;
    private readonly DocumentNavigationHistory _history = new();
    private readonly PdfPageEditHistory _editHistory = new();
    private readonly PageSelection _pageSelection = new();
    private readonly string _documentKey;
    private readonly string _thumbnailKey;
    private Border? _dropHighlightBorder;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _continuousHost;
    private readonly StackPanel _spreadHost;
    private readonly StackPanel _thumbnailHost;
    private readonly ScrollViewer _thumbnailScroll;
    private readonly TreeView _outlineTree;
    private readonly ListView _searchResults;
    private readonly ListView _annotationList;
    private readonly TextBox _searchBox;
    private readonly TextBox _gotoBox;
    private readonly CheckBox _caseSensitiveBox;
    private readonly ComboBox _layoutBox;
    private readonly TextBlock _status;
    private readonly Dictionary<int, IReadOnlyList<PdfTextChar>> _pageChars = new();
    private readonly Dictionary<int, IReadOnlyList<PdfLink>> _pageLinks = new();
    private string _selectedText = string.Empty;
    private int _selectionPageIndex = -1;
    private IReadOnlyList<PdfQuad> _selectionQuads = [];
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
    private IReadOnlyList<PdfAnnotationInfo> _annotationItems = [];
    private bool _suppressAnnotationNav;
    private bool _inkMode;
    private bool _inkDrawing;
    private int _inkPageIndex = -1;
    private readonly List<PdfPagePoint> _inkPoints = [];
    private Microsoft.UI.Xaml.Shapes.Polyline? _inkPreview;
    private PdfShapeKind? _shapeMode;
    private bool _shapeDrawing;
    private int _shapePageIndex = -1;
    private Windows.Foundation.Point _shapeStart;
    private FrameworkElement? _shapePreview;
    private Button? _inkButton;
    private Button? _rectButton;
    private Button? _ellipseButton;
    private Button? _lineButton;
    private bool _dragSelecting;
    private Windows.Foundation.Point _dragStart;
    private int _dragPageIndex = -1;
    private string _searchQuery = string.Empty;
    private bool _searchCaseSensitive;
    private bool _cropMode;
    private int _cropPageIndex = -1;
    private double _cropMarginLeftPt;
    private double _cropMarginTopPt;
    private double _cropMarginRightPt;
    private double _cropMarginBottomPt;
    private string? _cropDragHandle;
    private Windows.Foundation.Point _cropPointerStart;
    private double _cropDragStartLeft;
    private double _cropDragStartTop;
    private double _cropDragStartRight;
    private double _cropDragStartBottom;
    private StackPanel? _cropChrome;
    private PdfLengthUnit _cropUnit = PdfLengthUnit.Points;

    public PdfDocumentView(
        IPdfDocument document,
        IPdfRenderer renderer,
        PageRenderCache cache,
        IPdfTextSearchService searchService,
        IPdfTextExtractor textExtractor,
        IPdfOutlineService outlineService,
        IPdfLinkService linkService,
        IPdfPageEditor pageEditor,
        IPdfAnnotationService annotations,
        IPdfDocumentFactory documentFactory,
        DocumentViewState? viewState = null,
        Window? ownerWindow = null)
    {
        _document = document;
        _renderer = renderer;
        _cache = cache;
        _searchCoordinator = new PdfSearchCoordinator(searchService);
        _textExtractor = textExtractor;
        _outlineService = outlineService;
        _linkService = linkService;
        _pageEditor = pageEditor;
        _annotations = annotations;
        _documentFactory = documentFactory;
        _ownerWindow = ownerWindow;
        _viewState = viewState ?? new DocumentViewState();
        _scale = PdfZoomCalculator.Clamp(_viewState.Zoom <= 0 ? 1.25 : _viewState.Zoom);
        _layoutMode = _viewState.PageLayout;
        CurrentPageIndex = Math.Clamp(_viewState.CurrentPageIndex, 0, Math.Max(0, document.PageCount - 1));
        _pageSelection.SelectOnly(CurrentPageIndex);
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
            AllowDrop = true,
        };
        _thumbnailScroll.DragOver += ThumbnailHost_DragOver;
        _thumbnailScroll.DragLeave += ThumbnailHost_DragLeave;
        _thumbnailScroll.Drop += ThumbnailHost_Drop;
        _outlineTree = new TreeView { SelectionMode = TreeViewSelectionMode.Single };
        _outlineTree.ItemInvoked += OutlineTree_ItemInvoked;

        _searchBox = new TextBox { PlaceholderText = "Find in document", Width = 160 };
        _searchBox.KeyDown += SearchBox_KeyDown;
        _caseSensitiveBox = new CheckBox { Content = "Aa", VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(_caseSensitiveBox, "Match case");
        var searchButton = new Button { Content = "Find" };
        searchButton.Click += async (_, _) => await RunSearchAsync();
        var clearSearch = new Button { Content = "Clear" };
        ToolTipService.SetToolTip(clearSearch, "Clear search results");
        clearSearch.Click += async (_, _) => await ClearSearchAsync();
        var prevMatch = new Button { Content = "◁" };
        var nextMatch = new Button { Content = "▷" };
        ToolTipService.SetToolTip(prevMatch, "Previous match");
        ToolTipService.SetToolTip(nextMatch, "Next match");
        prevMatch.Click += async (_, _) => await GoToHitAsync(_activeHitIndex - 1);
        nextMatch.Click += async (_, _) => await GoToHitAsync(_activeHitIndex + 1);
        _searchResults = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Height = 120,
        };
        _searchResults.SelectionChanged += SearchResults_SelectionChanged;
        _annotationList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Height = 140,
        };
        _annotationList.SelectionChanged += AnnotationList_SelectionChanged;

        var sidePanel = new Grid
        {
            Width = 180,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(100) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(110) },
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
        var annotHeaderRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = "Annotations",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
        var removeAnnot = new Button { Content = "Delete", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(removeAnnot, "Delete selected annotation");
        removeAnnot.Click += async (_, _) => await RemoveSelectedAnnotationAsync();
        annotHeaderRow.Children.Add(removeAnnot);
        Grid.SetRow(annotHeaderRow, 6);
        sidePanel.Children.Add(annotHeaderRow);
        Grid.SetRow(_annotationList, 7);
        sidePanel.Children.Add(_annotationList);

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
        var rotateLeft = new Button { Content = "⟲" };
        var rotateRight = new Button { Content = "⟳" };
        var deletePages = new Button { Content = "Delete" };
        var moveUp = new Button { Content = "↑" };
        var moveDown = new Button { Content = "↓" };
        var insertBlank = new Button { Content = "Blank" };
        var duplicate = new Button { Content = "Dup" };
        var extract = new Button { Content = "Extract" };
        var merge = new Button { Content = "Merge" };
        var split = new Button { Content = "Split" };
        var crop = new Button { Content = "Crop" };
        var highlight = new Button { Content = "Highlight" };
        var underline = new Button { Content = "Underline" };
        var strikeout = new Button { Content = "Strike" };
        var stickyNote = new Button { Content = "Note" };
        var textBox = new Button { Content = "TextBox" };
        var ink = new Button { Content = "Ink" };
        var rect = new Button { Content = "Rect" };
        var ellipse = new Button { Content = "Ellipse" };
        var line = new Button { Content = "Line" };
        _inkButton = ink;
        _rectButton = rect;
        _ellipseButton = ellipse;
        _lineButton = line;
        var undoEdit = new Button { Content = "Undo" };
        var redoEdit = new Button { Content = "Redo" };
        ToolTipService.SetToolTip(rotateLeft, "Rotate selected pages left");
        ToolTipService.SetToolTip(rotateRight, "Rotate selected pages right");
        ToolTipService.SetToolTip(deletePages, "Delete selected pages");
        ToolTipService.SetToolTip(moveUp, "Move selected pages earlier");
        ToolTipService.SetToolTip(moveDown, "Move selected pages later");
        ToolTipService.SetToolTip(insertBlank, "Insert blank page after selection");
        ToolTipService.SetToolTip(duplicate, "Duplicate selected pages");
        ToolTipService.SetToolTip(extract, "Extract selected pages to a new PDF file");
        ToolTipService.SetToolTip(merge, "Merge other PDF files into this document");
        ToolTipService.SetToolTip(split, "Split document before each selected page");
        ToolTipService.SetToolTip(crop, "Interactive CropBox crop (visual handles; numeric via Crop → Numeric)");
        ToolTipService.SetToolTip(highlight, "Highlight selected text");
        ToolTipService.SetToolTip(underline, "Underline selected text");
        ToolTipService.SetToolTip(strikeout, "Strike through selected text");
        ToolTipService.SetToolTip(stickyNote, "Add a sticky note on the current page");
        ToolTipService.SetToolTip(textBox, "Add a FreeText text box on the current page");
        ToolTipService.SetToolTip(ink, "Toggle freehand ink drawing on the page");
        ToolTipService.SetToolTip(rect, "Draw a rectangle annotation");
        ToolTipService.SetToolTip(ellipse, "Draw an ellipse annotation");
        ToolTipService.SetToolTip(line, "Draw a line (stored as a 2-point ink stroke)");
        ToolTipService.SetToolTip(undoEdit, "Undo last page edit (Ctrl+Z)");
        ToolTipService.SetToolTip(redoEdit, "Redo page edit (Ctrl+Y)");

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
        rotateLeft.Click += async (_, _) => await RotateSelectedAsync(-90);
        rotateRight.Click += async (_, _) => await RotateSelectedAsync(90);
        deletePages.Click += async (_, _) => await DeleteSelectedAsync();
        moveUp.Click += async (_, _) => await MoveSelectedAsync(delta: -1);
        moveDown.Click += async (_, _) => await MoveSelectedAsync(delta: 1);
        insertBlank.Click += async (_, _) => await InsertBlankAfterSelectionAsync();
        duplicate.Click += async (_, _) => await DuplicateSelectedAsync();
        extract.Click += async (_, _) => await ExtractSelectedAsync();
        merge.Click += async (_, _) => await MergePdfsAsync();
        split.Click += async (_, _) => await SplitDocumentAsync();
        crop.Click += async (_, _) => await BeginCropModeAsync();
        highlight.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.Highlight);
        underline.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.Underline);
        strikeout.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.StrikeOut);
        stickyNote.Click += async (_, _) => await AddStickyNoteAsync();
        textBox.Click += async (_, _) => await AddTextBoxAsync();
        ink.Click += (_, _) => ToggleInkMode();
        rect.Click += (_, _) => ToggleShapeMode(PdfShapeKind.Rectangle);
        ellipse.Click += (_, _) => ToggleShapeMode(PdfShapeKind.Ellipse);
        line.Click += (_, _) => ToggleShapeMode(PdfShapeKind.Line);
        undoEdit.Click += async (_, _) => await UndoPageEditAsync();
        redoEdit.Click += async (_, _) => await RedoPageEditAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Thickness(8),
            Children =
            {
                first, prev, _gotoBox, next, last, back, forward,
                zoomOut, zoomIn, fitWidth, fitPage, actual, _layoutBox, copy,
                undoEdit, redoEdit,
                rotateLeft, rotateRight, deletePages, moveUp, moveDown, insertBlank, duplicate, extract, merge, split, crop,
                highlight, underline, strikeout, stickyNote, textBox, ink, rect, ellipse, line,
                _searchBox, _caseSensitiveBox, searchButton, clearSearch, prevMatch, nextMatch, _status,
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
        PdfPageDragRegistry.Register(_documentKey, _document);
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
        _ = RefreshAnnotationSidebarAsync();
    }

    private void PdfDocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        PdfPageDragRegistry.Unregister(_documentKey);
        ClearDropHighlight();
        if (_cropMode)
        {
            CancelCropMode();
        }

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

        if (_layoutMode == PageLayoutMode.Continuous)
        {
            BuildContinuousWindow();
        }
        else
        {
            var (first, last) = PageLayoutCalculator.VisibleRange(_layoutMode, CurrentPageIndex, _document.PageCount);
            for (var i = first; i <= last; i++)
            {
                _spreadHost.Children.Add(CreatePageVisual(i));
            }
        }

        _ = RefreshSearchHighlightsAsync();
    }

    private void BuildContinuousWindow()
    {
        var (first, last) = ContinuousPageWindow.Around(CurrentPageIndex, _document.PageCount);
        if (last < first)
        {
            return;
        }

        if (first > 0)
        {
            _continuousHost.Children.Add(CreateSpacer(EstimateHeight(0, first - 1), tag: "spacer-before"));
        }

        for (var i = first; i <= last; i++)
        {
            _continuousHost.Children.Add(CreatePageVisual(i));
        }

        if (last < _document.PageCount - 1)
        {
            _continuousHost.Children.Add(CreateSpacer(EstimateHeight(last + 1, _document.PageCount - 1), tag: "spacer-after"));
        }
    }

    private double EstimateHeight(int startInclusive, int endInclusive)
    {
        double total = 0;
        for (var i = startInclusive; i <= endInclusive; i++)
        {
            var page = _document.GetPage(i);
            total += Math.Max(1, page.HeightPoints * _scale) + 12;
        }

        return Math.Max(1, total);
    }

    private static Border CreateSpacer(double height, string tag) =>
        new()
        {
            Height = height,
            Width = 1,
            Tag = tag,
            Opacity = 0,
            IsHitTestVisible = false,
        };

    private Border CreatePageVisual(int pageIndex)
    {
        var page = _document.GetPage(pageIndex);
        var width = Math.Max(1, page.WidthPoints * _scale);
        var height = Math.Max(1, page.HeightPoints * _scale);
        var image = new Image
        {
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
        };
        _pageImages[pageIndex] = image;

        var overlay = new Canvas
        {
            Width = width,
            Height = height,
            IsHitTestVisible = false,
        };
        _pageOverlays[pageIndex] = overlay;

        var layer = new Grid { Width = width, Height = height };
        layer.Children.Add(image);
        layer.Children.Add(overlay);

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(1),
            Child = layer,
            Tag = pageIndex,
            Background = new SolidColorBrush(Colors.Transparent),
        };
        border.PointerPressed += PageBorder_PointerPressed;
        border.PointerMoved += PageBorder_PointerMoved;
        border.PointerReleased += PageBorder_PointerReleased;
        border.PointerCaptureLost += (_, _) => _dragSelecting = false;
        return border;
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
                CanDrag = true,
                AllowDrop = true,
            };
            border.PointerPressed += Thumbnail_PointerPressed;
            border.DragStarting += Thumbnail_DragStarting;
            border.DragOver += Thumbnail_DragOver;
            border.DragLeave += ThumbnailHost_DragLeave;
            border.Drop += Thumbnail_Drop;
            _thumbnailBorders[i] = border;
            _thumbnailHost.Children.Add(border);
        }
    }

    private async void Thumbnail_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int index })
        {
            return;
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        _pageSelection.ApplyClick(index, ctrlOrMeta: ctrl, shift: shift);
        RefreshThumbnailSelectionChrome();
        await GoToPageAsync(index, recordHistory: true);
        e.Handled = true;
    }

    private void Thumbnail_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not Border { Tag: int index })
        {
            args.Cancel = true;
            return;
        }

        if (!_pageSelection.Contains(index))
        {
            _pageSelection.SelectOnly(index);
            RefreshThumbnailSelectionChrome();
        }

        var indexes = _pageSelection.SelectedIndexes.OrderBy(i => i).ToList();
        var payload = new PageDragPayload(_documentKey, indexes);
        args.Data.SetText(payload.Format());
        args.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move;
        args.Data.Properties.Title = indexes.Count == 1
            ? "PDF page"
            : $"{indexes.Count} PDF pages";

        // Deferred StorageItems so Explorer (and other apps) receive an extracted PDF on drop.
        var pageIndexes = indexes;
        args.Data.SetDataProvider(StandardDataFormats.StorageItems, request =>
        {
            ProvideExtractedPagesAsync(request, pageIndexes);
        });
    }

    private async void ProvideExtractedPagesAsync(DataProviderRequest request, IReadOnlyList<int> pageIndexes)
    {
        var deferral = request.GetDeferral();
        try
        {
            var tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Glyph-pages-" + Guid.NewGuid().ToString("N") + ".pdf");
            await using (var extracted = await _pageEditor.ExtractPagesAsync(_document, pageIndexes))
            {
                await _pageEditor.SaveAsync(extracted, tempPath);
            }

            var file = await StorageFile.GetFileFromPathAsync(tempPath);
            request.SetData(new List<IStorageItem> { file });
        }
        catch (Exception)
        {
            // Drag target may cancel; leave package empty.
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void Thumbnail_DragOver(object sender, DragEventArgs e)
    {
        if (!CanAcceptPageDrop(e.DataView))
        {
            return;
        }

        e.AcceptedOperation = PreferredDropOperation(e);
        e.DragUIOverride.IsGlyphVisible = true;
        e.DragUIOverride.Caption = DropCaption(e.DataView);
        e.Handled = true;

        if (sender is Border border)
        {
            var insertAfter = e.GetPosition(border).Y > border.ActualHeight / 2;
            ShowDropHighlight(border, insertAfter);
        }
    }

    private void ThumbnailHost_DragOver(object sender, DragEventArgs e)
    {
        if (!CanAcceptPageDrop(e.DataView))
        {
            return;
        }

        e.AcceptedOperation = PreferredDropOperation(e);
        e.DragUIOverride.Caption = DropCaption(e.DataView);
        e.Handled = true;

        // Dropping on empty sidebar area appends after the last page.
        if (_thumbnailBorders.Count > 0 &&
            _thumbnailBorders.TryGetValue(_document.PageCount - 1, out var last))
        {
            ShowDropHighlight(last, insertAfter: true);
        }
    }

    private void ThumbnailHost_DragLeave(object sender, DragEventArgs e) => ClearDropHighlight();

    private async void Thumbnail_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border { Tag: int dropIndex })
        {
            return;
        }

        var insertBefore = e.GetPosition((UIElement)sender).Y > ((FrameworkElement)sender).ActualHeight / 2
            ? dropIndex + 1
            : dropIndex;
        await HandleThumbnailDropAsync(e, insertBefore);
    }

    private async void ThumbnailHost_Drop(object sender, DragEventArgs e)
    {
        await HandleThumbnailDropAsync(e, insertBefore: _document.PageCount);
    }

    private async Task HandleThumbnailDropAsync(DragEventArgs e, int insertBefore)
    {
        ClearDropHighlight();
        insertBefore = Math.Clamp(insertBefore, 0, _document.PageCount);

        try
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                if (PageDragPayload.TryParse(text, out var payload) && payload is not null)
                {
                    var sameDocument = string.IsNullOrEmpty(payload.DocumentKey)
                        || string.Equals(payload.DocumentKey, _documentKey, StringComparison.Ordinal);
                    if (sameDocument)
                    {
                        await ReorderFromDragAsync(payload.PageIndexes.ToList(), insertBefore);
                        e.Handled = true;
                        return;
                    }

                    if (PdfPageDragRegistry.TryGet(payload.DocumentKey, out var source) && source is not null)
                    {
                        await InsertPagesFromDocumentAsync(source, payload.PageIndexes, insertBefore);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var pdfFiles = items
                    .OfType<StorageFile>()
                    .Where(f => f.FileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (pdfFiles.Count > 0)
                {
                    await InsertPdfFilesAsync(pdfFiles, insertBefore);
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Drop failed: " + ex.Message;
        }
    }

    private static bool CanAcceptPageDrop(DataPackageView data) =>
        data.Contains(StandardDataFormats.Text) || data.Contains(StandardDataFormats.StorageItems);

    private static DataPackageOperation PreferredDropOperation(DragEventArgs e)
    {
        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
        {
            return DataPackageOperation.Copy;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems) && !e.DataView.Contains(StandardDataFormats.Text))
        {
            return DataPackageOperation.Copy;
        }

        return DataPackageOperation.Move | DataPackageOperation.Copy;
    }

    private static string DropCaption(DataPackageView data) =>
        data.Contains(StandardDataFormats.StorageItems) && !data.Contains(StandardDataFormats.Text)
            ? "Insert PDF pages"
            : "Move or copy pages here";

    private void ShowDropHighlight(Border border, bool insertAfter)
    {
        if (!ReferenceEquals(_dropHighlightBorder, border))
        {
            ClearDropHighlight();
            _dropHighlightBorder = border;
        }

        border.BorderBrush = new SolidColorBrush(Colors.Orange);
        border.BorderThickness = insertAfter
            ? new Thickness(2, 2, 2, 5)
            : new Thickness(2, 5, 2, 2);
    }

    private void ClearDropHighlight()
    {
        if (_dropHighlightBorder is null)
        {
            return;
        }

        _dropHighlightBorder.BorderThickness = new Thickness(2);
        _dropHighlightBorder = null;
        RefreshThumbnailSelectionChrome();
    }

    private async Task ReorderFromDragAsync(List<int> selected, int insertBefore)
    {
        var order = PageReorder.MoveSelection(_document.PageCount, selected, insertBefore);
        if (order.Select((value, i) => value == i).All(x => x))
        {
            return;
        }

        _status.Text = "Reordering…";
        await RunPageEditAsync(() => _pageEditor.ReorderPagesAsync(_document, order));

        var remap = new Dictionary<int, int>();
        for (var newIndex = 0; newIndex < order.Count; newIndex++)
        {
            remap[order[newIndex]] = newIndex;
        }

        _pageSelection.Clear();
        foreach (var oldIndex in selected.OrderBy(i => i))
        {
            _pageSelection.Toggle(remap[oldIndex]);
        }

        await ReloadAfterPageEditAsync();
        if (_pageSelection.Count > 0)
        {
            await GoToPageAsync(_pageSelection.SelectedIndexes.Min(), recordHistory: false);
        }

        _status.Text = "Pages reordered.";
    }

    private async Task InsertPagesFromDocumentAsync(
        IPdfDocument source,
        IReadOnlyList<int> sourceIndexes,
        int insertBefore)
    {
        if (ReferenceEquals(source, _document))
        {
            await ReorderFromDragAsync(sourceIndexes.ToList(), insertBefore);
            return;
        }

        var count = sourceIndexes.Count;
        _status.Text = count == 1 ? "Inserting page…" : $"Inserting {count} pages…";
        await RunPageEditAsync(() => _pageEditor.InsertPagesAsync(_document, source, sourceIndexes, insertBefore));

        _pageSelection.Clear();
        for (var i = 0; i < count; i++)
        {
            _pageSelection.Toggle(insertBefore + i);
        }

        await ReloadAfterPageEditAsync();
        await GoToPageAsync(insertBefore, recordHistory: true);
        _status.Text = count == 1 ? "Inserted 1 page." : $"Inserted {count} pages.";
    }

    private async Task InsertPdfFilesAsync(IReadOnlyList<StorageFile> pdfFiles, int insertBefore)
    {
        var cursor = insertBefore;
        var totalInserted = 0;
        foreach (var file in pdfFiles)
        {
            _status.Text = $"Inserting {file.Name}…";
            await using var source = await _documentFactory.OpenAsync(file.Path);
            var indexes = Enumerable.Range(0, source.PageCount).ToList();
            if (indexes.Count == 0)
            {
                continue;
            }

            var insertAt = cursor;
            await RunPageEditAsync(() => _pageEditor.InsertPagesAsync(_document, source, indexes, insertAt));
            cursor += indexes.Count;
            totalInserted += indexes.Count;
        }

        if (totalInserted == 0)
        {
            _status.Text = "No pages to insert.";
            return;
        }

        _pageSelection.Clear();
        for (var i = 0; i < totalInserted; i++)
        {
            _pageSelection.Toggle(insertBefore + i);
        }

        await ReloadAfterPageEditAsync();
        await GoToPageAsync(insertBefore, recordHistory: true);
        _status.Text = totalInserted == 1
            ? "Inserted 1 page from file."
            : $"Inserted {totalInserted} pages from file{(pdfFiles.Count == 1 ? string.Empty : "s")}.";
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await RunSearchAsync();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            await ClearSearchAsync();
            e.Handled = true;
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
        if (_cropMode && e.Key == VirtualKey.Escape)
        {
            CancelCropMode();
            e.Handled = true;
            return;
        }

        if (_cropMode && e.Key == VirtualKey.Enter)
        {
            await ApplyCropModeAsync();
            e.Handled = true;
            return;
        }

        var ctrlDown = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shiftDown = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrlDown && e.Key == VirtualKey.C)
        {
            // Prefer text when the user has a text selection; otherwise copy selected pages.
            if (!string.IsNullOrEmpty(_selectedText))
            {
                await CopyTextAsync();
            }
            else
            {
                await CopySelectedPagesAsync();
            }

            e.Handled = true;
            return;
        }

        if (ctrlDown && e.Key == VirtualKey.V)
        {
            await PastePagesAsync();
            e.Handled = true;
            return;
        }

        if (ctrlDown && e.Key == VirtualKey.A)
        {
            _pageSelection.SelectAll(_document.PageCount);
            RefreshThumbnailSelectionChrome();
            _status.Text = _document.PageCount == 1
                ? "Selected 1 page."
                : $"Selected {_document.PageCount} pages.";
            e.Handled = true;
            return;
        }

        if (ctrlDown && e.Key == VirtualKey.Z)
        {
            await UndoPageEditAsync();
            e.Handled = true;
            return;
        }

        if (ctrlDown && e.Key == VirtualKey.Y)
        {
            await RedoPageEditAsync();
            e.Handled = true;
            return;
        }

        if (e.Key is VirtualKey.Delete or VirtualKey.Back)
        {
            await DeleteSelectedAsync();
            e.Handled = true;
            return;
        }

        if (e.Key is VirtualKey.Up or VirtualKey.Down)
        {
            var delta = e.Key == VirtualKey.Up ? -1 : 1;
            var focus = _pageSelection.SelectedIndexes.DefaultIfEmpty(CurrentPageIndex).Max();
            if (shiftDown && _pageSelection.Count > 0)
            {
                focus = delta < 0
                    ? _pageSelection.SelectedIndexes.Min()
                    : _pageSelection.SelectedIndexes.Max();
            }

            var next = Math.Clamp(focus + delta, 0, Math.Max(0, _document.PageCount - 1));
            _pageSelection.ApplyKeyboardMove(next, extendRange: shiftDown);
            RefreshThumbnailSelectionChrome();
            await GoToPageAsync(next, recordHistory: !shiftDown);
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

    private async Task CopySelectedPagesAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        try
        {
            await PdfPageClipboard.SetFromDocumentAsync(_pageEditor, _document, indexes);
            _status.Text = indexes.Count == 1
                ? "Copied 1 page."
                : $"Copied {indexes.Count} pages.";
        }
        catch (Exception ex)
        {
            _status.Text = "Copy pages failed: " + ex.Message;
        }
    }

    private async Task PastePagesAsync()
    {
        if (!PdfPageClipboard.HasPages)
        {
            _status.Text = "No pages on the clipboard.";
            return;
        }

        var insertAt = SelectedOrCurrentPages().DefaultIfEmpty(CurrentPageIndex).Max() + 1;
        insertAt = Math.Clamp(insertAt, 0, _document.PageCount);
        string? tempPath = null;
        try
        {
            var (source, path) = await PdfPageClipboard.OpenCopyAsync(_documentFactory);
            tempPath = path;
            if (source is null || source.PageCount == 0)
            {
                _status.Text = "Clipboard pages unavailable.";
                return;
            }

            await using (source)
            {
                var indexes = Enumerable.Range(0, source.PageCount).ToList();
                _status.Text = indexes.Count == 1 ? "Pasting page…" : $"Pasting {indexes.Count} pages…";
                await RunPageEditAsync(() => _pageEditor.InsertPagesAsync(_document, source, indexes, insertAt));

                _pageSelection.Clear();
                for (var i = 0; i < indexes.Count; i++)
                {
                    _pageSelection.Toggle(insertAt + i);
                }

                await ReloadAfterPageEditAsync();
                await GoToPageAsync(insertAt, recordHistory: true);
                _status.Text = indexes.Count == 1 ? "Pasted 1 page." : $"Pasted {indexes.Count} pages.";
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Paste pages failed: " + ex.Message;
        }
        finally
        {
            if (tempPath is not null && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }

    private void PageBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int pageIndex } border)
        {
            return;
        }

        if (_cropMode)
        {
            if (pageIndex == _cropPageIndex)
            {
                BeginCropPointerDrag(border, e);
            }

            e.Handled = true;
            return;
        }

        if (_inkMode)
        {
            BeginInkStroke(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        if (_shapeMode is not null)
        {
            BeginShapeDrag(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        _dragSelecting = true;
        _dragPageIndex = pageIndex;
        _dragStart = e.GetCurrentPoint(border).Position;
        border.CapturePointer(e.Pointer);
    }

    private void PageBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_cropMode)
        {
            if (sender is Border { Tag: int cropPage } cropBorder &&
                cropPage == _cropPageIndex &&
                _cropDragHandle is not null)
            {
                UpdateCropPointerDrag(cropBorder, e);
                e.Handled = true;
            }

            return;
        }

        if (_inkMode && _inkDrawing)
        {
            if (sender is Border { Tag: int inkPage } inkBorder && inkPage == _inkPageIndex)
            {
                ContinueInkStroke(inkBorder, e);
                e.Handled = true;
            }

            return;
        }

        if (_shapeMode is not null && _shapeDrawing)
        {
            if (sender is Border { Tag: int shapePage } shapeBorder && shapePage == _shapePageIndex)
            {
                ContinueShapeDrag(shapeBorder, e);
                e.Handled = true;
            }

            return;
        }

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

        if (_cropMode)
        {
            if (pageIndex == _cropPageIndex)
            {
                _cropDragHandle = null;
                try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
                RedrawCropOverlay();
            }

            e.Handled = true;
            return;
        }

        if (_inkMode && _inkDrawing && pageIndex == _inkPageIndex)
        {
            await EndInkStrokeAsync(border, e);
            e.Handled = true;
            return;
        }

        if (_shapeMode is not null && _shapeDrawing && pageIndex == _shapePageIndex)
        {
            await EndShapeDragAsync(border, e);
            e.Handled = true;
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
            _selectionPageIndex = pageIndex;
            _selectionQuads = PdfTextMarkupQuads.FromSelectionRect(chars, selection);
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
        _selectionPageIndex = pageIndex;
        _selectionQuads = PdfTextMarkupQuads.FromIndexRange(chars, start, end);
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
        _searchQuery = string.Empty;
        _searchResults.ItemsSource = null;
        _status.Text = status;
    }

    private async Task ClearSearchAsync()
    {
        _searchBox.Text = string.Empty;
        ClearSearchResults("Search cleared.");
        foreach (var overlay in _pageOverlays.Values)
        {
            overlay.Children.Clear();
        }

        await Task.CompletedTask;
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

        // Rebuild when the target page is outside the materialized continuous window
        // or whenever facing/single layouts need a new spread.
        if (_layoutMode != PageLayoutMode.Continuous || !_pageImages.ContainsKey(pageIndex))
        {
            BuildPagePlaceholders();
        }

        var target = _continuousHost.Children.OfType<FrameworkElement>().FirstOrDefault(fe => fe.Tag is int tag && tag == pageIndex)
            ?? _spreadHost.Children.OfType<FrameworkElement>().FirstOrDefault(fe => fe.Tag is int tag && tag == pageIndex);
        target?.StartBringIntoView();

        HighlightThumbnail(pageIndex);
        SyncViewState();
        UpdateStatus();
        await RenderVisibleAsync();
    }

    private void HighlightThumbnail(int pageIndex)
    {
        if (_pageSelection.Count == 0 || !_pageSelection.Contains(pageIndex))
        {
            _pageSelection.SelectOnly(pageIndex);
        }

        RefreshThumbnailSelectionChrome();
        if (_thumbnailBorders.TryGetValue(pageIndex, out var selected))
        {
            selected.StartBringIntoView();
        }
    }

    private void RefreshThumbnailSelectionChrome()
    {
        _suppressThumbnailNav = true;
        try
        {
            foreach (var (index, border) in _thumbnailBorders)
            {
                var isSelected = _pageSelection.Contains(index);
                var isCurrent = index == CurrentPageIndex;
                border.BorderBrush = new SolidColorBrush(
                    isSelected ? Colors.DodgerBlue : isCurrent ? Colors.SteelBlue : Colors.Transparent);
            }
        }
        finally
        {
            _suppressThumbnailNav = false;
        }
    }

    private async Task RotateSelectedAsync(int deltaDegrees)
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        _status.Text = "Rotating…";
        await RunPageEditAsync(() => _pageEditor.RotatePagesAsync(_document, indexes, deltaDegrees));
        await ReloadAfterPageEditAsync();
        _status.Text = $"Rotated {indexes.Count} page{(indexes.Count == 1 ? string.Empty : "s")}.";
    }

    private async Task DeleteSelectedAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        if (indexes.Count >= _document.PageCount)
        {
            _status.Text = "Cannot delete every page.";
            return;
        }

        _status.Text = "Deleting…";
        await RunPageEditAsync(() => _pageEditor.DeletePagesAsync(_document, indexes));
        await ReloadAfterPageEditAsync();
        _status.Text = $"Deleted {indexes.Count} page{(indexes.Count == 1 ? string.Empty : "s")}.";
    }

    private async Task MoveSelectedAsync(int delta)
    {
        var selected = SelectedOrCurrentPages().OrderBy(i => i).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var order = Enumerable.Range(0, _document.PageCount).ToList();
        if (delta < 0)
        {
            for (var i = 0; i < selected.Count; i++)
            {
                var index = selected[i];
                if (index == 0 || selected.Contains(index - 1))
                {
                    continue;
                }

                (order[index - 1], order[index]) = (order[index], order[index - 1]);
            }
        }
        else
        {
            for (var i = selected.Count - 1; i >= 0; i--)
            {
                var index = selected[i];
                if (index >= order.Count - 1 || selected.Contains(index + 1))
                {
                    continue;
                }

                (order[index + 1], order[index]) = (order[index], order[index + 1]);
            }
        }

        if (order.Select((value, index) => value == index).All(x => x))
        {
            return;
        }

        _status.Text = "Reordering…";
        await RunPageEditAsync(() => _pageEditor.ReorderPagesAsync(_document, order));
        // Remap selection to new indexes.
        var remap = new Dictionary<int, int>();
        for (var newIndex = 0; newIndex < order.Count; newIndex++)
        {
            remap[order[newIndex]] = newIndex;
        }

        var moved = selected.Select(i => remap[i]).OrderBy(i => i).ToList();
        _pageSelection.Clear();
        foreach (var index in moved)
        {
            _pageSelection.Toggle(index);
        }

        await ReloadAfterPageEditAsync();
        if (moved.Count > 0)
        {
            await GoToPageAsync(moved[0], recordHistory: false);
        }

        _status.Text = "Pages reordered.";
    }

    private List<int> SelectedOrCurrentPages()
    {
        if (_pageSelection.Count > 0)
        {
            return _pageSelection.SelectedIndexes.OrderBy(i => i).ToList();
        }

        return [CurrentPageIndex];
    }

    private async Task InsertBlankAfterSelectionAsync()
    {
        var insertAt = SelectedOrCurrentPages().DefaultIfEmpty(CurrentPageIndex).Max() + 1;
        var template = _document.GetPage(Math.Clamp(CurrentPageIndex, 0, _document.PageCount - 1));
        _status.Text = "Inserting blank page…";
        await RunPageEditAsync(() => _pageEditor.InsertBlankPageAsync(
            _document,
            insertAt,
            template.WidthPoints,
            template.HeightPoints));
        _pageSelection.SelectOnly(insertAt);
        await ReloadAfterPageEditAsync();
        await GoToPageAsync(insertAt, recordHistory: true);
        _status.Text = "Inserted blank page.";
    }

    private async Task DuplicateSelectedAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        _status.Text = "Duplicating…";
        await RunPageEditAsync(() => _pageEditor.DuplicatePagesAsync(_document, indexes));
        await ReloadAfterPageEditAsync();
        _status.Text = $"Duplicated {indexes.Count} page{(indexes.Count == 1 ? string.Empty : "s")}.";
    }

    private async Task ApplyTextMarkupAsync(PdfTextMarkupKind kind)
    {
        if (_selectionPageIndex < 0 || _selectionQuads.Count == 0 || string.IsNullOrEmpty(_selectedText))
        {
            _status.Text = "Select text first, then apply markup.";
            return;
        }

        var color = kind switch
        {
            PdfTextMarkupKind.Highlight => PdfAnnotationColor.YellowHighlight,
            PdfTextMarkupKind.Underline => PdfAnnotationColor.UnderlineBlue,
            PdfTextMarkupKind.StrikeOut => PdfAnnotationColor.StrikeOutRed,
            _ => PdfAnnotationColor.YellowHighlight,
        };

        try
        {
            _status.Text = kind switch
            {
                PdfTextMarkupKind.Highlight => "Highlighting…",
                PdfTextMarkupKind.Underline => "Underlining…",
                _ => "Striking through…",
            };

            await _annotations.AddTextMarkupAsync(
                _document,
                _selectionPageIndex,
                kind,
                _selectionQuads,
                color);

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = kind switch
            {
                PdfTextMarkupKind.Highlight => "Highlight added.",
                PdfTextMarkupKind.Underline => "Underline added.",
                _ => "Strikethrough added.",
            };
        }
        catch (Exception ex)
        {
            _status.Text = "Markup failed: " + ex.Message;
        }
    }

    private async Task RefreshAnnotationSidebarAsync()
    {
        try
        {
            var all = await _annotations.ListAsync(_document);
            _annotationItems = all
                .Where(a => a.TextMarkupKind is not null || a.IsStickyNote || a.IsInk || a.ShapeKind is not null || a.IsTextBox)
                .OrderBy(a => a.PageIndex)
                .ThenBy(a => a.AnnotIndex)
                .ToList();

            _suppressAnnotationNav = true;
            _annotationList.ItemsSource = _annotationItems
                .Select(FormatAnnotationLabel)
                .ToList();
            _suppressAnnotationNav = false;
        }
        catch (Exception ex)
        {
            _status.Text = "Annotation list failed: " + ex.Message;
        }
    }

    private static string FormatAnnotationLabel(PdfAnnotationInfo info)
    {
        if (info.IsStickyNote)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimForStatus(info.Contents);
            return $"Note · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsTextBox)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimForStatus(info.Contents);
            return $"Text · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.ShapeKind is { } shape)
        {
            var shapeName = shape switch
            {
                PdfShapeKind.Rectangle => "Rect",
                PdfShapeKind.Ellipse => "Ellipse",
                PdfShapeKind.Line => "Line",
                _ => "Shape",
            };
            return $"{shapeName} · p.{info.PageIndex + 1}";
        }

        if (info.IsInk)
        {
            return $"Ink · p.{info.PageIndex + 1}";
        }

        var kind = info.TextMarkupKind switch
        {
            PdfTextMarkupKind.Highlight => "Highlight",
            PdfTextMarkupKind.Underline => "Underline",
            PdfTextMarkupKind.StrikeOut => "Strike",
            _ => "Markup",
        };
        return $"{kind} · p.{info.PageIndex + 1}";
    }

    private void ToggleInkMode()
    {
        ClearShapeMode();
        _inkMode = !_inkMode;
        if (!_inkMode)
        {
            CancelInkStroke();
            _status.Text = "Ink mode off.";
            RefreshToolButtonChrome();
            return;
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        RefreshToolButtonChrome();
        _status.Text = "Ink mode on — draw on the page.";
    }

    private void ToggleShapeMode(PdfShapeKind kind)
    {
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_shapeMode == kind)
        {
            ClearShapeMode();
            _status.Text = "Shape mode off.";
            RefreshToolButtonChrome();
            return;
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        CancelShapeDrag();
        _shapeMode = kind;
        RefreshToolButtonChrome();
        _status.Text = kind switch
        {
            PdfShapeKind.Rectangle => "Rectangle mode — drag on the page.",
            PdfShapeKind.Ellipse => "Ellipse mode — drag on the page.",
            _ => "Line mode — drag on the page.",
        };
    }

    private void ClearShapeMode()
    {
        CancelShapeDrag();
        _shapeMode = null;
    }

    private void RefreshToolButtonChrome()
    {
        var active = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 140, 0));
        if (_inkButton is not null)
        {
            _inkButton.Background = _inkMode ? active : null;
        }

        if (_rectButton is not null)
        {
            _rectButton.Background = _shapeMode == PdfShapeKind.Rectangle ? active : null;
        }

        if (_ellipseButton is not null)
        {
            _ellipseButton.Background = _shapeMode == PdfShapeKind.Ellipse ? active : null;
        }

        if (_lineButton is not null)
        {
            _lineButton.Background = _shapeMode == PdfShapeKind.Line ? active : null;
        }
    }

    private void BeginShapeDrag(Border border, int pageIndex, PointerRoutedEventArgs e)
    {
        CancelShapeDrag();
        _shapeDrawing = true;
        _shapePageIndex = pageIndex;
        _shapeStart = e.GetCurrentPoint(border).Position;
        border.CapturePointer(e.Pointer);
        ContinueShapeDrag(border, e);
    }

    private void ContinueShapeDrag(Border border, PointerRoutedEventArgs e)
    {
        if (_shapePageIndex < 0 || !_pageOverlays.TryGetValue(_shapePageIndex, out var overlay) || _shapeMode is null)
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        if (_shapePreview is not null)
        {
            overlay.Children.Remove(_shapePreview);
            _shapePreview = null;
        }

        var left = Math.Min(_shapeStart.X, current.X);
        var top = Math.Min(_shapeStart.Y, current.Y);
        var width = Math.Abs(current.X - _shapeStart.X);
        var height = Math.Abs(current.Y - _shapeStart.Y);
        var stroke = new SolidColorBrush(Colors.DodgerBlue);

        FrameworkElement preview = _shapeMode switch
        {
            PdfShapeKind.Ellipse => new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = Math.Max(1, width),
                Height = Math.Max(1, height),
                Stroke = stroke,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
            },
            PdfShapeKind.Line => new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = _shapeStart.X,
                Y1 = _shapeStart.Y,
                X2 = current.X,
                Y2 = current.Y,
                Stroke = stroke,
                StrokeThickness = 2,
            },
            _ => new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = Math.Max(1, width),
                Height = Math.Max(1, height),
                Stroke = stroke,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
            },
        };

        if (preview is not Microsoft.UI.Xaml.Shapes.Line)
        {
            Canvas.SetLeft(preview, left);
            Canvas.SetTop(preview, top);
        }

        overlay.Children.Add(preview);
        _shapePreview = preview;
    }

    private async Task EndShapeDragAsync(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        ContinueShapeDrag(border, e);

        var kind = _shapeMode;
        var pageIndex = _shapePageIndex;
        var start = _shapeStart;
        var end = e.GetCurrentPoint(border).Position;
        CancelShapeDrag();

        if (kind is null || pageIndex < 0)
        {
            return;
        }

        var page = _document.GetPage(pageIndex);
        double ToPdfX(double x) => x / _scale;
        double ToPdfY(double y) => page.HeightPoints - (y / _scale);

        PdfRect bounds;
        if (kind == PdfShapeKind.Line)
        {
            bounds = new PdfRect(
                ToPdfX(start.X),
                ToPdfY(start.Y),
                ToPdfX(end.X),
                ToPdfY(end.Y));
        }
        else
        {
            var left = Math.Min(ToPdfX(start.X), ToPdfX(end.X));
            var right = Math.Max(ToPdfX(start.X), ToPdfX(end.X));
            var bottom = Math.Min(ToPdfY(start.Y), ToPdfY(end.Y));
            var top = Math.Max(ToPdfY(start.Y), ToPdfY(end.Y));
            bounds = new PdfRect(left, bottom, right, top);
        }

        try
        {
            _status.Text = "Saving shape…";
            await _annotations.AddShapeAsync(
                _document,
                pageIndex,
                kind.Value,
                bounds,
                new PdfAnnotationColor(30, 144, 255),
                fillColor: kind == PdfShapeKind.Line
                    ? null
                    : new PdfAnnotationColor(30, 144, 255, 40));
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = kind switch
            {
                PdfShapeKind.Rectangle => "Rectangle added.",
                PdfShapeKind.Ellipse => "Ellipse added.",
                _ => "Line added.",
            };
        }
        catch (Exception ex)
        {
            _status.Text = "Shape failed: " + ex.Message;
        }
    }

    private void CancelShapeDrag()
    {
        if (_shapePreview is not null &&
            _shapePageIndex >= 0 &&
            _pageOverlays.TryGetValue(_shapePageIndex, out var overlay))
        {
            overlay.Children.Remove(_shapePreview);
        }

        _shapePreview = null;
        _shapeDrawing = false;
        _shapePageIndex = -1;
    }

    private void BeginInkStroke(Border border, int pageIndex, PointerRoutedEventArgs e)
    {
        CancelInkStroke();
        _inkDrawing = true;
        _inkPageIndex = pageIndex;
        _inkPoints.Clear();
        border.CapturePointer(e.Pointer);
        AppendInkPoint(border, pageIndex, e.GetCurrentPoint(border).Position);
    }

    private void ContinueInkStroke(Border border, PointerRoutedEventArgs e)
    {
        if (_inkPageIndex < 0)
        {
            return;
        }

        AppendInkPoint(border, _inkPageIndex, e.GetCurrentPoint(border).Position);
    }

    private void AppendInkPoint(Border border, int pageIndex, Windows.Foundation.Point uiPoint)
    {
        var page = _document.GetPage(pageIndex);
        var pdfX = uiPoint.X / _scale;
        var pdfY = page.HeightPoints - (uiPoint.Y / _scale);
        _inkPoints.Add(new PdfPagePoint(pdfX, pdfY));

        if (!_pageOverlays.TryGetValue(pageIndex, out var overlay))
        {
            return;
        }

        if (_inkPreview is null)
        {
            _inkPreview = new Microsoft.UI.Xaml.Shapes.Polyline
            {
                Stroke = new SolidColorBrush(Colors.OrangeRed),
                StrokeThickness = 2,
                Fill = null,
            };
            overlay.Children.Add(_inkPreview);
        }

        _inkPreview.Points.Add(uiPoint);
    }

    private async Task EndInkStrokeAsync(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        ContinueInkStroke(border, e);

        var points = _inkPoints.ToList();
        var pageIndex = _inkPageIndex;
        CancelInkStroke();

        if (points.Count < 2 || pageIndex < 0)
        {
            _status.Text = "Ink stroke too short.";
            return;
        }

        try
        {
            _status.Text = "Saving ink…";
            await _annotations.AddInkAsync(
                _document,
                pageIndex,
                points,
                new PdfAnnotationColor(220, 60, 40));
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Ink stroke added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Ink failed: " + ex.Message;
        }
    }

    private void CancelInkStroke()
    {
        if (_inkPreview is not null &&
            _inkPageIndex >= 0 &&
            _pageOverlays.TryGetValue(_inkPageIndex, out var overlay))
        {
            overlay.Children.Remove(_inkPreview);
        }

        _inkPreview = null;
        _inkDrawing = false;
        _inkPageIndex = -1;
        _inkPoints.Clear();
    }

    private async Task AddStickyNoteAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for note dialog.");

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            PlaceholderText = "Note text",
        };
        var dialog = new ContentDialog
        {
            Title = "Sticky note",
            Content = box,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _status.Text = "Note cancelled.";
            return;
        }

        var page = _document.GetPage(CurrentPageIndex);
        var x = Math.Max(24, page.WidthPoints * 0.5 - 10);
        var y = Math.Max(24, page.HeightPoints * 0.5 - 10);

        try
        {
            _status.Text = "Adding note…";
            await _annotations.AddStickyNoteAsync(
                _document,
                CurrentPageIndex,
                x,
                y,
                box.Text ?? string.Empty,
                PdfAnnotationColor.StickyNoteYellow);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Sticky note added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Note failed: " + ex.Message;
        }
    }

    private async Task AddTextBoxAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for text box dialog.");

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 140,
            PlaceholderText = "Text box contents",
        };
        var dialog = new ContentDialog
        {
            Title = "Text box",
            Content = box,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _status.Text = "Text box cancelled.";
            return;
        }

        var page = _document.GetPage(CurrentPageIndex);
        var width = Math.Min(240, page.WidthPoints * 0.45);
        var height = 72;
        var left = Math.Max(24, (page.WidthPoints - width) / 2);
        var bottom = Math.Max(24, (page.HeightPoints - height) / 2);
        var bounds = new PdfRect(left, bottom, left + width, bottom + height);

        try
        {
            _status.Text = "Adding text box…";
            await _annotations.AddTextBoxAsync(
                _document,
                CurrentPageIndex,
                bounds,
                box.Text ?? string.Empty,
                new PdfAnnotationColor(20, 20, 20));
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Text box added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Text box failed: " + ex.Message;
        }
    }

    private async void AnnotationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressAnnotationNav)
        {
            return;
        }

        var index = _annotationList.SelectedIndex;
        if (index < 0 || index >= _annotationItems.Count)
        {
            return;
        }

        var item = _annotationItems[index];
        await GoToPageAsync(item.PageIndex, recordHistory: true);
        _status.Text = $"Jumped to {FormatAnnotationLabel(item)}.";
    }

    private async Task RemoveSelectedAnnotationAsync()
    {
        var index = _annotationList.SelectedIndex;
        if (index < 0 || index >= _annotationItems.Count)
        {
            _status.Text = "Select an annotation to delete.";
            return;
        }

        var item = _annotationItems[index];
        try
        {
            await _annotations.RemoveAsync(_document, item.PageIndex, item.AnnotIndex);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = $"Deleted {FormatAnnotationLabel(item)}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Delete annotation failed: " + ex.Message;
        }
    }

    private async Task RunPageEditAsync(Func<Task> mutation)
    {
        await _editHistory.ExecuteAsync(_document, _pageEditor, mutation);
    }

    private async Task UndoPageEditAsync()
    {
        if (!_editHistory.CanUndo)
        {
            _status.Text = "Nothing to undo.";
            return;
        }

        _status.Text = "Undoing…";
        await _editHistory.UndoAsync(_document, _pageEditor);
        await ReloadAfterPageEditAsync();
        _status.Text = "Undid page edit.";
    }

    private async Task RedoPageEditAsync()
    {
        if (!_editHistory.CanRedo)
        {
            _status.Text = "Nothing to redo.";
            return;
        }

        _status.Text = "Redoing…";
        await _editHistory.RedoAsync(_document, _pageEditor);
        await ReloadAfterPageEditAsync();
        _status.Text = "Redid page edit.";
    }

    private async Task ExtractSelectedAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for save picker.");
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = "Extracted pages";
        picker.FileTypeChoices.Add("PDF", [".pdf"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            _status.Text = "Extract cancelled.";
            return;
        }

        _status.Text = "Extracting…";
        await using var extracted = await _pageEditor.ExtractPagesAsync(_document, indexes);
        await _pageEditor.SaveAsync(extracted, file.Path);
        _status.Text = $"Extracted {indexes.Count} page{(indexes.Count == 1 ? string.Empty : "s")} to {file.Name}.";
    }

    private async Task BeginCropModeAsync()
    {
        if (_document.PageCount == 0)
        {
            return;
        }

        await GoToPageAsync(CurrentPageIndex, recordHistory: false);
        _cropMode = true;
        _cropPageIndex = CurrentPageIndex;
        var page = _document.GetPage(_cropPageIndex);
        var inset = Math.Min(36, Math.Min(page.WidthPoints, page.HeightPoints) / 10);
        _cropMarginLeftPt = inset;
        _cropMarginTopPt = inset;
        _cropMarginRightPt = inset;
        _cropMarginBottomPt = inset;
        _cropDragHandle = null;

        EnsureCropChrome();
        if (_pageOverlays.TryGetValue(_cropPageIndex, out var overlay))
        {
            overlay.IsHitTestVisible = true;
        }

        RedrawCropOverlay();
        _status.Text = "Crop mode — drag handles, Enter to apply, Esc to cancel.";
    }

    private void EnsureCropChrome()
    {
        if (_cropChrome is not null)
        {
            _cropChrome.Visibility = Visibility.Visible;
            return;
        }

        var apply = new Button { Content = "Apply crop" };
        var cancel = new Button { Content = "Cancel" };
        var numeric = new Button { Content = "Numeric…" };
        var exportCropped = new Button { Content = "Export cropped…" };
        apply.Click += async (_, _) => await ApplyCropModeAsync();
        cancel.Click += (_, _) => CancelCropMode();
        numeric.Click += async (_, _) => await CropNumericDialogAsync();
        exportCropped.Click += async (_, _) => await ExportCroppedAsync();
        ToolTipService.SetToolTip(exportCropped, "Export selected pages with a permanent MediaBox crop");
        _cropChrome = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Padding = new Thickness(8, 0, 8, 8),
            Children =
            {
                new TextBlock
                {
                    Text = "Crop handles",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                },
                apply,
                cancel,
                numeric,
                exportCropped,
            },
        };

        if (Content is Grid root)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_cropChrome, root.RowDefinitions.Count - 1);
            root.Children.Add(_cropChrome);
        }
    }

    private void CancelCropMode()
    {
        _cropMode = false;
        _cropDragHandle = null;
        if (_cropPageIndex >= 0 && _pageOverlays.TryGetValue(_cropPageIndex, out var overlay))
        {
            overlay.Children.Clear();
            overlay.IsHitTestVisible = false;
        }

        _cropPageIndex = -1;
        if (_cropChrome is not null)
        {
            _cropChrome.Visibility = Visibility.Collapsed;
        }

        _status.Text = "Crop cancelled.";
    }

    private async Task ApplyCropModeAsync()
    {
        if (!_cropMode)
        {
            return;
        }

        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            indexes = [_cropPageIndex];
        }

        var margins = new PdfCropMargins(
            _cropMarginLeftPt,
            _cropMarginTopPt,
            _cropMarginRightPt,
            _cropMarginBottomPt);

        try
        {
            _status.Text = "Cropping…";
            await RunPageEditAsync(() => _pageEditor.CropPagesAsync(_document, indexes, margins));
            CancelCropMode();
            await ReloadAfterPageEditAsync();
            _status.Text = indexes.Count == 1
                ? "Cropped 1 page."
                : $"Cropped {indexes.Count} pages.";
        }
        catch (Exception ex)
        {
            _status.Text = "Crop failed: " + ex.Message;
        }
    }

    private async Task MergePdfsAsync()
    {
        var window = App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for open picker.");
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".pdf");

        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            _status.Text = "Merge cancelled.";
            return;
        }

        var insertAt = _document.PageCount;
        var opened = new List<IPdfDocument>();
        try
        {
            foreach (var file in files)
            {
                opened.Add(await _documentFactory.OpenAsync(file.Path));
            }

            var before = _document.PageCount;
            _status.Text = files.Count == 1 ? "Merging PDF…" : $"Merging {files.Count} PDFs…";
            await RunPageEditAsync(() => _pageEditor.MergeDocumentsAsync(_document, opened, insertAt));
            await ReloadAfterPageEditAsync();
            var added = _document.PageCount - before;
            _status.Text = added == 1 ? "Merged 1 page." : $"Merged {added} pages.";
            if (added > 0)
            {
                await GoToPageAsync(insertAt, recordHistory: true);
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Merge failed: " + ex.Message;
        }
        finally
        {
            foreach (var doc in opened)
            {
                await doc.DisposeAsync();
            }
        }
    }

    private async Task SplitDocumentAsync()
    {
        // Split before each selected page (excluding page 0). If only page 0 is selected, split every page.
        var selected = SelectedOrCurrentPages();
        var splitBefore = selected.Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
        if (splitBefore.Count == 0)
        {
            if (_document.PageCount < 2)
            {
                _status.Text = "Need at least two pages to split.";
                return;
            }

            splitBefore = Enumerable.Range(1, _document.PageCount - 1).ToList();
        }

        var ranges = PdfSplitRanges.BuildRanges(_document.PageCount, splitBefore);
        if (ranges.Count < 2)
        {
            _status.Text = "Split would produce a single document.";
            return;
        }

        var window = App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for folder picker.");
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            _status.Text = "Split cancelled.";
            return;
        }

        _status.Text = $"Splitting into {ranges.Count} PDFs…";
        var parts = await _pageEditor.SplitDocumentAsync(_document, splitBefore);
        try
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(_document.Path) ?? "Glyph";
            for (var i = 0; i < parts.Count; i++)
            {
                var name = $"{baseName}-part{i + 1}.pdf";
                var file = await folder.CreateFileAsync(name, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                await _pageEditor.SaveAsync(parts[i], file.Path);
            }

            _status.Text = $"Split into {parts.Count} PDFs in {folder.Name}.";
        }
        finally
        {
            foreach (var part in parts)
            {
                await part.DisposeAsync();
            }
        }
    }

    private async Task ExportCroppedAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        if (_cropMode && _cropPageIndex >= 0 && !indexes.Contains(_cropPageIndex))
        {
            indexes = indexes.Append(_cropPageIndex).OrderBy(i => i).ToList();
        }

        var window = App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for save picker.");
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = "Cropped pages";
        picker.FileTypeChoices.Add("PDF", [".pdf"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            _status.Text = "Export cancelled.";
            return;
        }

        try
        {
            _status.Text = "Exporting permanently cropped PDF…";
            await using var extracted = await _pageEditor.ExtractPagesAsync(_document, indexes);

            // Stamp in-progress visual margins onto the exported copy without mutating the open doc.
            if (_cropMode && _cropPageIndex >= 0)
            {
                var local = indexes.IndexOf(_cropPageIndex);
                if (local >= 0)
                {
                    await _pageEditor.CropPagesAsync(
                        extracted,
                        [local],
                        new PdfCropMargins(
                            _cropMarginLeftPt,
                            _cropMarginTopPt,
                            _cropMarginRightPt,
                            _cropMarginBottomPt));
                }
            }

            var partIndexes = Enumerable.Range(0, extracted.PageCount).ToList();
            await _pageEditor.PermanentCropPagesAsync(extracted, partIndexes);
            await _pageEditor.SaveAsync(extracted, file.Path);
            _status.Text = $"Exported cropped PDF to {file.Name}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed: " + ex.Message;
        }
    }

    private async Task CropNumericDialogAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        var unitBox = new ComboBox
        {
            Header = "Units",
            ItemsSource = new[] { "Points (pt)", "Inches (in)", "Centimeters (cm)", "Millimeters (mm)" },
            SelectedIndex = (int)_cropUnit,
            Width = 220,
        };
        var leftBox = new NumberBox
        {
            Header = "Left",
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginLeftPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var topBox = new NumberBox
        {
            Header = "Top",
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginTopPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var rightBox = new NumberBox
        {
            Header = "Right",
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginRightPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var bottomBox = new NumberBox
        {
            Header = "Bottom",
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginBottomPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var allPages = new CheckBox { Content = "Apply to all pages", IsChecked = false };
        var note = new TextBlock
        {
            Text = "Non-destructive CropBox inset. Visual handles remain available from Crop.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.75,
            FontSize = 12,
        };

        void RefreshHeaders()
        {
            var unit = (PdfLengthUnit)Math.Clamp(unitBox.SelectedIndex, 0, 3);
            var abbr = PdfLengthUnits.Abbreviation(unit);
            leftBox.Header = $"Left ({abbr})";
            topBox.Header = $"Top ({abbr})";
            rightBox.Header = $"Right ({abbr})";
            bottomBox.Header = $"Bottom ({abbr})";
        }

        unitBox.SelectionChanged += (_, _) =>
        {
            var previous = _cropUnit;
            var next = (PdfLengthUnit)Math.Clamp(unitBox.SelectedIndex, 0, 3);
            leftBox.Value = PdfLengthUnits.FromPoints(PdfLengthUnits.ToPoints(leftBox.Value, previous), next);
            topBox.Value = PdfLengthUnits.FromPoints(PdfLengthUnits.ToPoints(topBox.Value, previous), next);
            rightBox.Value = PdfLengthUnits.FromPoints(PdfLengthUnits.ToPoints(rightBox.Value, previous), next);
            bottomBox.Value = PdfLengthUnits.FromPoints(PdfLengthUnits.ToPoints(bottomBox.Value, previous), next);
            _cropUnit = next;
            RefreshHeaders();
        };
        RefreshHeaders();

        var dialog = new ContentDialog
        {
            Title = indexes.Count == 1 ? "Crop page" : $"Crop {indexes.Count} pages",
            PrimaryButtonText = "Crop",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { unitBox, leftBox, topBox, rightBox, bottomBox, allPages, note },
            },
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _cropUnit = (PdfLengthUnit)Math.Clamp(unitBox.SelectedIndex, 0, 3);
        var margins = new PdfCropMargins(
            PdfLengthUnits.ToPoints(leftBox.Value, _cropUnit),
            PdfLengthUnits.ToPoints(topBox.Value, _cropUnit),
            PdfLengthUnits.ToPoints(rightBox.Value, _cropUnit),
            PdfLengthUnits.ToPoints(bottomBox.Value, _cropUnit));

        if (_cropMode)
        {
            _cropMarginLeftPt = margins.LeftPoints;
            _cropMarginTopPt = margins.TopPoints;
            _cropMarginRightPt = margins.RightPoints;
            _cropMarginBottomPt = margins.BottomPoints;
            RedrawCropOverlay();
            return;
        }

        if (allPages.IsChecked == true)
        {
            indexes = Enumerable.Range(0, _document.PageCount).ToList();
        }

        try
        {
            _status.Text = "Cropping…";
            await RunPageEditAsync(() => _pageEditor.CropPagesAsync(_document, indexes, margins));
            await ReloadAfterPageEditAsync();
            _status.Text = indexes.Count == 1
                ? "Cropped 1 page."
                : $"Cropped {indexes.Count} pages.";
        }
        catch (Exception ex)
        {
            _status.Text = "Crop failed: " + ex.Message;
        }
    }

    private void BeginCropPointerDrag(Border border, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(border).Position;
        _cropDragHandle = HitTestCropHandle(pos) ?? "move";
        if (_cropDragHandle == "move" && !IsInsideCropRect(pos))
        {
            _cropDragHandle = null;
            return;
        }

        _cropPointerStart = pos;
        _cropDragStartLeft = _cropMarginLeftPt;
        _cropDragStartTop = _cropMarginTopPt;
        _cropDragStartRight = _cropMarginRightPt;
        _cropDragStartBottom = _cropMarginBottomPt;
        border.CapturePointer(e.Pointer);
    }

    private void UpdateCropPointerDrag(Border border, PointerRoutedEventArgs e)
    {
        if (_cropDragHandle is null || _cropPageIndex < 0)
        {
            return;
        }

        var page = _document.GetPage(_cropPageIndex);
        var pos = e.GetCurrentPoint(border).Position;
        var dx = (pos.X - _cropPointerStart.X) / _scale;
        var dy = (pos.Y - _cropPointerStart.Y) / _scale;
        var minSize = 12.0;

        var left = _cropDragStartLeft;
        var top = _cropDragStartTop;
        var right = _cropDragStartRight;
        var bottom = _cropDragStartBottom;

        switch (_cropDragHandle)
        {
            case "move":
                left = ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, _cropDragStartRight, minSize);
                right = ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                top = ClampMargin(_cropDragStartTop + dy, page.HeightPoints, _cropDragStartBottom, minSize);
                bottom = ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                // Keep width/height by shifting as a block.
                left = _cropDragStartLeft + dx;
                right = _cropDragStartRight - dx;
                top = _cropDragStartTop + dy;
                bottom = _cropDragStartBottom - dy;
                if (left < 0) { right += left; left = 0; }
                if (right < 0) { left += right; right = 0; }
                if (top < 0) { bottom += top; top = 0; }
                if (bottom < 0) { top += bottom; bottom = 0; }
                if (left + right > page.WidthPoints - minSize)
                {
                    left = _cropDragStartLeft;
                    right = _cropDragStartRight;
                }

                if (top + bottom > page.HeightPoints - minSize)
                {
                    top = _cropDragStartTop;
                    bottom = _cropDragStartBottom;
                }

                break;
            case "w":
                left = ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, right, minSize);
                break;
            case "e":
                right = ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                break;
            case "n":
                top = ClampMargin(_cropDragStartTop + dy, page.HeightPoints, bottom, minSize);
                break;
            case "s":
                bottom = ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                break;
            case "nw":
                left = ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, right, minSize);
                top = ClampMargin(_cropDragStartTop + dy, page.HeightPoints, bottom, minSize);
                break;
            case "ne":
                right = ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                top = ClampMargin(_cropDragStartTop + dy, page.HeightPoints, bottom, minSize);
                break;
            case "sw":
                left = ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, right, minSize);
                bottom = ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                break;
            case "se":
                right = ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                bottom = ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                break;
        }

        _cropMarginLeftPt = Math.Max(0, left);
        _cropMarginTopPt = Math.Max(0, top);
        _cropMarginRightPt = Math.Max(0, right);
        _cropMarginBottomPt = Math.Max(0, bottom);
        RedrawCropOverlay();
    }

    private static double ClampMargin(double value, double pageExtent, double opposite, double minSize) =>
        Math.Clamp(value, 0, Math.Max(0, pageExtent - opposite - minSize));

    private bool IsInsideCropRect(Windows.Foundation.Point pos)
    {
        if (_cropPageIndex < 0)
        {
            return false;
        }

        var page = _document.GetPage(_cropPageIndex);
        var left = _cropMarginLeftPt * _scale;
        var top = _cropMarginTopPt * _scale;
        var right = (page.WidthPoints - _cropMarginRightPt) * _scale;
        var bottom = (page.HeightPoints - _cropMarginBottomPt) * _scale;
        return pos.X >= left && pos.X <= right && pos.Y >= top && pos.Y <= bottom;
    }

    private string? HitTestCropHandle(Windows.Foundation.Point pos)
    {
        if (_cropPageIndex < 0)
        {
            return null;
        }

        var page = _document.GetPage(_cropPageIndex);
        var left = _cropMarginLeftPt * _scale;
        var top = _cropMarginTopPt * _scale;
        var right = (page.WidthPoints - _cropMarginRightPt) * _scale;
        var bottom = (page.HeightPoints - _cropMarginBottomPt) * _scale;
        const double hit = 10;
        bool Near(double x, double y) => Math.Abs(pos.X - x) <= hit && Math.Abs(pos.Y - y) <= hit;

        if (Near(left, top)) return "nw";
        if (Near(right, top)) return "ne";
        if (Near(left, bottom)) return "sw";
        if (Near(right, bottom)) return "se";
        if (Near(left, (top + bottom) / 2)) return "w";
        if (Near(right, (top + bottom) / 2)) return "e";
        if (Near((left + right) / 2, top)) return "n";
        if (Near((left + right) / 2, bottom)) return "s";
        return null;
    }

    private void RedrawCropOverlay()
    {
        if (!_cropMode || _cropPageIndex < 0 || !_pageOverlays.TryGetValue(_cropPageIndex, out var overlay))
        {
            return;
        }

        var page = _document.GetPage(_cropPageIndex);
        var width = page.WidthPoints * _scale;
        var height = page.HeightPoints * _scale;
        var left = _cropMarginLeftPt * _scale;
        var top = _cropMarginTopPt * _scale;
        var right = (page.WidthPoints - _cropMarginRightPt) * _scale;
        var bottom = (page.HeightPoints - _cropMarginBottomPt) * _scale;
        left = Math.Clamp(left, 0, width);
        top = Math.Clamp(top, 0, height);
        right = Math.Clamp(right, left + 4, width);
        bottom = Math.Clamp(bottom, top + 4, height);

        overlay.Children.Clear();
        overlay.IsHitTestVisible = true;

        void AddDim(double x, double y, double w, double h)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }

            var dim = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = w,
                Height = h,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 0, 0, 0)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(dim, x);
            Canvas.SetTop(dim, y);
            overlay.Children.Add(dim);
        }

        AddDim(0, 0, width, top);
        AddDim(0, bottom, width, height - bottom);
        AddDim(0, top, left, bottom - top);
        AddDim(right, top, width - right, bottom - top);

        var frame = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = right - left,
            Height = bottom - top,
            Stroke = new SolidColorBrush(Colors.Orange),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 165, 0)),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(frame, left);
        Canvas.SetTop(frame, top);
        overlay.Children.Add(frame);

        void AddHandle(double x, double y)
        {
            var handle = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.White),
                Stroke = new SolidColorBrush(Colors.Orange),
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(handle, x - 5);
            Canvas.SetTop(handle, y - 5);
            overlay.Children.Add(handle);
        }

        AddHandle(left, top);
        AddHandle(right, top);
        AddHandle(left, bottom);
        AddHandle(right, bottom);
        AddHandle(left, (top + bottom) / 2);
        AddHandle(right, (top + bottom) / 2);
        AddHandle((left + right) / 2, top);
        AddHandle((left + right) / 2, bottom);
    }

    private async Task ReloadAfterPageEditAsync()
    {
        _cache.ClearDocument(_documentKey);
        _cache.ClearDocument(_thumbnailKey);
        _pageChars.Clear();
        _pageLinks.Clear();
        _pageImages.Clear();
        _pageOverlays.Clear();
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, Math.Max(0, _document.PageCount - 1));

        var stillValid = _pageSelection.SelectedIndexes.Where(i => i < _document.PageCount).ToList();
        _pageSelection.Clear();
        if (stillValid.Count == 0)
        {
            _pageSelection.SelectOnly(CurrentPageIndex);
        }
        else
        {
            foreach (var index in stillValid)
            {
                _pageSelection.Toggle(index);
            }
        }

        BuildPagePlaceholders();
        BuildThumbnailPlaceholders();
        RefreshThumbnailSelectionChrome();
        SyncViewState();
        UpdateStatus();
        await RenderVisibleAsync();
        _ = RenderThumbnailsAsync();
        _ = RefreshAnnotationSidebarAsync();
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
        var nearestPage = CurrentPageIndex;
        for (var i = 0; i < _continuousHost.Children.Count; i++)
        {
            if (_continuousHost.Children[i] is not FrameworkElement fe)
            {
                continue;
            }

            var next = accumulated + fe.ActualHeight + 12;
            if (fe.Tag is int pageIndex && offset < next)
            {
                nearestPage = pageIndex;
                break;
            }

            if (fe.Tag is int stillPage)
            {
                nearestPage = stillPage;
            }

            accumulated = next;
        }

        if (CurrentPageIndex == nearestPage)
        {
            return;
        }

        CurrentPageIndex = nearestPage;
        HighlightThumbnail(nearestPage);
        SyncViewState();
        UpdateStatus();

        // If we scrolled near the edge of the materialized window, rebuild around the new page.
        var (start, end) = ContinuousPageWindow.Around(CurrentPageIndex, _document.PageCount);
        if (!_pageImages.ContainsKey(start) || !_pageImages.ContainsKey(end) || !_pageImages.ContainsKey(CurrentPageIndex))
        {
            BuildPagePlaceholders();
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
