using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Infrastructure.Recovery;
using Glyph.Ocr.Abstractions;
using Glyph.Ocr.Pdf;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Annotations;
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
    private readonly IPdfAnnotationStore _annotationStore;
    private readonly IPdfDocumentFactory _documentFactory;
    private readonly IPdfMetadataService? _metadata;
    private readonly IPdfSecurityService? _security;
    private readonly IPdfRedactionService? _redaction;
    private readonly IPdfOptimizationService? _optimization;
    private readonly ICrashRecoveryStore? _recovery;
    private readonly PdfPageOcrService? _pdfOcr;
    private string _ocrText = string.Empty;
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
    private readonly CheckBox _persistentHighlightBox;
    private readonly ComboBox _markupColorBox;
    private readonly ComboBox _drawToolBox;
    private readonly ComboBox _layoutBox;
    private readonly TextBlock _status;
    private readonly Dictionary<int, IReadOnlyList<PdfTextChar>> _pageChars = new();
    private readonly Dictionary<int, IReadOnlyList<PdfLink>> _pageLinks = new();
    private string _selectedText = string.Empty;
    private int _selectionPageIndex = -1;
    private PdfRect? _selectionRect;
    private IReadOnlyList<PdfAnnotation> _annotations = [];
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
    private readonly List<PdfUserPoint> _inkStroke = [];

    public PdfDocumentView(
        IPdfDocument document,
        IPdfRenderer renderer,
        PageRenderCache cache,
        IPdfTextSearchService searchService,
        IPdfTextExtractor textExtractor,
        IPdfOutlineService outlineService,
        IPdfLinkService linkService,
        IPdfPageEditor pageEditor,
        IPdfAnnotationStore annotationStore,
        IPdfDocumentFactory documentFactory,
        PdfPageOcrService? pdfOcr = null,
        DocumentViewState? viewState = null,
        IPdfMetadataService? metadata = null,
        IPdfSecurityService? security = null,
        IPdfRedactionService? redaction = null,
        IPdfOptimizationService? optimization = null,
        ICrashRecoveryStore? recovery = null)
    {
        _document = document;
        _renderer = renderer;
        _cache = cache;
        _searchCoordinator = new PdfSearchCoordinator(searchService);
        _textExtractor = textExtractor;
        _outlineService = outlineService;
        _linkService = linkService;
        _pageEditor = pageEditor;
        _annotationStore = annotationStore;
        _documentFactory = documentFactory;
        _pdfOcr = pdfOcr;
        _metadata = metadata;
        _security = security;
        _redaction = redaction;
        _optimization = optimization;
        _recovery = recovery;
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
                new RowDefinition { Height = new GridLength(130) },
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
        var annotHeader = new TextBlock { Text = "Annotations", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(8, 8, 8, 4) };
        Grid.SetRow(annotHeader, 6);
        sidePanel.Children.Add(annotHeader);
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
        var highlight = new Button { Content = "HL" };
        var underline = new Button { Content = "U" };
        var strike = new Button { Content = "S" };
        var note = new Button { Content = "Note" };
        var removeMarkup = new Button { Content = "Unmark" };
        var recolor = new Button { Content = "Recolor" };
        var saveDoc = new Button { Content = "Save" };
        var ocrPage = new Button { Content = "OCR" };
        var meta = new Button { Content = "Meta" };
        var securityButton = new Button { Content = "Security" };
        var protect = new Button { Content = "Protect" };
        var markRedact = new Button { Content = "Mark redact" };
        var applyRedact = new Button { Content = "Apply redact" };
        var optimize = new Button { Content = "Optimize" };
        var undoEdit = new Button { Content = "Undo" };
        var redoEdit = new Button { Content = "Redo" };
        _markupColorBox = new ComboBox
        {
            Width = 96,
            ItemsSource = new[] { "Yellow", "Green", "Pink", "Blue", "Red" },
            SelectedIndex = 0,
        };
        _drawToolBox = new ComboBox
        {
            Width = 100,
            ItemsSource = new[] { "Select", "Rect", "Ellipse", "Ink", "Text box" },
            SelectedIndex = 0,
        };
        _persistentHighlightBox = new CheckBox
        {
            Content = "HL mode",
            VerticalAlignment = VerticalAlignment.Center,
        };
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
        ToolTipService.SetToolTip(crop, "Crop selected pages (non-destructive CropBox)");
        ToolTipService.SetToolTip(highlight, "Highlight selected text");
        ToolTipService.SetToolTip(underline, "Underline selected text");
        ToolTipService.SetToolTip(strike, "Strikethrough selected text");
        ToolTipService.SetToolTip(note, "Add sticky note on current page");
        ToolTipService.SetToolTip(removeMarkup, "Delete selected annotation from sidebar");
        ToolTipService.SetToolTip(recolor, "Apply selected color to sidebar annotation");
        ToolTipService.SetToolTip(saveDoc, "Save PDF including markup");
        ToolTipService.SetToolTip(ocrPage, "Offline OCR of the current page (scanned PDFs)");
        ToolTipService.SetToolTip(meta, "View / edit PDF metadata (title, author, subject, keywords)");
        ToolTipService.SetToolTip(securityButton, "Show encryption and permission flags (permissions are advisory)");
        ToolTipService.SetToolTip(protect, "Save a password-protected copy");
        ToolTipService.SetToolTip(markRedact, "Mark selection or a page rectangle for redaction");
        ToolTipService.SetToolTip(applyRedact, "Permanently apply pending rededctions (removes underlying content)");
        ToolTipService.SetToolTip(optimize, "Optimize / re-save PDF with size estimate");
        ToolTipService.SetToolTip(_markupColorBox, "Markup color");
        ToolTipService.SetToolTip(_drawToolBox, "Drawing tool for shapes, ink, and text boxes");
        ToolTipService.SetToolTip(_persistentHighlightBox, "Persistent highlight mode: every text selection is highlighted");
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
        crop.Click += async (_, _) => await CropSelectedAsync();
        highlight.Click += async (_, _) => await ApplyTextMarkupAsync(PdfAnnotationKind.Highlight);
        underline.Click += async (_, _) => await ApplyTextMarkupAsync(PdfAnnotationKind.Underline);
        strike.Click += async (_, _) => await ApplyTextMarkupAsync(PdfAnnotationKind.StrikeOut);
        note.Click += async (_, _) => await AddStickyNoteAsync();
        removeMarkup.Click += async (_, _) => await DeleteSelectedAnnotationAsync();
        recolor.Click += async (_, _) => await RecolorSelectedAnnotationAsync();
        saveDoc.Click += async (_, _) => await SaveDocumentAsync();
        ocrPage.Click += async (_, _) => await RunPageOcrAsync();
        meta.Click += async (_, _) => await EditMetadataAsync();
        securityButton.Click += async (_, _) => await ShowSecurityInfoAsync();
        protect.Click += async (_, _) => await ProtectDocumentAsync();
        markRedact.Click += async (_, _) => await MarkRedactionAsync();
        applyRedact.Click += async (_, _) => await ApplyRedactionsAsync();
        optimize.Click += async (_, _) => await OptimizeDocumentAsync();
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
                zoomOut, zoomIn, fitWidth, fitPage, actual, _layoutBox, copy, saveDoc, ocrPage,
                meta, securityButton, protect, markRedact, applyRedact, optimize,
                undoEdit, redoEdit,
                rotateLeft, rotateRight, deletePages, moveUp, moveDown, insertBlank, duplicate, extract, merge, split, crop,
                highlight, underline, strike, note, removeMarkup, recolor, _markupColorBox, _drawToolBox, _persistentHighlightBox,
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
            var tempPath = Path.Combine(
                Path.GetTempPath(),
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
        var ctrlDown = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrlDown && e.Key == VirtualKey.C)
        {
            await CopyTextAsync();
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
        _inkStroke.Clear();
        if (_drawToolBox.SelectedIndex == 3)
        {
            var page = _document.GetPage(pageIndex);
            _inkStroke.Add(UiToPdfPoint(page, _dragStart));
        }

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
        if (_drawToolBox.SelectedIndex == 3)
        {
            var page = _document.GetPage(pageIndex);
            _inkStroke.Add(UiToPdfPoint(page, current));
            var polyline = new Microsoft.UI.Xaml.Shapes.Polyline
            {
                Stroke = new SolidColorBrush(Colors.IndianRed),
                StrokeThickness = 2,
                Points = new PointCollection(),
            };
            foreach (var pt in _inkStroke)
            {
                polyline.Points.Add(new Windows.Foundation.Point(pt.X * _scale, (page.HeightPoints - pt.Y) * _scale));
            }

            overlay.Children.Add(polyline);
            return;
        }

        var left = Math.Min(_dragStart.X, current.X);
        var top = Math.Min(_dragStart.Y, current.Y);
        var width = Math.Abs(current.X - _dragStart.X);
        var height = Math.Abs(current.Y - _dragStart.Y);
        if (width < 2 || height < 2)
        {
            return;
        }

        if (_drawToolBox.SelectedIndex == 2)
        {
            var ellipse = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
                Stroke = new SolidColorBrush(Colors.DodgerBlue),
                StrokeThickness = 1.5,
            };
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            overlay.Children.Add(ellipse);
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
        var drawTool = _drawToolBox.SelectedIndex;
        _dragSelecting = false;
        border.ReleasePointerCapture(e.Pointer);

        var pdfX = point.Position.X / _scale;
        var pdfY = page.HeightPoints - (point.Position.Y / _scale);
        var dragDistance = Math.Abs(point.Position.X - dragStart.X) + Math.Abs(point.Position.Y - dragStart.Y);

        if (drawTool is 1 or 2 or 3 or 4)
        {
            if (wasDragging && dragDistance >= 4)
            {
                await CommitDrawToolAsync(pageIndex, page, dragStart, point.Position, drawTool);
            }

            return;
        }

        if (!_pageLinks.ContainsKey(pageIndex))
        {
            _pageLinks[pageIndex] = await _linkService.GetPageLinksAsync(_document, pageIndex);
        }

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
            _selectionRect = selection;
            await RefreshSearchHighlightsAsync();
            DrawSelectionOverlay(pageIndex, chars, selection);
            if (_persistentHighlightBox.IsChecked == true && !string.IsNullOrEmpty(_selectedText))
            {
                await ApplyTextMarkupAsync(PdfAnnotationKind.Highlight);
                return;
            }

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

            _selectionRect = union;
            DrawSelectionOverlay(pageIndex, chars, union);
            if (_persistentHighlightBox.IsChecked == true && !string.IsNullOrEmpty(_selectedText))
            {
                await ApplyTextMarkupAsync(PdfAnnotationKind.Highlight);
                return;
            }
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

        var window = App.CurrentApp.MainWindowInstance
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

    private async Task CropSelectedAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        var leftBox = new NumberBox { Header = "Left (pt)", Value = 36, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var topBox = new NumberBox { Header = "Top (pt)", Value = 36, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var rightBox = new NumberBox { Header = "Right (pt)", Value = 36, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var bottomBox = new NumberBox { Header = "Bottom (pt)", Value = 36, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var allPages = new CheckBox
        {
            Content = "Apply to all pages",
            IsChecked = false,
        };
        var note = new TextBlock
        {
            Text = "Non-destructive CropBox inset from each page MediaBox. Undo with Ctrl+Z.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.75,
            FontSize = 12,
        };

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
                Children = { leftBox, topBox, rightBox, bottomBox, allPages, note },
            },
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Crop cancelled.";
            return;
        }

        if (allPages.IsChecked == true)
        {
            indexes = Enumerable.Range(0, _document.PageCount).ToList();
        }

        var margins = new PdfCropMargins(
            leftBox.Value,
            topBox.Value,
            rightBox.Value,
            bottomBox.Value);

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

    private async Task ApplyTextMarkupAsync(PdfAnnotationKind kind)
    {
        if (_selectionPageIndex < 0 || _selectionRect is null || string.IsNullOrEmpty(_selectedText))
        {
            _status.Text = "Select text to mark up.";
            return;
        }

        if (!_pageChars.ContainsKey(_selectionPageIndex))
        {
            _pageChars[_selectionPageIndex] = await _textExtractor.GetCharsAsync(_document, _selectionPageIndex);
        }

        var chars = PdfTextSelection.CharsInRect(_pageChars[_selectionPageIndex], _selectionRect.Value);
        var quads = PdfAnnotationQuads.FromChars(chars);
        if (quads.Count == 0)
        {
            _status.Text = "No text geometry for markup.";
            return;
        }

        try
        {
            await _annotationStore.AddTextMarkupAsync(
                _document,
                new PdfTextMarkupRequest(
                    _selectionPageIndex,
                    kind,
                    quads,
                    SelectedMarkupColor(),
                    SelectedText: _selectedText,
                    Author: "Glyph"));
            _cache.ClearDocument(_documentKey);
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
            _status.Text = kind switch
            {
                PdfAnnotationKind.Highlight => "Highlighted selection.",
                PdfAnnotationKind.Underline => "Underlined selection.",
                PdfAnnotationKind.StrikeOut => "Struck through selection.",
                _ => "Markup applied.",
            };
        }
        catch (Exception ex)
        {
            _status.Text = "Markup failed: " + ex.Message;
        }
    }

    private async Task AddStickyNoteAsync()
    {
        var input = new TextBox
        {
            PlaceholderText = "Note text",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 100,
        };
        var dialog = new ContentDialog
        {
            Title = "Sticky note",
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = input,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Note cancelled.";
            return;
        }

        var text = input.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            _status.Text = "Note text required.";
            return;
        }

        var page = _document.GetPage(CurrentPageIndex);
        try
        {
            await _annotationStore.AddStickyNoteAsync(
                _document,
                new PdfStickyNoteRequest(
                    CurrentPageIndex,
                    page.WidthPoints * 0.1,
                    page.HeightPoints * 0.9,
                    text,
                    SelectedMarkupColor(),
                    Author: "Glyph"));
            _cache.ClearDocument(_documentKey);
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
            _status.Text = "Sticky note added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Note failed: " + ex.Message;
        }
    }

    private async Task DeleteSelectedAnnotationAsync()
    {
        if (_annotationList.SelectedIndex < 0 || _annotationList.SelectedIndex >= _annotations.Count)
        {
            _status.Text = "Select an annotation in the sidebar.";
            return;
        }

        var annot = _annotations[_annotationList.SelectedIndex];
        try
        {
            await _annotationStore.DeleteAsync(_document, annot.PageIndex, annot.AnnotIndex);
            _cache.ClearDocument(_documentKey);
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
            _status.Text = "Annotation removed.";
        }
        catch (Exception ex)
        {
            _status.Text = "Remove failed: " + ex.Message;
        }
    }

    private async Task RecolorSelectedAnnotationAsync()
    {
        if (_annotationList.SelectedIndex < 0 || _annotationList.SelectedIndex >= _annotations.Count)
        {
            _status.Text = "Select an annotation in the sidebar.";
            return;
        }

        var annot = _annotations[_annotationList.SelectedIndex];
        try
        {
            await _annotationStore.SetColorAsync(
                _document,
                annot.PageIndex,
                annot.AnnotIndex,
                SelectedMarkupColor());
            _cache.ClearDocument(_documentKey);
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
            _status.Text = "Annotation color updated.";
        }
        catch (Exception ex)
        {
            _status.Text = "Recolor failed: " + ex.Message;
        }
    }

    private async Task SaveDocumentAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_document.Path))
            {
                await WriteRecoverySnapshotAsync(_document.Path);
                await _pageEditor.SaveAsync(_document, _document.Path);
                await ClearRecoveryForAsync(_document.Path);
                _status.Text = "Saved " + Path.GetFileName(_document.Path);
                return;
            }

            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable for save picker.");
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("PDF", [".pdf"]);
            picker.SuggestedFileName = "document.pdf";
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "Save cancelled.";
                return;
            }

            await WriteRecoverySnapshotAsync(file.Path);
            await _pageEditor.SaveAsync(_document, file.Path);
            await ClearRecoveryForAsync(file.Path);
            _status.Text = "Saved " + file.Name;
        }
        catch (Exception ex)
        {
            _status.Text = "Save failed: " + ex.Message;
        }
    }

    private async Task WriteRecoverySnapshotAsync(string documentPath)
    {
        if (_recovery is null || string.IsNullOrWhiteSpace(documentPath) || !File.Exists(documentPath))
        {
            return;
        }

        await using var stream = File.OpenRead(documentPath);
        await _recovery.SaveSnapshotAsync(documentPath, stream);
    }

    private async Task ClearRecoveryForAsync(string documentPath)
    {
        if (_recovery is null)
        {
            return;
        }

        var entries = await _recovery.ListAsync();
        foreach (var entry in entries.Where(e =>
                     string.Equals(e.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase)))
        {
            await _recovery.DeleteAsync(entry.SnapshotPath);
        }
    }

    private async Task RefreshAnnotationSidebarAsync()
    {
        try
        {
            _annotations = await _annotationStore.ListAsync(_document);
            _annotationList.ItemsSource = _annotations
                .Select(a =>
                {
                    var label = a.Kind.ToString();
                    var snippet = a.Contents ?? a.SelectedText;
                    if (!string.IsNullOrWhiteSpace(snippet))
                    {
                        snippet = snippet.Length > 28 ? snippet[..28] + "…" : snippet;
                        return $"p{a.PageIndex + 1} {label}: {snippet}";
                    }

                    return $"p{a.PageIndex + 1} {label}";
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _status.Text = "Annotation list failed: " + ex.Message;
        }
    }

    private async void AnnotationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_annotationList.SelectedIndex < 0 || _annotationList.SelectedIndex >= _annotations.Count)
        {
            return;
        }

        var annot = _annotations[_annotationList.SelectedIndex];
        await GoToPageAsync(annot.PageIndex, recordHistory: true);
        _status.Text = $"{annot.Kind} on page {annot.PageIndex + 1}.";
    }

    private PdfAnnotationColor SelectedMarkupColor() => _markupColorBox.SelectedIndex switch
    {
        1 => PdfAnnotationColor.Green,
        2 => PdfAnnotationColor.Pink,
        3 => PdfAnnotationColor.Blue,
        4 => PdfAnnotationColor.Red,
        _ => PdfAnnotationColor.Yellow,
    };

    private async Task EditMetadataAsync()
    {
        if (_metadata is null)
        {
            _status.Text = "Metadata service unavailable.";
            return;
        }

        try
        {
            var current = await _metadata.GetAsync(_document);
            var title = new TextBox { Header = "Title", Text = current.Title ?? string.Empty };
            var author = new TextBox { Header = "Author", Text = current.Author ?? string.Empty };
            var subject = new TextBox { Header = "Subject", Text = current.Subject ?? string.Empty };
            var keywords = new TextBox { Header = "Keywords", Text = current.Keywords ?? string.Empty };
            var info = new TextBlock
            {
                Text =
                    $"Pages: {current.PageCount} · Encrypted: {current.IsEncrypted} · " +
                    $"Size: {current.FileSizeBytes?.ToString() ?? "?"} bytes · " +
                    $"Producer: {current.Producer ?? "(none)"}",
                TextWrapping = TextWrapping.WrapWholeWords,
                Opacity = 0.85,
            };
            var dialog = new ContentDialog
            {
                Title = "PDF metadata",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children = { info, title, author, subject, keywords },
                },
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await _metadata.SetAsync(_document, title.Text, author.Text, subject.Text, keywords.Text);
            _status.Text = "Metadata saved (sidecar).";
        }
        catch (Exception ex)
        {
            _status.Text = "Metadata failed: " + ex.Message;
        }
    }

    private async Task ShowSecurityInfoAsync()
    {
        if (_security is null)
        {
            _status.Text = "Security service unavailable.";
            return;
        }

        try
        {
            var info = await _security.GetInfoAsync(_document);
            var dialog = new ContentDialog
            {
                Title = "PDF security",
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
                Content = new TextBlock
                {
                    Text =
                        $"Encrypted: {info.IsEncrypted}\n" +
                        $"Handler revision: {info.SecurityHandlerRevision}\n" +
                        $"Print: {info.CanPrint} · Modify: {info.CanModify} · Copy: {info.CanCopy} · Annotate: {info.CanAnnotate}\n\n" +
                        PdfSecurityInfo.PermissionEnforcementWarning,
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            };
            await dialog.ShowAsync();
            _status.Text = info.IsEncrypted ? "Document is encrypted." : "Document is not encrypted.";
        }
        catch (Exception ex)
        {
            _status.Text = "Security info failed: " + ex.Message;
        }
    }

    private async Task ProtectDocumentAsync()
    {
        if (_security is null)
        {
            _status.Text = "Security service unavailable.";
            return;
        }

        var passwordBox = new PasswordBox { Header = "Open password" };
        var ownerBox = new PasswordBox { Header = "Owner password (optional)" };
        var denyCopy = new CheckBox { Content = "Deny copy", IsChecked = true };
        var dialog = new ContentDialog
        {
            Title = "Protect PDF",
            PrimaryButtonText = "Save protected copy",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = PdfSecurityInfo.PermissionEnforcementWarning,
                        TextWrapping = TextWrapping.WrapWholeWords,
                    },
                    passwordBox,
                    ownerBox,
                    denyCopy,
                },
            },
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (string.IsNullOrEmpty(passwordBox.Password))
        {
            _status.Text = "Password required.";
            return;
        }

        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var window = App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable.");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedFileName = "protected.pdf";
        picker.FileTypeChoices.Add("PDF", [".pdf"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            _status.Text = "Protect cancelled.";
            return;
        }

        try
        {
            var deny = denyCopy.IsChecked == true ? PdfPermissionFlags.DenyCopy : PdfPermissionFlags.None;
            await _security.ProtectAsync(
                _document,
                file.Path,
                passwordBox.Password,
                string.IsNullOrEmpty(ownerBox.Password) ? null : ownerBox.Password,
                deny);
            _status.Text = "Protected copy saved.";
        }
        catch (Exception ex)
        {
            _status.Text = "Protect failed: " + ex.Message;
        }
    }

    private async Task MarkRedactionAsync()
    {
        if (_redaction is null)
        {
            _status.Text = "Redaction service unavailable.";
            return;
        }

        try
        {
            PdfRect bounds;
            if (_selectionRect is { } selected)
            {
                bounds = selected;
            }
            else
            {
                var page = _document.GetPage(CurrentPageIndex);
                // Default mark: centered band on the current page for quick preview/testing.
                bounds = new PdfRect(
                    page.WidthPoints * 0.15,
                    page.HeightPoints * 0.45,
                    page.WidthPoints * 0.85,
                    page.HeightPoints * 0.55);
            }

            var mark = await _redaction.MarkRectAsync(_document, CurrentPageIndex, bounds);
            var pending = await _redaction.ListAsync(_document);
            _status.Text = $"Marked redaction {mark.Id} ({pending.Count} pending). Save, then Apply redact.";
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Mark redaction failed: " + ex.Message;
        }
    }

    private async Task ApplyRedactionsAsync()
    {
        if (_redaction is null)
        {
            _status.Text = "Redaction service unavailable.";
            return;
        }

        var pending = await _redaction.ListAsync(_document);
        if (pending.Count == 0)
        {
            _status.Text = "No pending rededctions.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Apply rededctions permanently?",
            Content =
                $"This removes underlying text/image data under {pending.Count} mark(s) and cannot be undone via Unmark. " +
                "Use page Undo only if you still have an edit snapshot.",
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Apply redaction cancelled.";
            return;
        }

        try
        {
            _status.Text = "Applying rededctions…";
            await _redaction.ApplyAsync(_document);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
            _status.Text = $"Applied {pending.Count} redaction(s). Save the document to persist.";
        }
        catch (Exception ex)
        {
            _status.Text = "Apply redaction failed: " + ex.Message;
        }
    }

    private async Task OptimizeDocumentAsync()
    {
        if (_optimization is null)
        {
            _status.Text = "Optimization service unavailable.";
            return;
        }

        try
        {
            var estimate = await _optimization.EstimateAsync(
                _document,
                new PdfOptimizationOptions(PdfOptimizationPreset.Balanced, RemoveMetadata: false));
            var dialog = new ContentDialog
            {
                Title = "Optimize PDF",
                PrimaryButtonText = "Save optimized copy",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new TextBlock
                {
                    Text =
                        $"Source ≈ {estimate.SourceBytes:N0} bytes\n" +
                        $"Estimated ≈ {estimate.EstimatedBytes:N0} bytes\n" +
                        estimate.Detail,
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable.");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedFileName = "optimized.pdf";
            picker.FileTypeChoices.Add("PDF", [".pdf"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "Optimize cancelled.";
                return;
            }

            await _optimization.OptimizeAsync(
                _document,
                new PdfOptimizationOptions(PdfOptimizationPreset.Balanced),
                file.Path);
            _status.Text = $"Optimized copy saved ({estimate.EstimatedBytes:N0} bytes est.).";
        }
        catch (Exception ex)
        {
            _status.Text = "Optimize failed: " + ex.Message;
        }
    }

    private async Task RunPageOcrAsync()
    {
        if (_pdfOcr is null)
        {
            _status.Text = "OCR service unavailable.";
            return;
        }

        try
        {
            _status.Text = "Running page OCR…";
            var progress = new Progress<OcrProgress>(p => _status.Text = $"OCR {p.Fraction:P0}: {p.Status}");
            var result = await _pdfOcr.RecognizePageAsync(
                _document,
                CurrentPageIndex,
                scale: 2.0,
                progress: progress);
            _ocrText = result.Text;
            var entitySummary = result.Entities.Count == 0
                ? "no entities"
                : string.Join(", ", result.Entities.Take(3).Select(e => $"{e.Kind}:{e.Value}"));
            var preview = string.IsNullOrWhiteSpace(result.Text)
                ? "(no text)"
                : (result.Text.Length > 70 ? result.Text[..70] + "…" : result.Text.Replace('\n', ' '));
            _status.Text = $"OCR p{CurrentPageIndex + 1}: {preview} · {entitySummary}";
            if (!string.IsNullOrWhiteSpace(_ocrText) && !string.IsNullOrWhiteSpace(_searchQuery)
                && _ocrText.Contains(_searchQuery, _searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                _status.Text += " · matches Find query";
            }
        }
        catch (Exception ex)
        {
            _status.Text = "OCR failed: " + ex.Message;
        }
    }

    private static PdfUserPoint UiToPdfPoint(IPdfPage page, Windows.Foundation.Point ui, double scale) =>
        new(ui.X / scale, page.HeightPoints - (ui.Y / scale));

    private PdfUserPoint UiToPdfPoint(IPdfPage page, Windows.Foundation.Point ui) =>
        UiToPdfPoint(page, ui, _scale);

    private async Task CommitDrawToolAsync(
        int pageIndex,
        IPdfPage page,
        Windows.Foundation.Point dragStart,
        Windows.Foundation.Point dragEnd,
        int drawTool)
    {
        try
        {
            if (drawTool == 3)
            {
                if (_inkStroke.Count < 2)
                {
                    _status.Text = "Ink stroke too short.";
                    return;
                }

                await _annotationStore.AddInkAsync(
                    _document,
                    new PdfInkRequest(pageIndex, [_inkStroke.ToList()], SelectedMarkupColor(), StrokeWidth: 2.5f));
                _inkStroke.Clear();
                _status.Text = "Ink stroke added.";
            }
            else
            {
                var left = Math.Min(dragStart.X, dragEnd.X) / _scale;
                var right = Math.Max(dragStart.X, dragEnd.X) / _scale;
                var topUi = Math.Min(dragStart.Y, dragEnd.Y);
                var bottomUi = Math.Max(dragStart.Y, dragEnd.Y);
                var top = page.HeightPoints - (bottomUi / _scale);
                var bottom = page.HeightPoints - (topUi / _scale);
                var bounds = new PdfRect(left, bottom, right, top);

                if (drawTool == 4)
                {
                    var input = new TextBox
                    {
                        PlaceholderText = "Text box contents",
                        Text = "Text",
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        Height = 80,
                    };
                    var dialog = new ContentDialog
                    {
                        Title = "Text box",
                        PrimaryButtonText = "Add",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = XamlRoot,
                        Content = input,
                    };
                    if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    {
                        _status.Text = "Text box cancelled.";
                        return;
                    }

                    await _annotationStore.AddFreeTextAsync(
                        _document,
                        new PdfFreeTextRequest(pageIndex, bounds, input.Text?.Trim() ?? string.Empty, SelectedMarkupColor()));
                    _status.Text = "Text box added.";
                }
                else
                {
                    var kind = drawTool == 2 ? PdfAnnotationKind.Circle : PdfAnnotationKind.Square;
                    await _annotationStore.AddShapeAsync(
                        _document,
                        new PdfShapeRequest(
                            pageIndex,
                            kind,
                            bounds,
                            SelectedMarkupColor(),
                            FillColor: new PdfAnnotationColor(
                                SelectedMarkupColor().R,
                                SelectedMarkupColor().G,
                                SelectedMarkupColor().B,
                                60)));
                    _status.Text = kind == PdfAnnotationKind.Circle ? "Ellipse added." : "Rectangle added.";
                }
            }

            _cache.ClearDocument(_documentKey);
            await RefreshAnnotationSidebarAsync();
            await RenderVisibleAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Draw failed: " + ex.Message;
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
            var baseName = Path.GetFileNameWithoutExtension(_document.Path) ?? "Glyph";
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
