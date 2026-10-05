using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Core.Signatures;
using Glyph.Imaging.Abstractions;
using Glyph.Ocr.Abstractions;
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
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Glyph.App.Views;

/// <summary>
/// PDF viewer: layout modes, zoom, page nav, bitmap thumbnails, Find, and page edits.
/// Visible pages render on demand; distant bitmaps stay outside the LRU cache.
/// </summary>
public sealed class PdfDocumentView : UserControl
{
    private const double ThumbnailWidth = 108;
    private const int OcrMaxEdgePixels = 2048;

    private readonly IPdfDocument _document;
    private readonly IPdfRenderer _renderer;
    private readonly PageRenderCache _cache;
    private readonly PdfSearchCoordinator _searchCoordinator;
    private readonly IPdfTextExtractor _textExtractor;
    private readonly IPdfOutlineService _outlineService;
    private readonly IPdfLinkService _linkService;
    private readonly IPdfPageEditor _pageEditor;
    private readonly IPdfAnnotationService _annotations;
    private readonly IPdfRedactionService _redaction;
    private readonly IPdfDocumentInfoService _documentInfo;
    private readonly IPdfOptimizeService _optimize;
    private readonly IImageEncoder _imageEncoder;
    private readonly ISignatureLibrary _signatures;
    private readonly IPdfFormStore _forms;
    private readonly IPdfDocumentFactory _documentFactory;
    private readonly IOcrEngine? _ocr;
    private readonly Button _ocrCancelButton;
    private CancellationTokenSource? _ocrCts;
    private readonly Dictionary<int, string> _ocrPageTexts = new();
    private readonly Dictionary<int, (OcrResult Result, int SourceWidth, int SourceHeight)> _ocrPageData = new();
    private readonly Dictionary<int, Canvas> _ocrOverlays = new();
    private readonly Dictionary<int, Canvas> _redactionOverlays = new();
    private readonly Dictionary<int, List<(OcrWord Word, Microsoft.UI.Xaml.Shapes.Rectangle Visual)>> _ocrVisualsByPage = new();
    private readonly HashSet<(int PageIndex, int WordIndex)> _selectedOcrIndices = [];
    private readonly Button _copyOcrButton;
    private readonly Button _clearOcrOverlayButton;
    private readonly Button _ocrSavePdfButton;
    private readonly Button _ocrEntitiesButton;
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
    private bool _eraserMode;
    private bool _freeformMode;
    private bool _polygonMode;
    private int _polygonPageIndex = -1;
    private readonly List<PdfPagePoint> _polygonVertices = [];
    private Microsoft.UI.Xaml.Shapes.Polyline? _polygonPreview;
    /// <summary>Recent ink/freeform/polygon strokes for F18-06 stroke undo (Ctrl+Z prefers this).</summary>
    private readonly Stack<PdfAnnotationInfo> _strokeUndoStack = new();
    private PdfAnnotationColor _drawStrokeColor = PdfAnnotationColor.InkRed;
    private float _drawStrokeWidth = 2f;
    private bool _highlightMode;
    private PdfAnnotationColor _highlightModeColor = PdfAnnotationColor.YellowHighlight;
    private bool _formOverlayMode;
    private IReadOnlyList<PdfFormFieldInfo> _formOverlayFields = [];
    private int _formOverlayFocusIndex = -1;
    private readonly List<FrameworkElement> _formOverlayVisuals = [];
    private bool _signatureMode;
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
    private Button? _freeformButton;
    private Button? _polygonButton;
    private Button? _eraserButton;
    private Button? _highlightButton;
    private Button? _formButton;
    private Button? _signButton;
    private Button? _rectButton;
    private Button? _roundRectButton;
    private Button? _hiRectButton;
    private Button? _ellipseButton;
    private Button? _lineButton;
    private Button? _arrowButton;
    private Button? _starButton;
    private Button? _calloutButton;
    private Button? _redactButton;
    private bool _calloutMode;
    private bool _redactionMode;
    private bool _redactionDrawing;
    private int _redactionPageIndex = -1;
    private Windows.Foundation.Point _redactionStart;
    private FrameworkElement? _redactionPreview;
    private bool _dragSelecting;
    private Windows.Foundation.Point _dragStart;
    private int _dragPageIndex = -1;
    private int _regionCopyPageIndex = -1;
    private Windows.Foundation.Rect _regionCopyDisplayRect;
    private PdfAnnotationInfo? _selectedAnnot;
    private bool _annotDragging;
    private PdfRect _annotDragOriginBounds;
    private Windows.Foundation.Point _annotDragOriginUi;
    private string? _annotResizeHandle;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _annotSelectionRect;
    private readonly List<FrameworkElement> _annotResizeHandleVisuals = [];
    /// <summary>In-app annotation clipboard (page/annot index). Cut removes the source on paste.</summary>
    private (int PageIndex, int AnnotIndex)? _annotClipboard;
    private bool _annotClipboardIsCut;
    private string _searchQuery = string.Empty;
    private bool _searchCaseSensitive;
    private bool _cropMode;
    private int _cropPageIndex = -1;
    private double _cropMarginLeftPt;
    private double _cropMarginTopPt;
    private double _cropMarginRightPt;
    private double _cropMarginBottomPt;
    private string? _cropDragHandle;
    private string _annotationAuthor = Environment.UserName;
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
        IPdfRedactionService redaction,
        IPdfDocumentInfoService documentInfo,
        IPdfOptimizeService optimize,
        IImageEncoder imageEncoder,
        ISignatureLibrary signatures,
        IPdfFormStore forms,
        IPdfDocumentFactory documentFactory,
        DocumentViewState? viewState = null,
        Window? ownerWindow = null,
        IOcrEngine? ocr = null)
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
        _redaction = redaction;
        _documentInfo = documentInfo;
        _optimize = optimize;
        _imageEncoder = imageEncoder;
        _signatures = signatures;
        _forms = forms;
        _documentFactory = documentFactory;
        _ocr = ocr;
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
        var findSelection = new Button { Content = "Find sel" };
        findSelection.Click += async (_, _) => await SearchSelectedTextAsync();
        ToolTipService.SetToolTip(findSelection, "Search for the currently selected text");
        var ocrPage = new Button { Content = "OCR" };
        ocrPage.Click += async (_, _) => await OnOcrButtonClickAsync();
        ToolTipService.SetToolTip(ocrPage, "Run offline OCR on selected pages or the entire PDF");
        _ocrCancelButton = new Button { Content = "Cancel OCR", Visibility = Visibility.Collapsed };
        _ocrCancelButton.Click += (_, _) => CancelOcr();
        ToolTipService.SetToolTip(_ocrCancelButton, "Cancel the in-flight OCR job");
        _copyOcrButton = new Button { Content = "Copy OCR", Visibility = Visibility.Collapsed };
        _copyOcrButton.Click += (_, _) => CopySelectedOcrText();
        ToolTipService.SetToolTip(_copyOcrButton, "Copy selected OCR words (or all OCR text on visible pages)");
        _clearOcrOverlayButton = new Button { Content = "Clear OCR", Visibility = Visibility.Collapsed };
        _clearOcrOverlayButton.Click += (_, _) => ClearOcrOverlays();
        ToolTipService.SetToolTip(_clearOcrOverlayButton, "Hide OCR word overlays (keeps Find OCR cache)");
        _ocrSavePdfButton = new Button { Content = "OCR→PDF", Visibility = Visibility.Collapsed };
        _ocrSavePdfButton.Click += async (_, _) => await SaveSearchableOcrPdfAsync();
        ToolTipService.SetToolTip(_ocrSavePdfButton, "Export OCR'd pages as a searchable PDF with invisible text");
        _ocrEntitiesButton = new Button { Content = "Entities", Visibility = Visibility.Collapsed };
        _ocrEntitiesButton.Click += async (_, _) => await ShowOcrEntitiesAsync();
        ToolTipService.SetToolTip(_ocrEntitiesButton, "Review detected URLs, emails, phones, addresses, dates, and times in OCR text");
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
        var duplicateAnnot = new Button { Content = "Dup", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(duplicateAnnot, "Duplicate selected annotation (offset copy)");
        duplicateAnnot.Click += async (_, _) => await DuplicateSelectedAnnotationAsync();
        var copyAnnot = new Button { Content = "Copy", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(copyAnnot, "Copy selected annotation (Ctrl+C when selected)");
        copyAnnot.Click += (_, _) => CopySelectedAnnotationToClipboard();
        var cutAnnot = new Button { Content = "Cut", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(cutAnnot, "Cut selected annotation (Ctrl+X when selected)");
        cutAnnot.Click += (_, _) => CutSelectedAnnotationToClipboard();
        var pasteAnnot = new Button { Content = "Paste", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(pasteAnnot, "Paste annotation clipboard (Ctrl+V when clipboard has an annotation)");
        pasteAnnot.Click += async (_, _) => await PasteAnnotationClipboardAsync();
        var editAnnot = new Button { Content = "Edit", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(editAnnot, "Edit contents of selected sticky note, text box, or callout");
        editAnnot.Click += async (_, _) => await EditSelectedAnnotationContentsAsync();
        var authorAnnot = new Button { Content = "Author", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(authorAnnot, "Set default annotation author name for new sticky notes");
        authorAnnot.Click += async (_, _) => await ConfigureAnnotationAuthorAsync();
        annotHeaderRow.Children.Add(duplicateAnnot);
        annotHeaderRow.Children.Add(editAnnot);
        annotHeaderRow.Children.Add(copyAnnot);
        annotHeaderRow.Children.Add(cutAnnot);
        annotHeaderRow.Children.Add(pasteAnnot);
        annotHeaderRow.Children.Add(authorAnnot);
        var colorAnnot = new Button { Content = "Color", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(colorAnnot, "Change selected annotation color");
        colorAnnot.Click += async (_, _) => await SetSelectedAnnotationColorAsync();
        annotHeaderRow.Children.Add(colorAnnot);
        var opacityAnnot = new Button { Content = "Opacity", Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(opacityAnnot, "Change selected annotation opacity");
        opacityAnnot.Click += async (_, _) => await SetSelectedAnnotationOpacityAsync();
        annotHeaderRow.Children.Add(opacityAnnot);
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
        var callout = new Button { Content = "Callout" };
        var flatten = new Button { Content = "Flatten" };
        var redact = new Button { Content = "Redact" };
        var info = new Button { Content = "Info" };
        var optimize = new Button { Content = "Optimize" };
        var export = new Button { Content = "Export" };
        var sign = new Button { Content = "Sign" };
        var formFill = new Button { Content = "Form" };
        var ink = new Button { Content = "Ink" };
        var freeform = new Button { Content = "Freeform" };
        var polygon = new Button { Content = "Polygon" };
        var eraser = new Button { Content = "Eraser" };
        var rect = new Button { Content = "Rect" };
        var roundRect = new Button { Content = "Round" };
        var hiRect = new Button { Content = "Area" };
        var ellipse = new Button { Content = "Ellipse" };
        var line = new Button { Content = "Line" };
        var arrow = new Button { Content = "Arrow" };
        var star = new Button { Content = "Star" };
        _signButton = sign;
        _inkButton = ink;
        _freeformButton = freeform;
        _polygonButton = polygon;
        _eraserButton = eraser;
        _highlightButton = highlight;
        _formButton = formFill;
        _rectButton = rect;
        _roundRectButton = roundRect;
        _hiRectButton = hiRect;
        _ellipseButton = ellipse;
        _lineButton = line;
        _arrowButton = arrow;
        _starButton = star;
        _calloutButton = callout;
        _redactButton = redact;
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
        ToolTipService.SetToolTip(highlight, "Highlight selected text, or toggle persistent highlight mode");
        ToolTipService.SetToolTip(underline, "Underline selected text");
        ToolTipService.SetToolTip(strikeout, "Strike through selected text");
        ToolTipService.SetToolTip(stickyNote, "Add a sticky note on the current page");
        ToolTipService.SetToolTip(textBox, "Add a FreeText text box on the current page");
        ToolTipService.SetToolTip(callout, "Draw a callout: drag from tip to text box");
        ToolTipService.SetToolTip(flatten, "Flatten annotations into page content (permanent)");
        ToolTipService.SetToolTip(redact, "Mark areas/text for redaction; apply permanently removes content");
        ToolTipService.SetToolTip(info, "Document metadata, encryption, and permissions");
        ToolTipService.SetToolTip(optimize, "Downsample images / shrink PDF (presets)");
        ToolTipService.SetToolTip(export, "Export selected/current page(s) as PNG, JPEG, WebP, TIFF, BMP, GIF, AVIF, or JPEG 2000");
        ToolTipService.SetToolTip(sign, "Signature: draw with mouse or import PNG/JPEG (saved to library)");
        ToolTipService.SetToolTip(formFill, "Form fill: overlay mode or field list (Tab order)");
        ToolTipService.SetToolTip(ink, "Toggle freehand ink drawing on the page");
        ToolTipService.SetToolTip(freeform, "Draw a closed freeform shape (auto-closes path)");
        ToolTipService.SetToolTip(eraser, "Erase annotations by clicking them (ink preferred)");
        ToolTipService.SetToolTip(rect, "Draw a rectangle annotation");
        ToolTipService.SetToolTip(roundRect, "Draw a rounded rectangle annotation");
        ToolTipService.SetToolTip(hiRect, "Draw a translucent highlight rectangle area");
        ToolTipService.SetToolTip(ellipse, "Draw an ellipse annotation");
        ToolTipService.SetToolTip(line, "Draw a line (stored as a 2-point ink stroke)");
        ToolTipService.SetToolTip(arrow, "Draw an arrow (ink shaft + arrowhead)");
        ToolTipService.SetToolTip(undoEdit, "Undo last stroke (if any) or page edit (Ctrl+Z)");
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
        highlight.Click += async (_, _) => await OnHighlightButtonClickAsync();
        underline.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.Underline);
        strikeout.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.StrikeOut);
        stickyNote.Click += async (_, _) => await AddStickyNoteAsync();
        textBox.Click += async (_, _) => await AddTextBoxAsync();
        callout.Click += (_, _) => ToggleCalloutMode();
        flatten.Click += async (_, _) => await FlattenAnnotationsAsync();
        redact.Click += async (_, _) => await OnRedactButtonClickAsync();
        info.Click += async (_, _) => await ShowDocumentInfoAsync();
        optimize.Click += async (_, _) => await ShowOptimizeDialogAsync();
        export.Click += async (_, _) => await ExportPagesAsImagesAsync();
        sign.Click += async (_, _) => await BeginSignatureAsync();
        formFill.Click += async (_, _) => await OnFormButtonClickAsync();
        ink.Click += async (_, _) => await ToggleInkModeAsync();
        freeform.Click += async (_, _) => await ToggleFreeformModeAsync();
        polygon.Click += async (_, _) => await TogglePolygonModeAsync();
        eraser.Click += (_, _) => ToggleEraserMode();
        rect.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.Rectangle);
        roundRect.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.RoundedRectangle);
        hiRect.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.HighlightRectangle);
        ellipse.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.Ellipse);
        line.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.Line);
        arrow.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.Arrow);
        star.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.Star);
        undoEdit.Click += async (_, _) =>
        {
            if (_strokeUndoStack.Count > 0)
            {
                await UndoLastStrokeAsync();
            }
            else
            {
                await UndoPageEditAsync();
            }
        };
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
                highlight, underline, strikeout, stickyNote, textBox, callout, flatten, redact, info, optimize, export, sign, formFill, ink, freeform, polygon, eraser, rect, roundRect, hiRect, ellipse, line, arrow, star,
                _searchBox, _caseSensitiveBox, searchButton, findSelection, ocrPage, _ocrCancelButton, _copyOcrButton, _clearOcrOverlayButton, _ocrSavePdfButton, _ocrEntitiesButton, clearSearch, prevMatch, nextMatch, _status,
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
        _ocrOverlays.Clear();
        _redactionOverlays.Clear();
        _ocrVisualsByPage.Clear();
        _selectedOcrIndices.Clear();
        UpdateOcrOverlayChrome();

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

        var ocrOverlay = new Canvas
        {
            Width = width,
            Height = height,
            IsHitTestVisible = true,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
        };
        _ocrOverlays[pageIndex] = ocrOverlay;

        var redactionOverlay = new Canvas
        {
            Width = width,
            Height = height,
            IsHitTestVisible = false,
        };
        _redactionOverlays[pageIndex] = redactionOverlay;

        var layer = new Grid { Width = width, Height = height };
        layer.Children.Add(image);
        layer.Children.Add(overlay);
        layer.Children.Add(redactionOverlay);
        layer.Children.Add(ocrOverlay);

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
        border.RightTapped += PageBorder_RightTapped;
        border.CanDrag = true;
        border.DragStarting += PageBorder_DragStarting;
        RebuildOcrOverlayForPage(pageIndex);
        RefreshPendingRedactionOverlay(pageIndex);
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

        if (_highlightMode && e.Key == VirtualKey.Escape)
        {
            ClearHighlightMode();
            RefreshToolButtonChrome();
            _status.Text = "Highlight mode off.";
            e.Handled = true;
            return;
        }

        if (_redactionMode && e.Key == VirtualKey.Escape)
        {
            ClearRedactionMode();
            RefreshToolButtonChrome();
            _status.Text = "Redact mode off.";
            e.Handled = true;
            return;
        }

        if (_formOverlayMode && e.Key == VirtualKey.Escape)
        {
            ClearFormOverlayMode();
            RefreshToolButtonChrome();
            _status.Text = "Form overlay off.";
            e.Handled = true;
            return;
        }

        if (_eraserMode && e.Key == VirtualKey.Escape)
        {
            ClearEraserMode();
            RefreshToolButtonChrome();
            _status.Text = "Eraser off.";
            e.Handled = true;
            return;
        }

        if (_polygonMode && e.Key == VirtualKey.Escape)
        {
            ClearPolygonMode();
            RefreshToolButtonChrome();
            _status.Text = "Polygon cancelled.";
            e.Handled = true;
            return;
        }

        if (_polygonMode && e.Key == VirtualKey.Enter)
        {
            await FinishPolygonAsync();
            e.Handled = true;
            return;
        }

        if (_formOverlayMode && e.Key == VirtualKey.Tab)
        {
            var shiftDownTab = Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            await MoveFormOverlayFocusAsync(forward: !shiftDownTab);
            e.Handled = true;
            return;
        }

        if (_formOverlayMode && e.Key == VirtualKey.Enter)
        {
            await EditFocusedFormOverlayFieldAsync();
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
            // Prefer text, then selected annotation, then selected pages.
            if (!string.IsNullOrEmpty(_selectedText))
            {
                await CopyTextAsync();
            }
            else if (TryGetSelectedAnnotation(out _))
            {
                CopySelectedAnnotationToClipboard();
            }
            else
            {
                await CopySelectedPagesAsync();
            }

            e.Handled = true;
            return;
        }

        if (ctrlDown && e.Key == VirtualKey.X)
        {
            if (TryGetSelectedAnnotation(out _))
            {
                CutSelectedAnnotationToClipboard();
                e.Handled = true;
                return;
            }
        }

        if (ctrlDown && e.Key == VirtualKey.V)
        {
            if (_annotClipboard is not null)
            {
                await PasteAnnotationClipboardAsync();
            }
            else
            {
                await PastePagesAsync();
            }

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
            if (_strokeUndoStack.Count > 0)
            {
                await UndoLastStrokeAsync();
            }
            else
            {
                await UndoPageEditAsync();
            }

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
            if (TryGetSelectedAnnotation(out _))
            {
                await RemoveSelectedAnnotationAsync();
            }
            else
            {
                await DeleteSelectedAsync();
            }

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

    private async void PageBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int pageIndex } border)
        {
            return;
        }

        // Reserve right-click for the text selection context menu.
        if (e.GetCurrentPoint(border).Properties.IsRightButtonPressed)
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

        if (_redactionMode)
        {
            BeginRedactionDrag(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        if (_formOverlayMode)
        {
            var formPage = _document.GetPage(pageIndex);
            var formPoint = e.GetCurrentPoint(border).Position;
            var formPdfX = formPoint.X / _scale;
            var formPdfY = formPage.HeightPoints - (formPoint.Y / _scale);
            var formHit = HitTestFormOverlayField(pageIndex, formPdfX, formPdfY);
            if (formHit is not null)
            {
                _ = EditFormOverlayFieldAsync(formHit);
                e.Handled = true;
                return;
            }

            e.Handled = true;
            return;
        }

        if (_inkMode || _freeformMode || _signatureMode)
        {
            BeginInkStroke(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        if (_polygonMode)
        {
            await AddPolygonVertexAsync(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        if (_eraserMode)
        {
            await EraseAnnotationAtAsync(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        if (_shapeMode is not null || _calloutMode)
        {
            BeginShapeDrag(border, pageIndex, e);
            e.Handled = true;
            return;
        }

        var pressPoint = e.GetCurrentPoint(border).Position;
        var page = _document.GetPage(pageIndex);
        var pdfX = pressPoint.X / _scale;
        var pdfY = page.HeightPoints - (pressPoint.Y / _scale);

        if (_selectedAnnot is not null &&
            pageIndex == _selectedAnnot.PageIndex &&
            HitTestAnnotResizeHandle(_selectedAnnot, pressPoint, page) is { } resizeHandle)
        {
            BeginAnnotResize(border, _selectedAnnot, resizeHandle, pressPoint, e);
            e.Handled = true;
            return;
        }

        var hit = PdfAnnotationHitTest.HitTest(_annotationItems, pageIndex, pdfX, pdfY);
        if (hit is not null)
        {
            BeginAnnotDrag(border, hit, pressPoint, e);
            e.Handled = true;
            return;
        }

        ClearAnnotSelectionVisual();
        _selectedAnnot = null;
        _dragSelecting = true;
        _dragPageIndex = pageIndex;
        _dragStart = pressPoint;
        border.CapturePointer(e.Pointer);
    }

    private void PageBorder_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement target)
        {
            return;
        }

        var hasText = !string.IsNullOrWhiteSpace(_selectedText);
        var hasRegion = _regionCopyPageIndex >= 0
            && _regionCopyDisplayRect.Width >= 4
            && _regionCopyDisplayRect.Height >= 4;
        if (!hasText && !hasRegion)
        {
            _status.Text = "Select text or drag a region, then right-click for actions.";
            return;
        }

        var flyout = new MenuFlyout();
        if (hasText)
        {
            var copyItem = new MenuFlyoutItem { Text = "Copy" };
            copyItem.Click += async (_, _) => await CopyTextAsync();
            var findItem = new MenuFlyoutItem { Text = "Find selection" };
            findItem.Click += async (_, _) => await SearchSelectedTextAsync();
            var webItem = new MenuFlyoutItem { Text = "Search web" };
            webItem.Click += async (_, _) => await SearchWebAsync(_selectedText);
            var redactTextItem = new MenuFlyoutItem { Text = "Mark for redaction" };
            redactTextItem.Click += (_, _) => MarkSelectionForRedaction();
            flyout.Items.Add(copyItem);
            flyout.Items.Add(findItem);
            flyout.Items.Add(webItem);
            flyout.Items.Add(redactTextItem);
        }

        if (hasRegion)
        {
            if (flyout.Items.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var imageItem = new MenuFlyoutItem { Text = "Copy region as image" };
            imageItem.Click += async (_, _) => await CopyRegionAsBitmapAsync();
            var redactRegionItem = new MenuFlyoutItem { Text = "Mark region for redaction" };
            redactRegionItem.Click += (_, _) => MarkRegionForRedaction();
            flyout.Items.Add(imageItem);
            flyout.Items.Add(redactRegionItem);
        }

        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void PageBorder_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            args.Cancel = true;
            return;
        }

        args.Data.SetText(_selectedText);
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        _status.Text = "Dragging selected text…";
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

        if (_redactionMode && _redactionDrawing)
        {
            if (sender is Border { Tag: int redactPage } redactBorder && redactPage == _redactionPageIndex)
            {
                ContinueRedactionDrag(redactBorder, e);
                e.Handled = true;
            }

            return;
        }

        if ((_inkMode || _freeformMode || _signatureMode) && _inkDrawing)
        {
            if (sender is Border { Tag: int inkPage } inkBorder && inkPage == _inkPageIndex)
            {
                ContinueInkStroke(inkBorder, e);
                e.Handled = true;
            }

            return;
        }

        if ((_shapeMode is not null || _calloutMode) && _shapeDrawing)
        {
            if (sender is Border { Tag: int shapePage } shapeBorder && shapePage == _shapePageIndex)
            {
                ContinueShapeDrag(shapeBorder, e);
                e.Handled = true;
            }

            return;
        }

        if (_annotDragging)
        {
            if (sender is Border { Tag: int annotPage } annotBorder &&
                _selectedAnnot is not null &&
                annotPage == _selectedAnnot.PageIndex)
            {
                if (_annotResizeHandle is not null)
                {
                    ContinueAnnotResize(annotBorder, e);
                }
                else
                {
                    ContinueAnnotDrag(annotBorder, e);
                }

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

        if (_redactionMode && _redactionDrawing && pageIndex == _redactionPageIndex)
        {
            EndRedactionDrag(border, e);
            e.Handled = true;
            return;
        }

        if ((_inkMode || _freeformMode || _signatureMode) && _inkDrawing && pageIndex == _inkPageIndex)
        {
            if (_signatureMode)
            {
                await EndSignatureStrokeAsync(border, e);
            }
            else
            {
                await EndInkStrokeAsync(border, e);
            }

            e.Handled = true;
            return;
        }

        if ((_shapeMode is not null || _calloutMode) && _shapeDrawing && pageIndex == _shapePageIndex)
        {
            if (_calloutMode)
            {
                await EndCalloutDragAsync(border, e);
            }
            else
            {
                await EndShapeDragAsync(border, e);
            }

            e.Handled = true;
            return;
        }

        if (_annotDragging && _selectedAnnot is not null && pageIndex == _selectedAnnot.PageIndex)
        {
            if (_annotResizeHandle is not null)
            {
                await EndAnnotResizeAsync(border, e);
            }
            else
            {
                await EndAnnotDragAsync(border, e);
            }

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
            var leftUi = Math.Min(dragStart.X, point.Position.X);
            var topUi = Math.Min(dragStart.Y, point.Position.Y);
            var rightUi = Math.Max(dragStart.X, point.Position.X);
            var bottomUi = Math.Max(dragStart.Y, point.Position.Y);
            _regionCopyPageIndex = pageIndex;
            _regionCopyDisplayRect = new Windows.Foundation.Rect(
                leftUi,
                topUi,
                Math.Max(1, rightUi - leftUi),
                Math.Max(1, bottomUi - topUi));

            var left = leftUi / _scale;
            var right = rightUi / _scale;
            var top = page.HeightPoints - (bottomUi / _scale);
            var bottom = page.HeightPoints - (topUi / _scale);
            var selection = new PdfRect(left, bottom, right, top);

            var startPdfX = dragStart.X / _scale;
            var startPdfY = page.HeightPoints - (dragStart.Y / _scale);
            var endPdfX = point.Position.X / _scale;
            var endPdfY = page.HeightPoints - (point.Position.Y / _scale);
            var rectW = selection.Width;
            var rectH = selection.Height;
            // Alt or a wide short-tall drag prefers column/region geometry; otherwise stream across lines.
            var columnMode = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu)
                || (rectW > Math.Max(40, rectH * 1.75) && rectH > 18);

            if (columnMode)
            {
                _selectedText = PdfTextSelection.CopyCharsInRect(chars, selection);
                _selectionPageIndex = pageIndex;
                _selectionQuads = PdfTextMarkupQuads.FromSelectionRect(chars, selection);
                await RefreshSearchHighlightsAsync();
                DrawSelectionOverlay(pageIndex, chars, selection);
            }
            else
            {
                var startIdx = PdfTextSelection.NearestCharIndex(chars, startPdfX, startPdfY);
                var endIdx = PdfTextSelection.NearestCharIndex(chars, endPdfX, endPdfY);
                _selectedText = PdfTextSelection.CopyText(chars, startIdx, endIdx);
                _selectionPageIndex = pageIndex;
                _selectionQuads = PdfTextMarkupQuads.FromIndexRange(chars, startIdx, endIdx);
                await RefreshSearchHighlightsAsync();
                DrawSelectionOverlayFromRange(pageIndex, chars, startIdx, endIdx);
            }

            if (_highlightMode && !string.IsNullOrEmpty(_selectedText))
            {
                await ApplyTextMarkupAsync(PdfTextMarkupKind.Highlight, usePersistentColor: true);
                return;
            }

            _status.Text = string.IsNullOrEmpty(_selectedText)
                ? "Region selected — right-click to copy as image."
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

        if (_highlightMode && !string.IsNullOrEmpty(_selectedText))
        {
            await ApplyTextMarkupAsync(PdfTextMarkupKind.Highlight, usePersistentColor: true);
        }
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

    private void DrawSelectionOverlayFromRange(int pageIndex, IReadOnlyList<PdfTextChar> chars, int startIndex, int endIndexInclusive)
    {
        if (!_pageOverlays.TryGetValue(pageIndex, out var overlay) || chars.Count == 0)
        {
            return;
        }

        var start = Math.Clamp(Math.Min(startIndex, endIndexInclusive), 0, chars.Count - 1);
        var end = Math.Clamp(Math.Max(startIndex, endIndexInclusive), 0, chars.Count - 1);
        var page = _document.GetPage(pageIndex);
        for (var i = start; i <= end; i++)
        {
            AddHighlightRect(overlay, page.HeightPoints, chars[i].Bounds, Windows.UI.Color.FromArgb(70, 30, 144, 255));
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

    private void CancelOcr()
    {
        if (_ocrCts is null)
        {
            return;
        }

        _ocrCts.Cancel();
        _status.Text = "Cancelling OCR…";
    }

    private void BeginOcrJob()
    {
        _ocrCts?.Cancel();
        _ocrCts?.Dispose();
        _ocrCts = new CancellationTokenSource();
        _ocrCancelButton.Visibility = Visibility.Visible;
    }

    private void EndOcrJob()
    {
        _ocrCancelButton.Visibility = Visibility.Collapsed;
        _ocrCts?.Dispose();
        _ocrCts = null;
    }

    private async Task OnOcrButtonClickAsync()
    {
        if (_ocr is null)
        {
            _status.Text = "OCR engine unavailable.";
            return;
        }

        var selected = SelectedOrCurrentPages();
        var selectedLabel = selected.Count == 1
            ? $"Selected / current (page {selected[0] + 1})"
            : $"Selected pages ({selected.Count})";

        var chooser = new ContentDialog
        {
            Title = "OCR",
            Content = new TextBlock
            {
                Text = $"Recognize text offline.\n\n• {selectedLabel}\n• Entire document ({_document.PageCount} pages)",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = selected.Count == 1 ? "Current page" : "Selected pages",
            SecondaryButtonText = "Entire document",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var choice = await chooser.ShowAsync();
        if (choice == ContentDialogResult.Primary)
        {
            await RunOcrPagesAsync(selected);
        }
        else if (choice == ContentDialogResult.Secondary)
        {
            var all = Enumerable.Range(0, _document.PageCount).ToList();
            await RunOcrPagesAsync(all);
        }
    }

    private async Task RunOcrPagesAsync(IReadOnlyList<int> pages)
    {
        if (_ocr is null)
        {
            _status.Text = "OCR engine unavailable.";
            return;
        }

        if (pages.Count == 0)
        {
            _status.Text = "No pages selected for OCR.";
            return;
        }

        BeginOcrJob();
        var token = _ocrCts!.Token;
        try
        {
            var sections = new List<string>(pages.Count);
            var totalLines = 0;
            var totalWords = 0;

            for (var i = 0; i < pages.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var pageIndex = pages[i];
                _status.Text = pages.Count == 1
                    ? $"Running OCR on page {pageIndex + 1}… (1/1)"
                    : $"Running OCR on page {pageIndex + 1} ({i + 1}/{pages.Count})…";

                using var rendered = await _renderer.RenderPageAsync(
                    _document,
                    pageIndex,
                    new PdfRenderRequest(
                        Scale: 4.0,
                        MaxWidthPixels: OcrMaxEdgePixels,
                        MaxHeightPixels: OcrMaxEdgePixels),
                    token);

                token.ThrowIfCancellationRequested();
                var pixels = rendered.Pixels.ToArray();
                var result = await _ocr.RecognizeAsync(
                    new OcrRequest(rendered.Width, rendered.Height, pixels),
                    token);

                totalLines += result.Lines.Count;
                totalWords += result.Lines.Sum(l => l.Words.Count);
                var body = string.IsNullOrWhiteSpace(result.Text) ? "(no text recognized)" : result.Text.Trim();
                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    _ocrPageTexts[pageIndex] = result.Text;
                    _ocrPageData[pageIndex] = (result, rendered.Width, rendered.Height);
                }
                else
                {
                    _ocrPageTexts.Remove(pageIndex);
                    _ocrPageData.Remove(pageIndex);
                }

                RebuildOcrOverlayForPage(pageIndex);

                sections.Add(pages.Count == 1
                    ? body
                    : $"--- Page {pageIndex + 1} ---\n{body}");
            }

            UpdateOcrOverlayChrome();
            var combined = string.Join("\n\n", sections);
            var box = new TextBox
            {
                Text = combined,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Width = 480,
                Height = 320,
            };
            var copy = new Button { Content = "Copy text", Margin = new Thickness(0, 8, 0, 0) };
            copy.Click += (_, _) =>
            {
                var package = new DataPackage();
                package.SetText(combined);
                Clipboard.SetContent(package);
                _status.Text = "OCR text copied.";
            };

            var summary = pages.Count == 1
                ? $"Page {pages[0] + 1} · {totalLines} line(s) · {totalWords} word(s)"
                : $"{pages.Count} pages · {totalLines} line(s) · {totalWords} word(s)";

            var panel = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = summary, Opacity = 0.75 },
                    box,
                    copy,
                },
            };

            var dialog = new ContentDialog
            {
                Title = pages.Count == 1 ? "OCR result" : "OCR results",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
            _status.Text = totalLines == 0
                ? (pages.Count == 1
                    ? $"OCR page {pages[0] + 1} — no text."
                    : $"OCR {pages.Count} pages — no text.")
                : (pages.Count == 1
                    ? $"OCR page {pages[0] + 1} — {totalLines} line(s). Click words to select."
                    : $"OCR {pages.Count} pages — {totalLines} line(s). Click words to select.");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "OCR cancelled.";
        }
        catch (Exception ex)
        {
            _status.Text = "OCR failed: " + ex.Message;
        }
        finally
        {
            EndOcrJob();
        }
    }

    private void RebuildOcrOverlayForPage(int pageIndex)
    {
        if (!_ocrOverlays.TryGetValue(pageIndex, out var overlay))
        {
            return;
        }

        overlay.Children.Clear();
        _ocrVisualsByPage[pageIndex] = [];

        if (!_ocrPageData.TryGetValue(pageIndex, out var data))
        {
            return;
        }

        var displayWidth = (int)Math.Max(1, Math.Round(overlay.Width));
        var displayHeight = (int)Math.Max(1, Math.Round(overlay.Height));
        if (displayWidth <= 0 || displayHeight <= 0 || data.SourceWidth <= 0 || data.SourceHeight <= 0)
        {
            return;
        }

        var visuals = new List<(OcrWord Word, Microsoft.UI.Xaml.Shapes.Rectangle Visual)>();
        foreach (var line in data.Result.Lines)
        {
            foreach (var word in line.Words)
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }

                var mapped = OcrOverlayMapper.MapToDisplay(
                    word,
                    data.SourceWidth,
                    data.SourceHeight,
                    displayWidth,
                    displayHeight);
                var wordIndex = visuals.Count;
                var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = mapped.Width,
                    Height = mapped.Height,
                    Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(55, 0, 120, 215)),
                    Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 0, 120, 215)),
                    StrokeThickness = 1,
                    Tag = (pageIndex, wordIndex),
                };
                Canvas.SetLeft(rect, mapped.X);
                Canvas.SetTop(rect, mapped.Y);
                var capturedIndex = wordIndex;
                rect.PointerPressed += (s, e) =>
                {
                    e.Handled = true;
                    ToggleOcrWordSelection(pageIndex, capturedIndex);
                };
                overlay.Children.Add(rect);
                visuals.Add((word, rect));
            }
        }

        _ocrVisualsByPage[pageIndex] = visuals;
        RefreshOcrSelectionChrome(pageIndex);
    }

    private void ToggleOcrWordSelection(int pageIndex, int wordIndex)
    {
        var key = (pageIndex, wordIndex);
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!ctrl)
        {
            _selectedOcrIndices.Clear();
            foreach (var page in _ocrVisualsByPage.Keys.ToList())
            {
                RefreshOcrSelectionChrome(page);
            }
        }

        if (!_selectedOcrIndices.Add(key))
        {
            _selectedOcrIndices.Remove(key);
        }

        RefreshOcrSelectionChrome(pageIndex);
        UpdateOcrOverlayChrome();
        if (_ocrVisualsByPage.TryGetValue(pageIndex, out var visuals)
            && wordIndex >= 0
            && wordIndex < visuals.Count)
        {
            _status.Text = _selectedOcrIndices.Count == 0
                ? "OCR selection cleared."
                : $"Selected OCR: {visuals[wordIndex].Word.Text}";
        }
    }

    private void RefreshOcrSelectionChrome(int pageIndex)
    {
        if (!_ocrVisualsByPage.TryGetValue(pageIndex, out var visuals))
        {
            return;
        }

        for (var i = 0; i < visuals.Count; i++)
        {
            var selected = _selectedOcrIndices.Contains((pageIndex, i));
            visuals[i].Visual.Fill = new SolidColorBrush(
                selected
                    ? Windows.UI.Color.FromArgb(120, 255, 200, 0)
                    : Windows.UI.Color.FromArgb(55, 0, 120, 215));
            visuals[i].Visual.Stroke = new SolidColorBrush(
                selected
                    ? Windows.UI.Color.FromArgb(220, 255, 170, 0)
                    : Windows.UI.Color.FromArgb(160, 0, 120, 215));
        }
    }

    private void UpdateOcrOverlayChrome()
    {
        var hasOverlay = _ocrPageData.Count > 0;
        _copyOcrButton.Visibility = hasOverlay ? Visibility.Visible : Visibility.Collapsed;
        _clearOcrOverlayButton.Visibility = hasOverlay ? Visibility.Visible : Visibility.Collapsed;
        _ocrSavePdfButton.Visibility = hasOverlay ? Visibility.Visible : Visibility.Collapsed;
        _ocrEntitiesButton.Visibility = hasOverlay ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task SaveSearchableOcrPdfAsync()
    {
        if (_ocrPageData.Count == 0)
        {
            _status.Text = "Run OCR before exporting a searchable PDF.";
            return;
        }

        try
        {
            _status.Text = "Building searchable OCR PDF…";
            var pages = new List<(byte[] ImageBytes, bool IsJpeg, int PixelWidth, int PixelHeight, IEnumerable<SearchablePdfWord> Words)>();
            foreach (var pageIndex in _ocrPageData.Keys.OrderBy(i => i))
            {
                var data = _ocrPageData[pageIndex];
                using var rendered = await _renderer.RenderPageAsync(
                    _document,
                    pageIndex,
                    new PdfRenderRequest(
                        Scale: 4.0,
                        MaxWidthPixels: OcrMaxEdgePixels,
                        MaxHeightPixels: OcrMaxEdgePixels));

                var png = await EncodeBgraPngAsync(rendered.Pixels.ToArray(), rendered.Width, rendered.Height);
                var words = data.Result.Lines
                    .SelectMany(l => l.Words)
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .Select(w => new SearchablePdfWord(w.Text, w.X, w.Y, w.Width, w.Height));
                pages.Add((png, false, rendered.Width, rendered.Height, words));
            }

            var pdfBytes = OcrSearchablePdfWriter.BuildPages(pages);
            var window = _ownerWindow
                ?? App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable for save picker.");
            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = "OCR searchable";
            picker.FileTypeChoices.Add("PDF", [".pdf"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "OCR→PDF cancelled.";
                return;
            }

            await FileIO.WriteBytesAsync(file, pdfBytes);
            _status.Text = $"Saved searchable OCR PDF ({pages.Count} page(s)): {file.Name}";
        }
        catch (Exception ex)
        {
            _status.Text = "OCR→PDF failed: " + ex.Message;
        }
    }

    private static async Task<byte[]> EncodeBgraPngAsync(byte[] bgra, int width, int height)
    {
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)width,
            (uint)height,
            96,
            96,
            bgra);
        await encoder.FlushAsync();
        var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
        var size = (uint)stream.Size;
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private void CopySelectedOcrText()
    {
        string text;
        if (_selectedOcrIndices.Count > 0)
        {
            text = string.Join(
                ' ',
                _selectedOcrIndices
                    .OrderBy(k => k.PageIndex)
                    .ThenBy(k => k.WordIndex)
                    .Select(k =>
                    {
                        if (!_ocrVisualsByPage.TryGetValue(k.PageIndex, out var visuals)
                            || k.WordIndex < 0
                            || k.WordIndex >= visuals.Count)
                        {
                            return null;
                        }

                        return visuals[k.WordIndex].Word.Text;
                    })
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
        }
        else
        {
            text = string.Join(
                "\n\n",
                _ocrPageTexts.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _status.Text = "No OCR text to copy.";
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        _status.Text = _selectedOcrIndices.Count == 0
            ? "All OCR text copied."
            : $"Copied {_selectedOcrIndices.Count} OCR word(s).";
    }

    private void ClearOcrOverlays()
    {
        _selectedOcrIndices.Clear();
        foreach (var overlay in _ocrOverlays.Values)
        {
            overlay.Children.Clear();
        }

        _ocrVisualsByPage.Clear();
        // Keep _ocrPageData / _ocrPageTexts so Find and OCR→PDF still work.
        _copyOcrButton.Visibility = _ocrPageData.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _clearOcrOverlayButton.Visibility = Visibility.Collapsed;
        _ocrSavePdfButton.Visibility = _ocrPageData.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _ocrEntitiesButton.Visibility = _ocrPageData.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _status.Text = "OCR overlays cleared.";
    }

    private async Task ShowOcrEntitiesAsync()
    {
        var text = string.Join("\n", _ocrPageTexts.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        var entities = OcrEntityDetector.Detect(text);
        if (entities.Count == 0)
        {
            _status.Text = "No URLs, emails, phones, addresses, dates, or times detected.";
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 440,
            MaxHeight = 320,
            ItemsSource = entities.Select(e => $"{e.Kind}: {e.Value}").ToList(),
        };
        var copy = new Button { Content = "Copy value", Margin = new Thickness(0, 8, 8, 0) };
        var open = new Button { Content = "Open / act", Margin = new Thickness(0, 8, 8, 0) };
        var searchWeb = new Button { Content = "Search web", Margin = new Thickness(0, 8, 0, 0) };
        copy.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= entities.Count)
            {
                return;
            }

            var package = new DataPackage();
            package.SetText(entities[list.SelectedIndex].Value);
            Clipboard.SetContent(package);
            _status.Text = $"Copied {entities[list.SelectedIndex].Kind}.";
        };
        open.Click += async (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= entities.Count)
            {
                return;
            }

            await ActOnOcrEntityAsync(entities[list.SelectedIndex]);
        };
        searchWeb.Click += async (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= entities.Count)
            {
                return;
            }

            await SearchWebAsync(entities[list.SelectedIndex].Value);
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"{entities.Count} entity(ies) in OCR text", Opacity = 0.75 },
                list,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { copy, open, searchWeb },
                },
            },
        };
        var dialog = new ContentDialog
        {
            Title = "OCR entities",
            Content = panel,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async Task ActOnOcrEntityAsync(OcrEntity entity)
    {
        try
        {
            switch (entity.Kind)
            {
                case OcrEntityKind.Url:
                {
                    var href = entity.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? entity.Value
                        : "https://" + entity.Value;
                    await Launcher.LaunchUriAsync(new Uri(href));
                    _status.Text = "Opened URL.";
                    break;
                }
                case OcrEntityKind.Email:
                    await Launcher.LaunchUriAsync(new Uri("mailto:" + entity.Value));
                    _status.Text = "Opened mail compose.";
                    break;
                case OcrEntityKind.Address:
                {
                    var maps = "https://www.bing.com/maps?q=" + Uri.EscapeDataString(entity.Value);
                    await Launcher.LaunchUriAsync(new Uri(maps));
                    _status.Text = "Opened address in Maps.";
                    break;
                }
                case OcrEntityKind.Date:
                case OcrEntityKind.Time:
                    await CreateCalendarFromOcrAsync(entity);
                    break;
                case OcrEntityKind.Phone:
                default:
                {
                    var package = new DataPackage();
                    package.SetText(entity.Value);
                    Clipboard.SetContent(package);
                    _status.Text = $"Copied {entity.Kind}.";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Entity action failed: " + ex.Message;
        }
    }

    private async Task CreateCalendarFromOcrAsync(OcrEntity entity)
    {
        string ics;
        if (entity.Kind == OcrEntityKind.Date
            && OcrCalendarInvite.TryParseDate(entity.Value, out var date))
        {
            ics = OcrCalendarInvite.BuildAllDayEvent(date, "Glyph OCR: " + entity.Value);
        }
        else if (entity.Kind == OcrEntityKind.Time
            && OcrCalendarInvite.TryParseTime(entity.Value, out var time))
        {
            var start = DateTime.Today.Add(time);
            ics = OcrCalendarInvite.BuildTimedEvent(start, TimeSpan.FromHours(1), "Glyph OCR: " + entity.Value);
        }
        else
        {
            var package = new DataPackage();
            package.SetText(entity.Value);
            Clipboard.SetContent(package);
            _status.Text = $"Copied {entity.Kind} (could not parse for calendar).";
            return;
        }

        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "glyph-ocr-" + Guid.NewGuid().ToString("N") + ".ics");
        await System.IO.File.WriteAllTextAsync(path, ics);
        var file = await StorageFile.GetFileFromPathAsync(path);
        await Launcher.LaunchFileAsync(file);
        _status.Text = "Opened calendar invite.";
    }

    private async Task SearchWebAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _status.Text = "Nothing to search.";
            return;
        }

        try
        {
            var url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(query.Trim());
            await Launcher.LaunchUriAsync(new Uri(url));
            _status.Text = "Opened web search.";
        }
        catch (Exception ex)
        {
            _status.Text = "Search web failed: " + ex.Message;
        }
    }

    private async Task CopyRegionAsBitmapAsync()
    {
        if (_regionCopyPageIndex < 0
            || _regionCopyDisplayRect.Width < 4
            || _regionCopyDisplayRect.Height < 4)
        {
            _status.Text = "Drag a region on the page first.";
            return;
        }

        try
        {
            _status.Text = "Copying region…";
            using var rendered = await _renderer.RenderPageAsync(
                _document,
                _regionCopyPageIndex,
                new PdfRenderRequest(_scale));

            var page = _document.GetPage(_regionCopyPageIndex);
            var scaleX = rendered.Width / Math.Max(1.0, page.WidthPoints * _scale);
            var scaleY = rendered.Height / Math.Max(1.0, page.HeightPoints * _scale);
            var srcX = (int)Math.Floor(_regionCopyDisplayRect.X * scaleX);
            var srcY = (int)Math.Floor(_regionCopyDisplayRect.Y * scaleY);
            var srcW = (int)Math.Ceiling(_regionCopyDisplayRect.Width * scaleX);
            var srcH = (int)Math.Ceiling(_regionCopyDisplayRect.Height * scaleY);
            srcX = Math.Clamp(srcX, 0, Math.Max(0, rendered.Width - 1));
            srcY = Math.Clamp(srcY, 0, Math.Max(0, rendered.Height - 1));
            srcW = Math.Clamp(srcW, 1, rendered.Width - srcX);
            srcH = Math.Clamp(srcH, 1, rendered.Height - srcY);

            var full = rendered.Pixels.ToArray();
            var cropped = new byte[srcW * srcH * 4];
            for (var y = 0; y < srcH; y++)
            {
                var srcOffset = ((srcY + y) * rendered.Width + srcX) * 4;
                var dstOffset = y * srcW * 4;
                Buffer.BlockCopy(full, srcOffset, cropped, dstOffset, srcW * 4);
            }

            var png = await EncodeBgraPngAsync(cropped, srcW, srcH);
            var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var writer = new Windows.Storage.Streams.DataWriter(stream);
            writer.WriteBytes(png);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
            stream.Seek(0);
            var package = new DataPackage();
            package.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(package);
            _status.Text = $"Copied region ({srcW}×{srcH}) to clipboard.";
        }
        catch (Exception ex)
        {
            _status.Text = "Copy region failed: " + ex.Message;
        }
    }

    private async Task SearchSelectedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            _status.Text = "Select text on the page first.";
            return;
        }

        var query = _selectedText.Trim();
        if (query.Length > 200)
        {
            query = query[..200].Trim();
        }

        _searchBox.Text = query;
        await RunSearchAsync();
    }

    public async Task RunExternalFindAsync(string query, int? preferPageIndex = null)
    {
        _searchBox.Text = query ?? string.Empty;
        await RunSearchAsync();
        if (preferPageIndex is int page
            && page >= 0
            && page < _document.PageCount)
        {
            await GoToPageAsync(page, recordHistory: true);
            if (_hits.Count > 0)
            {
                var hitIndex = _hits.ToList().FindIndex(h => h.PageIndex == page);
                if (hitIndex >= 0)
                {
                    _activeHitIndex = hitIndex;
                    _searchResults.SelectedIndex = hitIndex;
                }
            }
        }
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

        var ocrHits = PdfPageTextSearch.Find(_ocrPageTexts, query, _searchCaseSensitive);
        var merged = MergeSearchHits(result.Hits, ocrHits);
        var usedOcr = ocrHits.Count > 0;
        var status = result.Status;
        string? message = result.Message;

        if (merged.Count > 0)
        {
            status = PdfSearchStatus.Success;
            message = usedOcr && result.Hits.Count == 0
                ? $"{merged.Count} OCR match{(merged.Count == 1 ? string.Empty : "es")}"
                : usedOcr
                    ? $"{merged.Count} match{(merged.Count == 1 ? string.Empty : "es")} (incl. OCR)"
                    : null;
        }
        else if (status == PdfSearchStatus.NoExtractableText && _ocrPageTexts.Count == 0)
        {
            message = result.Message ?? "OCR required.";
            if (_ocr is not null && !string.IsNullOrWhiteSpace(query))
            {
                var offer = new ContentDialog
                {
                    Title = "OCR required",
                    Content = "This PDF has no extractable text. Run OCR on the current page so Find can search recognized text?",
                    PrimaryButtonText = "OCR page",
                    CloseButtonText = "Not now",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                };
                if (await offer.ShowAsync() == ContentDialogResult.Primary)
                {
                    await RunOcrPagesAsync([CurrentPageIndex]);
                    if (_ocrPageTexts.ContainsKey(CurrentPageIndex))
                    {
                        await RunSearchAsync();
                        return;
                    }
                }
            }
        }
        else if ((status is PdfSearchStatus.NoExtractableText or PdfSearchStatus.NoMatches)
                 && _ocrPageTexts.Count > 0
                 && !string.IsNullOrWhiteSpace(query))
        {
            status = PdfSearchStatus.NoMatches;
            message = "No matches in document text or OCR cache.";
        }

        _hits = merged;
        _activeHitIndex = _hits.Count > 0 ? 0 : -1;
        if (status is PdfSearchStatus.EmptyQuery or PdfSearchStatus.NoMatches or PdfSearchStatus.NoExtractableText)
        {
            _searchQuery = string.Empty;
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Clear();
            }
        }
        _searchResults.ItemsSource = _hits
            .Select(h =>
            {
                var ocrTag = ocrHits.Any(o =>
                    o.PageIndex == h.PageIndex
                    && o.MatchStart == h.MatchStart
                    && o.MatchLength == h.MatchLength)
                    ? " [OCR]"
                    : string.Empty;
                return $"p.{h.PageIndex + 1}{ocrTag}: {h.Snippet}";
            })
            .ToList();

        _status.Text = status switch
        {
            PdfSearchStatus.EmptyQuery => message ?? "Enter search text.",
            PdfSearchStatus.NoMatches => message ?? "No matches.",
            PdfSearchStatus.NoExtractableText => message ?? "OCR required.",
            PdfSearchStatus.DocumentEncrypted => message ?? "Password required.",
            PdfSearchStatus.Failed => message ?? "Search failed.",
            PdfSearchStatus.Success => message ?? $"{_hits.Count} match{(_hits.Count == 1 ? string.Empty : "es")}",
            _ => message ?? _status.Text,
        };

        if (_activeHitIndex >= 0)
        {
            _searchResults.SelectedIndex = _activeHitIndex;
            await GoToPageAsync(_hits[_activeHitIndex].PageIndex, recordHistory: true);
        }

        await RefreshSearchHighlightsAsync();
    }

    private static IReadOnlyList<PdfSearchHit> MergeSearchHits(
        IReadOnlyList<PdfSearchHit> nativeHits,
        IReadOnlyList<PdfSearchHit> ocrHits)
    {
        if (ocrHits.Count == 0)
        {
            return nativeHits;
        }

        if (nativeHits.Count == 0)
        {
            return ocrHits;
        }

        var seen = new HashSet<(int Page, int Start, int Length)>();
        var merged = new List<PdfSearchHit>(nativeHits.Count + ocrHits.Count);
        foreach (var hit in nativeHits.Concat(ocrHits).OrderBy(h => h.PageIndex).ThenBy(h => h.MatchStart))
        {
            var key = (hit.PageIndex, hit.MatchStart, hit.MatchLength);
            if (!seen.Add(key))
            {
                continue;
            }

            merged.Add(hit);
        }

        return merged;
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

    private async Task OnHighlightButtonClickAsync()
    {
        // One-shot: selection present and mode off → apply once with color picker.
        if (!_highlightMode
            && _selectionPageIndex >= 0
            && _selectionQuads.Count > 0
            && !string.IsNullOrEmpty(_selectedText))
        {
            await ApplyTextMarkupAsync(PdfTextMarkupKind.Highlight);
            return;
        }

        await ToggleHighlightModeAsync();
    }

    private async Task ToggleHighlightModeAsync()
    {
        ClearRedactionMode();
        if (_highlightMode)
        {
            ClearHighlightMode();
            RefreshToolButtonChrome();
            _status.Text = "Highlight mode off.";
            return;
        }

        var picked = await PickHighlightColorAsync();
        if (picked is null)
        {
            _status.Text = "Highlight mode cancelled.";
            return;
        }

        ClearShapeMode();
        ClearSignatureMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        ClearCalloutMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        _highlightMode = true;
        _highlightModeColor = picked.Value;
        RefreshToolButtonChrome();
        _status.Text = "Highlight mode on — select text to highlight (Esc to exit).";

        if (_selectionPageIndex >= 0
            && _selectionQuads.Count > 0
            && !string.IsNullOrEmpty(_selectedText))
        {
            await ApplyTextMarkupAsync(PdfTextMarkupKind.Highlight, usePersistentColor: true);
        }
    }

    private void ClearHighlightMode()
    {
        _highlightMode = false;
    }

    private async Task ApplyTextMarkupAsync(PdfTextMarkupKind kind, bool usePersistentColor = false)
    {
        if (_selectionPageIndex < 0 || _selectionQuads.Count == 0 || string.IsNullOrEmpty(_selectedText))
        {
            _status.Text = _highlightMode && kind == PdfTextMarkupKind.Highlight
                ? "Highlight mode on — select text to highlight."
                : "Select text first, then apply markup.";
            return;
        }

        PdfAnnotationColor color;
        if (kind == PdfTextMarkupKind.Highlight)
        {
            if (usePersistentColor || _highlightMode)
            {
                color = _highlightModeColor;
            }
            else
            {
                var picked = await PickHighlightColorAsync();
                if (picked is null)
                {
                    _status.Text = "Highlight cancelled.";
                    return;
                }

                color = picked.Value;
                _highlightModeColor = color;
            }
        }
        else
        {
            color = kind switch
            {
                PdfTextMarkupKind.Underline => PdfAnnotationColor.UnderlineBlue,
                PdfTextMarkupKind.StrikeOut => PdfAnnotationColor.StrikeOutRed,
                _ => PdfAnnotationColor.YellowHighlight,
            };
        }

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
                PdfTextMarkupKind.Highlight => _highlightMode
                    ? "Highlight added — select more text, or Esc to exit mode."
                    : "Highlight added.",
                PdfTextMarkupKind.Underline => "Underline added.",
                _ => "Strikethrough added.",
            };
        }
        catch (Exception ex)
        {
            _status.Text = "Markup failed: " + ex.Message;
        }
    }

    private async Task<PdfAnnotationColor?> PickHighlightColorAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for color dialog.");

        var list = new ListView
        {
            Height = 220,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.HighlightPresets.Select(p => p.Name).ToList(),
        };
        list.SelectedIndex = 0;
        var dialog = new ContentDialog
        {
            Title = "Highlight color",
            Content = list,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var index = Math.Clamp(list.SelectedIndex, 0, PdfAnnotationColor.HighlightPresets.Count - 1);
        return PdfAnnotationColor.HighlightPresets[index].Color;
    }

    private async Task SetSelectedAnnotationColorAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for color dialog.");

        var index = _annotationList.SelectedIndex;
        PdfAnnotationInfo? item = index >= 0 && index < _annotationItems.Count
            ? _annotationItems[index]
            : _selectedAnnot;
        if (item is null)
        {
            _status.Text = "Select an annotation to change color.";
            return;
        }

        PdfAnnotationColor color;
        if (item.TextMarkupKind == PdfTextMarkupKind.Highlight)
        {
            var picked = await PickHighlightColorAsync();
            if (picked is null)
            {
                return;
            }

            color = picked.Value;
        }
        else if (item.IsStickyNote)
        {
            var list = new ListView
            {
                Height = 220,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = PdfAnnotationColor.StickyNotePresets.Select(p => p.Name).ToList(),
            };
            list.SelectedIndex = 0;
            var dialog = new ContentDialog
            {
                Title = $"Note color — {FormatAnnotationLabel(item)}",
                Content = list,
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            color = PdfAnnotationColor.StickyNotePresets[
                Math.Clamp(list.SelectedIndex, 0, PdfAnnotationColor.StickyNotePresets.Count - 1)].Color;
        }
        else
        {
            // Simple presets for non-highlight annots.
            var presets = new (string Name, PdfAnnotationColor Color)[]
            {
                ("Dodger blue", new PdfAnnotationColor(30, 144, 255)),
                ("Red", new PdfAnnotationColor(220, 50, 50)),
                ("Green", new PdfAnnotationColor(40, 160, 60)),
                ("Orange", new PdfAnnotationColor(255, 140, 0)),
                ("Purple", new PdfAnnotationColor(140, 60, 200)),
            };
            var list = new ListView
            {
                Height = 220,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = presets.Select(p => p.Name).ToList(),
            };
            list.SelectedIndex = 0;
            var dialog = new ContentDialog
            {
                Title = $"Color — {FormatAnnotationLabel(item)}",
                Content = list,
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            color = presets[Math.Clamp(list.SelectedIndex, 0, presets.Length - 1)].Color;
        }

        try
        {
            await _annotations.SetColorAsync(_document, item.PageIndex, item.AnnotIndex, color);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Annotation color updated.";
        }
        catch (Exception ex)
        {
            _status.Text = "Color failed: " + ex.Message;
        }
    }

    private async Task RefreshAnnotationSidebarAsync()
    {
        try
        {
            var all = await _annotations.ListAsync(_document);
            _annotationItems = all
                .Where(a =>
                    (a.TextMarkupKind is not null || a.IsStickyNote || a.IsInk || a.ShapeKind is not null || a.IsTextBox || a.IsStamp)
                    && !(a.IsInk && a.Contents == "CalloutPointer"))
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
        if (info.IsCallout)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimForStatus(info.Contents);
            return $"Callout · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsStickyNote)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimForStatus(info.Contents);
            var author = string.IsNullOrWhiteSpace(info.Author) ? string.Empty : $" · {info.Author}";
            return $"Note{author} · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsTextBox)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimForStatus(info.Contents);
            return $"Text · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsStamp)
        {
            return $"Signature · p.{info.PageIndex + 1}";
        }

        if (info.ShapeKind is { } shape)
        {
            var shapeName = shape switch
            {
                PdfShapeKind.Rectangle => "Rect",
                PdfShapeKind.RoundedRectangle => "Round",
                PdfShapeKind.HighlightRectangle => "Area",
                PdfShapeKind.Ellipse => "Ellipse",
                PdfShapeKind.Line => "Line",
                PdfShapeKind.Arrow => "Arrow",
                PdfShapeKind.Freeform => "Freeform",
                PdfShapeKind.Star => "Star",
                PdfShapeKind.Polygon => "Polygon",
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

    private void ToggleCalloutMode()
    {
        ClearRedactionMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_calloutMode)
        {
            _calloutMode = false;
            CancelShapeDrag();
            _status.Text = "Callout mode off.";
            RefreshToolButtonChrome();
            return;
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        CancelShapeDrag();
        _calloutMode = true;
        RefreshToolButtonChrome();
        _status.Text = "Callout mode — drag from tip to where the text box should sit.";
    }

    private async Task EndCalloutDragAsync(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        ContinueShapeDrag(border, e);

        var pageIndex = _shapePageIndex;
        var start = _shapeStart;
        var end = e.GetCurrentPoint(border).Position;
        CancelShapeDrag();

        if (pageIndex < 0)
        {
            return;
        }

        var page = _document.GetPage(pageIndex);
        double ToPdfX(double x) => x / _scale;
        double ToPdfY(double y) => page.HeightPoints - (y / _scale);
        var tip = new PdfPagePoint(ToPdfX(start.X), ToPdfY(start.Y));
        var boxCenterX = ToPdfX(end.X);
        var boxCenterY = ToPdfY(end.Y);
        var boxWidth = Math.Min(180, page.WidthPoints * 0.4);
        var boxHeight = 56;
        var left = Math.Clamp(boxCenterX - (boxWidth / 2), 8, Math.Max(8, page.WidthPoints - boxWidth - 8));
        var bottom = Math.Clamp(boxCenterY - (boxHeight / 2), 8, Math.Max(8, page.HeightPoints - boxHeight - 8));
        var textBounds = new PdfRect(left, bottom, left + boxWidth, bottom + boxHeight);

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for callout dialog.");
        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 100,
            PlaceholderText = "Callout text",
        };
        var fontSizeBox = new NumberBox
        {
            Header = "Font size (pt)",
            Value = 12,
            Minimum = 6,
            Maximum = 72,
            SmallChange = 1,
            LargeChange = 2,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var fontFamilyBox = new ComboBox
        {
            Header = "Font",
            ItemsSource = new[] { "Helvetica", "Times", "Courier" },
            SelectedIndex = 0,
            Width = 220,
        };
        var boldCheck = new CheckBox { Content = "Bold", IsChecked = false };
        var italicCheck = new CheckBox { Content = "Italic", IsChecked = false };
        var textColorList = new ListView
        {
            Height = 100,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.StrokePresets.Select(p => p.Name).ToList(),
            SelectedIndex = 5, // Black
        };
        var panel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                box,
                fontSizeBox,
                fontFamilyBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { boldCheck, italicCheck },
                },
                new TextBlock { Text = "Text color", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                textColorList,
            },
        };
        var dialog = new ContentDialog
        {
            Title = "Callout",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Callout cancelled.";
            return;
        }

        var textColor = PdfAnnotationColor.StrokePresets[
            Math.Clamp(textColorList.SelectedIndex, 0, PdfAnnotationColor.StrokePresets.Count - 1)].Color;
        var fontSize = (float)(double.IsNaN(fontSizeBox.Value) ? 12 : Math.Clamp(fontSizeBox.Value, 6, 72));
        var fontResource = PdfFreeTextFont.ResolveResourceName(
            (PdfFreeTextFontFamily)Math.Clamp(fontFamilyBox.SelectedIndex, 0, 2),
            boldCheck.IsChecked == true,
            italicCheck.IsChecked == true);

        try
        {
            _status.Text = "Adding callout…";
            await _annotations.AddCalloutAsync(
                _document,
                pageIndex,
                textBounds,
                tip,
                box.Text ?? string.Empty,
                textColor,
                borderColor: _drawStrokeColor,
                fillColor: new PdfAnnotationColor(255, 255, 230),
                fontSizePoints: fontSize,
                fontResourceName: fontResource,
                pointerWidthPoints: _drawStrokeWidth);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Callout added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Callout failed: " + ex.Message;
        }
    }

    private async Task ToggleInkModeAsync()
    {
        ClearRedactionMode();
        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearFreeformMode();
        ClearPolygonMode();
        ClearEraserMode();
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
            _status.Text = "Ink mode off.";
            RefreshToolButtonChrome();
            return;
        }

        var picked = await PickStrokeStyleAsync("Ink stroke");
        if (picked is null)
        {
            _status.Text = "Ink mode cancelled.";
            return;
        }

        _drawStrokeColor = picked.Value.Color;
        _drawStrokeWidth = picked.Value.WidthPoints;
        if (_cropMode)
        {
            CancelCropMode();
        }

        _inkMode = true;
        RefreshToolButtonChrome();
        _status.Text = "Ink mode on — draw on the page.";
    }

    private void ToggleEraserMode()
    {
        ClearRedactionMode();
        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_eraserMode)
        {
            ClearEraserMode();
            _status.Text = "Eraser off.";
            RefreshToolButtonChrome();
            return;
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        _eraserMode = true;
        ClearAnnotSelectionVisual();
        _selectedAnnot = null;
        RefreshToolButtonChrome();
        _status.Text = "Eraser on — click an annotation to remove it (Esc to exit).";
    }

    private void ClearEraserMode()
    {
        _eraserMode = false;
    }

    private async Task EraseAnnotationAtAsync(Border border, int pageIndex, PointerRoutedEventArgs e)
    {
        var pressPoint = e.GetCurrentPoint(border).Position;
        var page = _document.GetPage(pageIndex);
        var pdfX = pressPoint.X / _scale;
        var pdfY = page.HeightPoints - (pressPoint.Y / _scale);
        const double pad = 8.0;

        var onPage = _annotationItems.Where(a => a.PageIndex == pageIndex).ToList();
        // Prefer ink strokes (F18-05), then any annotation under/near the cursor.
        var hit = HitWithPad(onPage.Where(a => a.IsInk), pdfX, pdfY, pad)
            ?? HitWithPad(onPage, pdfX, pdfY, pad);

        if (hit is null)
        {
            _status.Text = "Eraser: no annotation under cursor.";
            return;
        }

        try
        {
            await _annotations.RemoveAsync(_document, hit.PageIndex, hit.AnnotIndex);
            if (_annotClipboard is { } clip
                && clip.PageIndex == hit.PageIndex
                && clip.AnnotIndex == hit.AnnotIndex)
            {
                _annotClipboard = null;
                _annotClipboardIsCut = false;
            }

            ClearAnnotSelectionVisual();
            _selectedAnnot = null;
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = $"Erased {FormatAnnotationLabel(hit)}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Eraser failed: " + ex.Message;
        }
    }

    private static PdfAnnotationInfo? HitWithPad(
        IEnumerable<PdfAnnotationInfo> annotations,
        double xPoints,
        double yPoints,
        double pad)
    {
        PdfAnnotationInfo? hit = null;
        foreach (var annot in annotations)
        {
            var b = annot.Bounds;
            var left = Math.Min(b.Left, b.Right) - pad;
            var right = Math.Max(b.Left, b.Right) + pad;
            var bottom = Math.Min(b.Bottom, b.Top) - pad;
            var top = Math.Max(b.Bottom, b.Top) + pad;
            if (xPoints < left || xPoints > right || yPoints < bottom || yPoints > top)
            {
                continue;
            }

            if (hit is null || annot.AnnotIndex >= hit.AnnotIndex)
            {
                hit = annot;
            }
        }

        return hit;
    }

    private async Task ToggleFreeformModeAsync()
    {
        ClearRedactionMode();
        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearEraserMode();
        ClearPolygonMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_freeformMode)
        {
            ClearFreeformMode();
            _status.Text = "Freeform mode off.";
            RefreshToolButtonChrome();
            return;
        }

        var picked = await PickStrokeStyleAsync("Freeform stroke");
        if (picked is null)
        {
            _status.Text = "Freeform mode cancelled.";
            return;
        }

        _drawStrokeColor = picked.Value.Color;
        _drawStrokeWidth = picked.Value.WidthPoints;
        if (_cropMode)
        {
            CancelCropMode();
        }

        _freeformMode = true;
        RefreshToolButtonChrome();
        _status.Text = "Freeform mode on — draw a closed shape.";
    }

    private void ClearFreeformMode()
    {
        if (!_freeformMode)
        {
            return;
        }

        _freeformMode = false;
        CancelInkStroke();
    }

    private async Task TogglePolygonModeAsync()
    {
        ClearRedactionMode();
        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearEraserMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_polygonMode)
        {
            ClearPolygonMode();
            _status.Text = "Polygon mode off.";
            RefreshToolButtonChrome();
            return;
        }

        var picked = await PickStrokeStyleAsync("Polygon stroke");
        if (picked is null)
        {
            _status.Text = "Polygon mode cancelled.";
            return;
        }

        _drawStrokeColor = picked.Value.Color;
        _drawStrokeWidth = picked.Value.WidthPoints;
        if (_cropMode)
        {
            CancelCropMode();
        }

        _polygonMode = true;
        _polygonPageIndex = -1;
        _polygonVertices.Clear();
        ClearPolygonPreview();
        RefreshToolButtonChrome();
        _status.Text = "Polygon mode — click vertices; Enter to close (Esc cancels).";
    }

    private void ClearPolygonMode()
    {
        if (!_polygonMode && _polygonVertices.Count == 0 && _polygonPreview is null)
        {
            return;
        }

        _polygonMode = false;
        _polygonPageIndex = -1;
        _polygonVertices.Clear();
        ClearPolygonPreview();
    }

    private void ClearPolygonPreview()
    {
        if (_polygonPreview is not null &&
            _polygonPageIndex >= 0 &&
            _pageOverlays.TryGetValue(_polygonPageIndex, out var overlay))
        {
            overlay.Children.Remove(_polygonPreview);
        }

        _polygonPreview = null;
    }

    private async Task AddPolygonVertexAsync(Border border, int pageIndex, PointerRoutedEventArgs e)
    {
        var page = _document.GetPage(pageIndex);
        var ui = e.GetCurrentPoint(border).Position;
        var pdf = new PdfPagePoint(ui.X / _scale, page.HeightPoints - (ui.Y / _scale));

        if (_polygonPageIndex >= 0 && _polygonPageIndex != pageIndex)
        {
            _status.Text = "Finish the current page polygon first (Enter), or Esc to cancel.";
            return;
        }

        if (_polygonVertices.Count >= 3)
        {
            var first = _polygonVertices[0];
            var dx = pdf.X - first.X;
            var dy = pdf.Y - first.Y;
            if (((dx * dx) + (dy * dy)) <= 64) // within ~8 pt of first vertex → close
            {
                await FinishPolygonAsync();
                return;
            }
        }

        _polygonPageIndex = pageIndex;
        _polygonVertices.Add(pdf);
        RedrawPolygonPreview();
        _status.Text = _polygonVertices.Count < 3
            ? $"Polygon vertex {_polygonVertices.Count} — need at least 3."
            : $"Polygon vertex {_polygonVertices.Count} — Enter to close, or click near first vertex.";
    }

    private void RedrawPolygonPreview()
    {
        ClearPolygonPreview();
        if (_polygonPageIndex < 0 ||
            _polygonVertices.Count == 0 ||
            !_pageOverlays.TryGetValue(_polygonPageIndex, out var overlay))
        {
            return;
        }

        var page = _document.GetPage(_polygonPageIndex);
        var points = new PointCollection();
        foreach (var p in _polygonVertices)
        {
            points.Add(new Windows.Foundation.Point(p.X * _scale, (page.HeightPoints - p.Y) * _scale));
        }

        if (_polygonVertices.Count >= 3)
        {
            var first = _polygonVertices[0];
            points.Add(new Windows.Foundation.Point(first.X * _scale, (page.HeightPoints - first.Y) * _scale));
        }

        var stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(
            _drawStrokeColor.A,
            _drawStrokeColor.R,
            _drawStrokeColor.G,
            _drawStrokeColor.B));
        _polygonPreview = new Microsoft.UI.Xaml.Shapes.Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = Math.Max(1, _drawStrokeWidth * _scale / 1.5),
            Fill = null,
        };
        overlay.Children.Add(_polygonPreview);
    }

    private async Task FinishPolygonAsync()
    {
        if (!_polygonMode)
        {
            return;
        }

        if (_polygonVertices.Count < 3 || _polygonPageIndex < 0)
        {
            _status.Text = "Polygon needs at least three vertices.";
            return;
        }

        var pageIndex = _polygonPageIndex;
        var vertices = _polygonVertices.ToList();
        var color = _drawStrokeColor;
        var width = _drawStrokeWidth;
        ClearPolygonMode();
        RefreshToolButtonChrome();

        try
        {
            _status.Text = "Saving polygon…";
            await _annotations.AddPolygonAsync(_document, pageIndex, vertices, color, width);
            // Re-list to capture the created annot for stroke undo.
            var listed = await _annotations.ListAsync(_document, pageIndex);
            var created = listed.LastOrDefault(a => a.ShapeKind == PdfShapeKind.Polygon && a.IsInk);
            if (created is not null)
            {
                _strokeUndoStack.Push(created);
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Polygon added.";
        }
        catch (Exception ex)
        {
            _status.Text = "Polygon failed: " + ex.Message;
        }
    }

    private async Task ToggleShapeModeAsync(PdfShapeKind kind)
    {
        ClearRedactionMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        ClearFreeformMode();
        ClearPolygonMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearEraserMode();
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_shapeMode == kind)
        {
            ClearShapeMode();
            _status.Text = "Shape mode off.";
            RefreshToolButtonChrome();
            return;
        }

        if (kind == PdfShapeKind.HighlightRectangle)
        {
            var hiColor = await PickHighlightColorAsync();
            if (hiColor is null)
            {
                _status.Text = "Area highlight cancelled.";
                return;
            }

            _drawStrokeColor = hiColor.Value;
            _drawStrokeWidth = 0.5f;
        }
        else
        {
            var picked = await PickStrokeStyleAsync("Shape stroke");
            if (picked is null)
            {
                _status.Text = "Shape mode cancelled.";
                return;
            }

            _drawStrokeColor = picked.Value.Color;
            _drawStrokeWidth = picked.Value.WidthPoints;
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
            PdfShapeKind.RoundedRectangle => "Rounded rectangle mode — drag on the page.",
            PdfShapeKind.HighlightRectangle => "Area highlight mode — drag a translucent rectangle.",
            PdfShapeKind.Ellipse => "Ellipse mode — drag on the page.",
            PdfShapeKind.Arrow => "Arrow mode — drag from tail to tip.",
            PdfShapeKind.Star => "Star mode — drag a bounding box for a 5-point star.",
            _ => "Line mode — drag on the page.",
        };
    }

    private async Task<(PdfAnnotationColor Color, float WidthPoints)?> PickStrokeStyleAsync(string title)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for stroke style dialog.");

        float[] widths = [1f, 2f, 3f, 5f, 8f];
        var colorList = new ListView
        {
            Height = 180,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.StrokePresets.Select(p => p.Name).ToList(),
        };
        var currentColor = PdfAnnotationColor.StrokePresets.ToList()
            .FindIndex(p => p.Color.Equals(_drawStrokeColor));
        colorList.SelectedIndex = currentColor >= 0 ? currentColor : 0;

        var widthList = new ListView
        {
            Height = 140,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = widths.Select(w => $"{w:0} pt").ToList(),
        };
        var currentWidth = Array.FindIndex(widths, w => Math.Abs(w - _drawStrokeWidth) < 0.01f);
        widthList.SelectedIndex = currentWidth >= 0 ? currentWidth : 1;

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Color", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                colorList,
                new TextBlock { Text = "Width", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                widthList,
            },
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "Use",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var colorIndex = Math.Clamp(colorList.SelectedIndex, 0, PdfAnnotationColor.StrokePresets.Count - 1);
        var widthIndex = Math.Clamp(widthList.SelectedIndex, 0, widths.Length - 1);
        return (PdfAnnotationColor.StrokePresets[colorIndex].Color, widths[widthIndex]);
    }

    private void ClearShapeMode()
    {
        CancelShapeDrag();
        _shapeMode = null;
    }

    private void ClearCalloutMode()
    {
        if (!_calloutMode)
        {
            return;
        }

        _calloutMode = false;
        CancelShapeDrag();
    }

    private void ClearSignatureMode()
    {
        if (!_signatureMode)
        {
            return;
        }

        _signatureMode = false;
        CancelInkStroke();
    }

    private void RefreshToolButtonChrome()
    {
        var active = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 140, 0));
        if (_inkButton is not null)
        {
            _inkButton.Background = _inkMode ? active : null;
        }

        if (_freeformButton is not null)
        {
            _freeformButton.Background = _freeformMode ? active : null;
        }

        if (_polygonButton is not null)
        {
            _polygonButton.Background = _polygonMode ? active : null;
        }

        if (_eraserButton is not null)
        {
            _eraserButton.Background = _eraserMode ? active : null;
        }

        if (_highlightButton is not null)
        {
            _highlightButton.Background = _highlightMode ? active : null;
        }

        if (_formButton is not null)
        {
            _formButton.Background = _formOverlayMode ? active : null;
        }

        if (_signButton is not null)
        {
            _signButton.Background = _signatureMode ? active : null;
        }

        if (_rectButton is not null)
        {
            _rectButton.Background = _shapeMode == PdfShapeKind.Rectangle ? active : null;
        }

        if (_roundRectButton is not null)
        {
            _roundRectButton.Background = _shapeMode == PdfShapeKind.RoundedRectangle ? active : null;
        }

        if (_hiRectButton is not null)
        {
            _hiRectButton.Background = _shapeMode == PdfShapeKind.HighlightRectangle ? active : null;
        }

        if (_ellipseButton is not null)
        {
            _ellipseButton.Background = _shapeMode == PdfShapeKind.Ellipse ? active : null;
        }

        if (_lineButton is not null)
        {
            _lineButton.Background = _shapeMode == PdfShapeKind.Line ? active : null;
        }

        if (_arrowButton is not null)
        {
            _arrowButton.Background = _shapeMode == PdfShapeKind.Arrow ? active : null;
        }

        if (_starButton is not null)
        {
            _starButton.Background = _shapeMode == PdfShapeKind.Star ? active : null;
        }

        if (_calloutButton is not null)
        {
            _calloutButton.Background = _calloutMode ? active : null;
        }

        if (_redactButton is not null)
        {
            _redactButton.Background = _redactionMode ? active : null;
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
        if (_shapePageIndex < 0 || !_pageOverlays.TryGetValue(_shapePageIndex, out var overlay) || (_shapeMode is null && !_calloutMode))
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
        var stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(
            _drawStrokeColor.A,
            _drawStrokeColor.R,
            _drawStrokeColor.G,
            _drawStrokeColor.B));
        var fillAlpha = (byte)(_shapeMode == PdfShapeKind.HighlightRectangle ? 70 : 40);
        var fill = new SolidColorBrush(Windows.UI.Color.FromArgb(
            fillAlpha,
            _drawStrokeColor.R,
            _drawStrokeColor.G,
            _drawStrokeColor.B));

        var strokeThickness = Math.Max(1, _drawStrokeWidth * _scale / 1.5);

        FrameworkElement preview;
        if (_calloutMode)
        {
            preview = CreateLineOrArrowPreview(
                PdfShapeKind.Line,
                _shapeStart,
                current,
                stroke,
                strokeThickness);
        }
        else
        {
            preview = _shapeMode switch
            {
                PdfShapeKind.Ellipse => new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = Math.Max(1, width),
                    Height = Math.Max(1, height),
                    Stroke = stroke,
                    StrokeThickness = strokeThickness,
                    Fill = fill,
                },
                PdfShapeKind.Line or PdfShapeKind.Arrow => CreateLineOrArrowPreview(
                    _shapeMode.Value,
                    _shapeStart,
                    current,
                    stroke,
                    strokeThickness),
                PdfShapeKind.Star => CreateStarPreview(
                    left,
                    top,
                    width,
                    height,
                    stroke,
                    strokeThickness),
                _ => new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = Math.Max(1, width),
                    Height = Math.Max(1, height),
                    Stroke = stroke,
                    StrokeThickness = strokeThickness,
                    Fill = fill,
                },
            };
        }

        if (preview is not Microsoft.UI.Xaml.Shapes.Line and not Microsoft.UI.Xaml.Shapes.Polyline)
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
        if (kind is PdfShapeKind.Line or PdfShapeKind.Arrow)
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
                _drawStrokeColor,
                fillColor: kind is PdfShapeKind.Line or PdfShapeKind.Arrow or PdfShapeKind.Star
                    or PdfShapeKind.HighlightRectangle
                    ? null
                    : new PdfAnnotationColor(
                        _drawStrokeColor.R,
                        _drawStrokeColor.G,
                        _drawStrokeColor.B,
                        40),
                borderWidthPoints: _drawStrokeWidth);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = kind switch
            {
                PdfShapeKind.Rectangle => "Rectangle added.",
                PdfShapeKind.RoundedRectangle => "Rounded rectangle added.",
                PdfShapeKind.HighlightRectangle => "Area highlight added.",
                PdfShapeKind.Ellipse => "Ellipse added.",
                PdfShapeKind.Arrow => "Arrow added.",
                PdfShapeKind.Star => "Star added.",
                _ => "Line added.",
            };
        }
        catch (Exception ex)
        {
            _status.Text = "Shape failed: " + ex.Message;
        }
    }

    private static FrameworkElement CreateStarPreview(
        double left,
        double top,
        double width,
        double height,
        SolidColorBrush stroke,
        double strokeThickness)
    {
        var cx = left + (width / 2);
        var cy = top + (height / 2);
        var rx = Math.Max(width / 2, 0.5);
        var ry = Math.Max(height / 2, 0.5);
        const double innerScale = 0.38;
        var points = new PointCollection();
        for (var i = 0; i < 10; i++)
        {
            // UI Y grows downward; tip-up star uses −π/2 for the first outer vertex.
            var angle = (-Math.PI / 2) + (i * Math.PI / 5);
            var scale = (i % 2 == 0) ? 1.0 : innerScale;
            points.Add(new Windows.Foundation.Point(
                cx + (Math.Cos(angle) * rx * scale),
                cy + (Math.Sin(angle) * ry * scale)));
        }

        points.Add(points[0]);
        return new Microsoft.UI.Xaml.Shapes.Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            Fill = null,
        };
    }

    private static FrameworkElement CreateLineOrArrowPreview(
        PdfShapeKind kind,
        Windows.Foundation.Point start,
        Windows.Foundation.Point end,
        SolidColorBrush stroke,
        double strokeThickness = 2)
    {
        if (kind == PdfShapeKind.Line)
        {
            return new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
            };
        }

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1)
        {
            return new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
            };
        }

        var ux = dx / length;
        var uy = dy / length;
        var head = Math.Clamp(length * 0.22, 10.0, 28.0);
        const double wingRadians = Math.PI / 7;
        var cos = Math.Cos(wingRadians);
        var sin = Math.Sin(wingRadians);
        var backX = -ux * head;
        var backY = -uy * head;
        var wing1 = new Windows.Foundation.Point(
            end.X + (backX * cos) - (backY * sin),
            end.Y + (backX * sin) + (backY * cos));
        var wing2 = new Windows.Foundation.Point(
            end.X + (backX * cos) + (backY * sin),
            end.Y + (-backX * sin) + (backY * cos));

        return new Microsoft.UI.Xaml.Shapes.Polyline
        {
            Points =
            [
                start,
                end,
                wing1,
                end,
                wing2,
            ],
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            Fill = null,
        };
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
                Stroke = new SolidColorBrush(_signatureMode
                    ? Colors.Black
                    : Windows.UI.Color.FromArgb(
                        _drawStrokeColor.A,
                        _drawStrokeColor.R,
                        _drawStrokeColor.G,
                        _drawStrokeColor.B)),
                StrokeThickness = _signatureMode ? 2.5 : Math.Max(1, _drawStrokeWidth * _scale / 1.5),
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

        if ((_freeformMode && points.Count < 3) || (!_freeformMode && points.Count < 2) || pageIndex < 0)
        {
            _status.Text = _freeformMode ? "Freeform needs at least three points." : "Ink stroke too short.";
            return;
        }

        try
        {
            PdfAnnotationInfo created;
            if (_freeformMode)
            {
                _status.Text = "Saving freeform…";
                created = await _annotations.AddFreeformAsync(
                    _document,
                    pageIndex,
                    points,
                    _drawStrokeColor,
                    borderWidthPoints: _drawStrokeWidth);
                _status.Text = "Freeform shape added.";
            }
            else
            {
                _status.Text = "Saving ink…";
                created = await _annotations.AddInkAsync(
                    _document,
                    pageIndex,
                    points,
                    _drawStrokeColor,
                    borderWidthPoints: _drawStrokeWidth);
                _status.Text = "Ink stroke added.";
            }

            _strokeUndoStack.Push(created);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
        }
        catch (Exception ex)
        {
            _status.Text = (_freeformMode ? "Freeform" : "Ink") + " failed: " + ex.Message;
        }
    }

    private async Task UndoLastStrokeAsync()
    {
        if (_strokeUndoStack.Count == 0)
        {
            _status.Text = "No stroke to undo.";
            return;
        }

        var stroke = _strokeUndoStack.Pop();
        try
        {
            // Prefer the live sidebar entry in case indices shifted after other edits.
            var live = _annotationItems.FirstOrDefault(a =>
                a.PageIndex == stroke.PageIndex
                && a.AnnotIndex == stroke.AnnotIndex
                && a.IsInk);
            var target = live ?? stroke;
            await _annotations.RemoveAsync(_document, target.PageIndex, target.AnnotIndex);
            if (_selectedAnnot is not null
                && _selectedAnnot.PageIndex == target.PageIndex
                && _selectedAnnot.AnnotIndex == target.AnnotIndex)
            {
                ClearAnnotSelectionVisual();
                _selectedAnnot = null;
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Stroke undone.";
        }
        catch (Exception ex)
        {
            _status.Text = "Undo stroke failed: " + ex.Message;
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

    private async Task BeginSignatureAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for signature dialog.");

        if (_signatureMode)
        {
            ClearSignatureMode();
            RefreshToolButtonChrome();
            _status.Text = "Signature draw cancelled.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Signature",
            Content = "Draw with the mouse on the page, or import a transparent PNG/JPEG.",
            PrimaryButtonText = "Draw",
            SecondaryButtonText = "Import image",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            StartSignatureDrawMode();
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            await ImportSignatureImageAsync(window);
            return;
        }

        _status.Text = "Signature cancelled.";
    }

    private void StartSignatureDrawMode()
    {
        ClearRedactionMode();
        ClearShapeMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        _signatureMode = true;
        RefreshToolButtonChrome();
        _status.Text = "Signature draw — draw on the page, then release to save.";
    }

    private async Task EndSignatureStrokeAsync(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        ContinueInkStroke(border, e);

        var points = _inkPoints.ToList();
        var pageIndex = _inkPageIndex;
        CancelInkStroke();
        _signatureMode = false;
        RefreshToolButtonChrome();

        if (points.Count < 2 || pageIndex < 0)
        {
            _status.Text = "Signature stroke too short.";
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for signature name.");

        var nameBox = new TextBox
        {
            Text = "Signature",
            PlaceholderText = "Signature name",
        };
        var nameDialog = new ContentDialog
        {
            Title = "Save signature",
            Content = nameBox,
            PrimaryButtonText = "Insert",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await nameDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Signature cancelled.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Signature" : nameBox.Text.Trim();

        try
        {
            _status.Text = "Saving signature…";
            var stroke = points.Select(p => (p.X, p.Y)).ToList();
            var raster = SignatureStrokeRasterizer.Rasterize(stroke);
            var png = SignaturePngEncoder.EncodeBgra(raster.BgraPixels, raster.PixelWidth, raster.PixelHeight);
            await using (var pngStream = new MemoryStream(png))
            {
                await _signatures.SaveAsync(name, pngStream);
            }

            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            var pad = raster.PaddingPoints;
            var bounds = new PdfRect(minX - pad, minY - pad, maxX + pad, maxY + pad);

            await _annotations.AddStampAsync(
                _document,
                pageIndex,
                bounds,
                raster.BgraPixels,
                raster.PixelWidth,
                raster.PixelHeight);

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Signature inserted.";
        }
        catch (Exception ex)
        {
            _status.Text = "Signature failed: " + ex.Message;
        }
    }

    private async Task ImportSignatureImageAsync(Window window)
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            _status.Text = "Signature cancelled.";
            return;
        }

        try
        {
            _status.Text = "Loading signature…";
            using var winStream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(winStream);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var pixels = pixelData.DetachPixelData();
            var width = (int)decoder.PixelWidth;
            var height = (int)decoder.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                _status.Text = "Signature image is empty.";
                return;
            }

            // Persist a copy into the local signature library (PNG preferred).
            try
            {
                await using var copy = await file.OpenStreamForReadAsync();
                await _signatures.SaveAsync(
                    System.IO.Path.GetFileNameWithoutExtension(file.Name),
                    copy);
            }
            catch
            {
                // Library save is best-effort; insertion can still proceed.
            }

            var page = _document.GetPage(CurrentPageIndex);
            var targetWidth = Math.Min(180, page.WidthPoints * 0.35);
            var aspect = height / (double)width;
            var targetHeight = Math.Clamp(targetWidth * aspect, 24, page.HeightPoints * 0.25);
            var left = Math.Max(36, page.WidthPoints - targetWidth - 48);
            var bottom = Math.Max(36, 48.0);
            var bounds = new PdfRect(left, bottom, left + targetWidth, bottom + targetHeight);

            await _annotations.AddStampAsync(
                _document,
                CurrentPageIndex,
                bounds,
                pixels,
                width,
                height);

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = "Signature inserted.";
        }
        catch (Exception ex)
        {
            _status.Text = "Signature failed: " + ex.Message;
        }
    }

    private async Task OnFormButtonClickAsync()
    {
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
            RefreshToolButtonChrome();
            _status.Text = "Form overlay off.";
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for form dialog.");

        if (!await _forms.HasFormAsync(_document))
        {
            _status.Text = "No AcroForm fields in this document.";
            return;
        }

        var chooser = new ContentDialog
        {
            Title = "Form fill",
            Content = "Overlay draws clickable field boxes on the page. List opens the classic field picker.",
            PrimaryButtonText = "Overlay",
            SecondaryButtonText = "List fields",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var choice = await chooser.ShowAsync();
        if (choice == ContentDialogResult.Primary)
        {
            await BeginFormOverlayModeAsync();
            return;
        }

        if (choice == ContentDialogResult.Secondary)
        {
            await EditFormFieldsAsync();
        }
    }

    private async Task BeginFormOverlayModeAsync()
    {
        ClearRedactionMode();
        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        var fields = await _forms.ListFieldsAsync(_document);
        if (fields.Count == 0)
        {
            _status.Text = "AcroForm present but no widget fields found.";
            return;
        }

        _formOverlayMode = true;
        _formOverlayFields = fields;
        _formOverlayFocusIndex = 0;
        RefreshToolButtonChrome();
        DrawFormOverlays();
        await GoToPageAsync(fields[0].PageIndex, recordHistory: true);
        _status.Text = "Form overlay on — click a field, Tab/Shift+Tab to move, Enter to edit, Esc to exit.";
    }

    private void ClearFormOverlayMode()
    {
        _formOverlayMode = false;
        _formOverlayFields = [];
        _formOverlayFocusIndex = -1;
        ClearFormOverlayVisuals();
    }

    private void ClearFormOverlayVisuals()
    {
        foreach (var visual in _formOverlayVisuals)
        {
            if (visual.Parent is Canvas canvas)
            {
                canvas.Children.Remove(visual);
            }
        }

        _formOverlayVisuals.Clear();
    }

    private void DrawFormOverlays()
    {
        ClearFormOverlayVisuals();
        if (!_formOverlayMode || _formOverlayFields.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _formOverlayFields.Count; i++)
        {
            var field = _formOverlayFields[i];
            if (!_pageOverlays.TryGetValue(field.PageIndex, out var overlay))
            {
                continue;
            }

            var page = _document.GetPage(field.PageIndex);
            var b = field.Bounds;
            var left = b.Left * _scale;
            var top = (page.HeightPoints - b.Top) * _scale;
            var width = Math.Max(4, (b.Right - b.Left) * _scale);
            var height = Math.Max(4, (b.Top - b.Bottom) * _scale);
            var focused = i == _formOverlayFocusIndex;
            var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Stroke = new SolidColorBrush(focused ? Colors.Orange : Colors.DodgerBlue),
                StrokeThickness = focused ? 2.5 : 1.5,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(
                    focused ? (byte)70 : (byte)40,
                    30,
                    144,
                    255)),
                IsHitTestVisible = false,
                Tag = field,
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            overlay.Children.Add(rect);
            _formOverlayVisuals.Add(rect);

            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(field.Name) ? field.Kind.ToString() : field.Name,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.DodgerBlue),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(label, left + 2);
            Canvas.SetTop(label, Math.Max(0, top - 14));
            overlay.Children.Add(label);
            _formOverlayVisuals.Add(label);
        }
    }

    private PdfFormFieldInfo? HitTestFormOverlayField(int pageIndex, double pdfX, double pdfY)
    {
        for (var i = _formOverlayFields.Count - 1; i >= 0; i--)
        {
            var field = _formOverlayFields[i];
            if (field.PageIndex != pageIndex)
            {
                continue;
            }

            var b = field.Bounds;
            if (pdfX >= b.Left && pdfX <= b.Right && pdfY >= b.Bottom && pdfY <= b.Top)
            {
                _formOverlayFocusIndex = i;
                DrawFormOverlays();
                return field;
            }
        }

        return null;
    }

    private async Task MoveFormOverlayFocusAsync(bool forward)
    {
        if (_formOverlayFields.Count == 0)
        {
            return;
        }

        var current = _formOverlayFocusIndex >= 0 && _formOverlayFocusIndex < _formOverlayFields.Count
            ? _formOverlayFields[_formOverlayFocusIndex]
            : _formOverlayFields[0];

        var next = await _forms.FocusAdjacentAsync(
            _document,
            current.PageIndex,
            current.AnnotIndex,
            forward);
        if (next is null)
        {
            return;
        }

        var nextIndex = _formOverlayFields.ToList().FindIndex(
            f => f.PageIndex == next.PageIndex && f.AnnotIndex == next.AnnotIndex);
        if (nextIndex < 0)
        {
            return;
        }

        _formOverlayFocusIndex = nextIndex;
        DrawFormOverlays();
        await GoToPageAsync(next.PageIndex, recordHistory: true);
        _status.Text = $"Focused {next.Name} ({next.Kind}).";
    }

    private async Task EditFocusedFormOverlayFieldAsync()
    {
        if (_formOverlayFocusIndex < 0 || _formOverlayFocusIndex >= _formOverlayFields.Count)
        {
            _status.Text = "No form field focused.";
            return;
        }

        await EditFormOverlayFieldAsync(_formOverlayFields[_formOverlayFocusIndex]);
    }

    private async Task EditFormOverlayFieldAsync(PdfFormFieldInfo field)
    {
        var changed = await TryEditFormFieldAsync(field);
        if (!changed)
        {
            return;
        }

        _formOverlayFields = await _forms.ListFieldsAsync(_document);
        if (_formOverlayFocusIndex >= _formOverlayFields.Count)
        {
            _formOverlayFocusIndex = Math.Max(0, _formOverlayFields.Count - 1);
        }

        _cache.ClearDocument(_documentKey);
        await RenderVisibleAsync();
        DrawFormOverlays();
    }

    private async Task EditFormFieldsAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for form dialog.");

        if (!await _forms.HasFormAsync(_document))
        {
            _status.Text = "No AcroForm fields in this document.";
            return;
        }

        var fields = await _forms.ListFieldsAsync(_document);
        if (fields.Count == 0)
        {
            _status.Text = "AcroForm present but no widget fields found.";
            return;
        }

        var list = new ListView
        {
            Height = 260,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = fields
                .Select(f => $"{f.TabOrder + 1}. {f.Name} ({f.Kind}) = \"{f.Value}\"")
                .ToList(),
        };
        list.SelectedIndex = 0;

        var dialog = new ContentDialog
        {
            Title = "Form fields",
            Content = list,
            PrimaryButtonText = "Edit",
            SecondaryButtonText = "Next (Tab)",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                _status.Text = "Form editor closed.";
                return;
            }

            var index = list.SelectedIndex;
            if (index < 0 || index >= fields.Count)
            {
                index = 0;
            }

            var field = fields[index];
            if (result == ContentDialogResult.Secondary)
            {
                var next = await _forms.FocusAdjacentAsync(
                    _document,
                    field.PageIndex,
                    field.AnnotIndex,
                    forward: true);
                if (next is null)
                {
                    continue;
                }

                var nextIndex = fields.ToList().FindIndex(
                    f => f.PageIndex == next.PageIndex && f.AnnotIndex == next.AnnotIndex);
                if (nextIndex >= 0)
                {
                    list.SelectedIndex = nextIndex;
                }

                await GoToPageAsync(next.PageIndex, recordHistory: true);
                continue;
            }

            var changed = await TryEditFormFieldAsync(field);
            if (!changed)
            {
                continue;
            }

            fields = await _forms.ListFieldsAsync(_document);
            list.ItemsSource = fields
                .Select(f => $"{f.TabOrder + 1}. {f.Name} ({f.Kind}) = \"{f.Value}\"")
                .ToList();
            list.SelectedIndex = Math.Clamp(index, 0, Math.Max(0, fields.Count - 1));
            _cache.ClearDocument(_documentKey);
            await RenderVisibleAsync();
        }
    }

    /// <summary>
    /// Opens the appropriate edit UI for one field. Returns true when the document was modified.
    /// </summary>
    private async Task<bool> TryEditFormFieldAsync(PdfFormFieldInfo field)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for form dialog.");

        if (field.Kind == PdfFormFieldKind.RadioButton)
        {
            var currentlyOn = !string.Equals(field.Value, "Off", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(field.Value);
            var select = new ContentDialog
            {
                Title = field.Name,
                Content = currentlyOn
                    ? $"Radio is selected (\"{field.Value}\")."
                    : "Radio is not selected. Selecting it turns off siblings in this group.",
                PrimaryButtonText = currentlyOn ? "OK" : "Select",
                CloseButtonText = currentlyOn ? "Close" : "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };

            var radioResult = await select.ShowAsync();
            if (currentlyOn || radioResult != ContentDialogResult.Primary)
            {
                return false;
            }

            try
            {
                await _forms.SetRadioButtonAsync(
                    _document,
                    field.PageIndex,
                    field.AnnotIndex);
                _status.Text = $"Selected radio {field.Name}.";
                return true;
            }
            catch (Exception ex)
            {
                _status.Text = "Form fill failed: " + ex.Message;
                return false;
            }
        }

        if (field.Kind == PdfFormFieldKind.CheckBox)
        {
            var currentlyOn = !string.Equals(field.Value, "Off", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(field.Value);
            var toggle = new ContentDialog
            {
                Title = field.Name,
                Content = currentlyOn ? "Checkbox is checked." : "Checkbox is unchecked.",
                PrimaryButtonText = currentlyOn ? "Uncheck" : "Check",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };

            if (await toggle.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            try
            {
                await _forms.SetCheckBoxAsync(
                    _document,
                    field.PageIndex,
                    field.AnnotIndex,
                    isChecked: !currentlyOn);
                _status.Text = $"Updated {field.Name}.";
                return true;
            }
            catch (Exception ex)
            {
                _status.Text = "Form fill failed: " + ex.Message;
                return false;
            }
        }

        if (field.Kind is PdfFormFieldKind.ComboBox or PdfFormFieldKind.ListBox)
        {
            var options = field.ChoiceOptions;
            if (options.Count > 0)
            {
                var choiceList = new ListView
                {
                    Height = 220,
                    SelectionMode = ListViewSelectionMode.Single,
                    ItemsSource = options.ToList(),
                };
                var selected = options.ToList().FindIndex(o => o == field.Value);
                choiceList.SelectedIndex = selected >= 0 ? selected : 0;
                var pick = new ContentDialog
                {
                    Title = $"Select {field.Name}",
                    Content = choiceList,
                    PrimaryButtonText = "Apply",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = window.Content.XamlRoot,
                };

                if (await pick.ShowAsync() != ContentDialogResult.Primary)
                {
                    return false;
                }

                var choice = choiceList.SelectedItem as string ?? field.Value;
                try
                {
                    await _forms.SetTextValueAsync(
                        _document,
                        field.PageIndex,
                        field.AnnotIndex,
                        choice);
                    _status.Text = $"Updated {field.Name}.";
                    return true;
                }
                catch (Exception ex)
                {
                    _status.Text = "Form fill failed: " + ex.Message;
                    return false;
                }
            }
        }

        if (field.Kind is not (PdfFormFieldKind.TextField
            or PdfFormFieldKind.ComboBox
            or PdfFormFieldKind.ListBox))
        {
            _status.Text = $"Editing {field.Kind} fields is not supported yet.";
            return false;
        }

        var box = new TextBox
        {
            Text = field.Value,
            AcceptsReturn = field.Kind == PdfFormFieldKind.TextField,
            TextWrapping = TextWrapping.Wrap,
            Height = 100,
            PlaceholderText = field.Name,
        };
        var edit = new ContentDialog
        {
            Title = $"Edit {field.Name}",
            Content = box,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await edit.ShowAsync() != ContentDialogResult.Primary)
        {
            return false;
        }

        try
        {
            await _forms.SetTextValueAsync(
                _document,
                field.PageIndex,
                field.AnnotIndex,
                box.Text ?? string.Empty);
            _status.Text = $"Updated {field.Name}.";
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = "Form fill failed: " + ex.Message;
            return false;
        }
    }

    private async Task FlattenAnnotationsAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for flatten dialog.");

        var dialog = new ContentDialog
        {
            Title = "Flatten annotations",
            Content = "Bake all annotations into page content? This cannot be undone from the annotation layer.",
            PrimaryButtonText = "Flatten",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _status.Text = "Flatten cancelled.";
            return;
        }

        try
        {
            _status.Text = "Flattening…";
            var outcome = await _annotations.FlattenAsync(_document);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();

            if (outcome.PagesFailed > 0)
            {
                _status.Text =
                    $"Flattened {outcome.PagesChanged} page(s); {outcome.PagesFailed} failed.";
            }
            else if (outcome.PagesChanged == 0)
            {
                _status.Text = "Nothing to flatten.";
            }
            else
            {
                _status.Text = outcome.PagesChanged == 1
                    ? "Flattened annotations on 1 page."
                    : $"Flattened annotations on {outcome.PagesChanged} pages.";
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Flatten failed: " + ex.Message;
        }
    }

    private async Task OnRedactButtonClickAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for redaction dialog.");

        var pending = _redaction.GetPending(_document);
        var hasSelection = !string.IsNullOrWhiteSpace(_selectedText)
            && _selectionPageIndex >= 0
            && _selectionQuads.Count > 0;
        var hasRegion = _regionCopyPageIndex >= 0
            && _regionCopyDisplayRect.Width >= 4
            && _regionCopyDisplayRect.Height >= 4;
        var hasFindMatches = !string.IsNullOrWhiteSpace(_searchQuery) && _hits.Count > 0;

        if (_redactionMode)
        {
            ClearRedactionMode();
            RefreshToolButtonChrome();
            _status.Text = pending.Count == 0
                ? "Redact mode off."
                : $"Redact mode off — {pending.Count} pending mark(s). Use Redact → Apply when ready.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Redaction",
            Content = pending.Count == 0
                ? "Mark areas to remove permanently. Drag rectangles in draw mode, or mark the current selection/region."
                : $"{pending.Count} pending mark(s). Apply permanently removes underlying text/images — this cannot be undone.",
            XamlRoot = window.Content.XamlRoot,
        };

        if (pending.Count > 0)
        {
            dialog.PrimaryButtonText = "Apply…";
            dialog.SecondaryButtonText = "Draw marks";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = ContentDialogButton.Close;
        }
        else
        {
            dialog.PrimaryButtonText = "Draw marks";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = ContentDialogButton.Primary;
            if (hasFindMatches)
            {
                dialog.SecondaryButtonText = $"Mark find matches ({_hits.Count})";
            }
            else if (hasSelection)
            {
                dialog.SecondaryButtonText = "Mark selection";
            }
            else if (hasRegion)
            {
                dialog.SecondaryButtonText = "Mark region";
            }
        }

        var result = await dialog.ShowAsync();
        if (pending.Count > 0)
        {
            if (result == ContentDialogResult.Primary)
            {
                await ApplyPendingRedactionsAsync();
                return;
            }

            if (result == ContentDialogResult.Secondary)
            {
                EnterRedactionMode();
                return;
            }

            _status.Text = "Redaction cancelled.";
            return;
        }

        if (result == ContentDialogResult.Primary)
        {
            EnterRedactionMode();
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            if (hasFindMatches)
            {
                await MarkFindMatchesForRedactionAsync();
            }
            else if (hasSelection)
            {
                MarkSelectionForRedaction();
            }
            else if (hasRegion)
            {
                MarkRegionForRedaction();
            }
        }
    }

    private void EnterRedactionMode()
    {
        ClearShapeMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearFreeformMode();
        ClearPolygonMode();
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
        }

        if (_inkMode)
        {
            _inkMode = false;
            CancelInkStroke();
        }

        if (_cropMode)
        {
            CancelCropMode();
        }

        _redactionMode = true;
        RefreshToolButtonChrome();
        _status.Text = "Redact mode — drag a rectangle to mark. Esc to exit.";
    }

    private void ClearRedactionMode()
    {
        if (!_redactionMode && !_redactionDrawing)
        {
            return;
        }

        CancelRedactionDrag();
        _redactionMode = false;
    }

    private void BeginRedactionDrag(Border border, int pageIndex, PointerRoutedEventArgs e)
    {
        CancelRedactionDrag();
        _redactionDrawing = true;
        _redactionPageIndex = pageIndex;
        _redactionStart = e.GetCurrentPoint(border).Position;
        border.CapturePointer(e.Pointer);
        ContinueRedactionDrag(border, e);
    }

    private void ContinueRedactionDrag(Border border, PointerRoutedEventArgs e)
    {
        if (_redactionPageIndex < 0 || !_pageOverlays.TryGetValue(_redactionPageIndex, out var overlay))
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        if (_redactionPreview is not null)
        {
            overlay.Children.Remove(_redactionPreview);
            _redactionPreview = null;
        }

        var left = Math.Min(_redactionStart.X, current.X);
        var top = Math.Min(_redactionStart.Y, current.Y);
        var width = Math.Abs(current.X - _redactionStart.X);
        var height = Math.Abs(current.Y - _redactionStart.Y);
        if (width < 2 || height < 2)
        {
            return;
        }

        var preview = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(140, 0, 0, 0)),
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 180, 0, 0)),
            StrokeThickness = 1.5,
            StrokeDashArray = [4, 2],
        };
        Canvas.SetLeft(preview, left);
        Canvas.SetTop(preview, top);
        overlay.Children.Add(preview);
        _redactionPreview = preview;
    }

    private void EndRedactionDrag(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        ContinueRedactionDrag(border, e);

        var pageIndex = _redactionPageIndex;
        var start = _redactionStart;
        var end = e.GetCurrentPoint(border).Position;
        CancelRedactionDrag();

        if (pageIndex < 0)
        {
            return;
        }

        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (width < 4 || height < 4)
        {
            _status.Text = "Redaction mark too small.";
            return;
        }

        var page = _document.GetPage(pageIndex);
        double ToPdfX(double x) => x / _scale;
        double ToPdfY(double y) => page.HeightPoints - (y / _scale);
        var left = Math.Min(ToPdfX(start.X), ToPdfX(end.X));
        var right = Math.Max(ToPdfX(start.X), ToPdfX(end.X));
        var bottom = Math.Min(ToPdfY(start.Y), ToPdfY(end.Y));
        var top = Math.Max(ToPdfY(start.Y), ToPdfY(end.Y));
        var bounds = new PdfRect(left, bottom, right, top);

        try
        {
            _redaction.MarkRectangle(_document, pageIndex, bounds);
            RefreshPendingRedactionOverlay(pageIndex);
            var count = _redaction.GetPending(_document).Count;
            _status.Text = $"Marked redaction ({count} pending). Redact → Apply when ready.";
        }
        catch (Exception ex)
        {
            _status.Text = "Mark failed: " + ex.Message;
        }
    }

    private void CancelRedactionDrag()
    {
        if (_redactionPreview is not null &&
            _redactionPageIndex >= 0 &&
            _pageOverlays.TryGetValue(_redactionPageIndex, out var overlay))
        {
            overlay.Children.Remove(_redactionPreview);
        }

        _redactionPreview = null;
        _redactionDrawing = false;
        _redactionPageIndex = -1;
    }

    private void MarkSelectionForRedaction()
    {
        if (_selectionPageIndex < 0 || _selectionQuads.Count == 0 || string.IsNullOrWhiteSpace(_selectedText))
        {
            _status.Text = "Select text to mark for redaction.";
            return;
        }

        try
        {
            var union = _selectionQuads[0].Bounds;
            foreach (var quad in _selectionQuads.Skip(1))
            {
                var b = quad.Bounds;
                union = new PdfRect(
                    Math.Min(union.Left, b.Left),
                    Math.Min(union.Bottom, b.Bottom),
                    Math.Max(union.Right, b.Right),
                    Math.Max(union.Top, b.Top));
            }

            // Pad slightly so glyph objects fully intersect.
            union = new PdfRect(union.Left - 1, union.Bottom - 1, union.Right + 1, union.Top + 1);
            _redaction.MarkTextRegion(_document, _selectionPageIndex, union, TrimForStatus(_selectedText));
            RefreshPendingRedactionOverlay(_selectionPageIndex);
            var count = _redaction.GetPending(_document).Count;
            _status.Text = $"Marked text for redaction ({count} pending).";
        }
        catch (Exception ex)
        {
            _status.Text = "Mark failed: " + ex.Message;
        }
    }

    private void MarkRegionForRedaction()
    {
        if (_regionCopyPageIndex < 0
            || _regionCopyDisplayRect.Width < 4
            || _regionCopyDisplayRect.Height < 4)
        {
            _status.Text = "Drag a region first.";
            return;
        }

        try
        {
            var page = _document.GetPage(_regionCopyPageIndex);
            var left = _regionCopyDisplayRect.X / _scale;
            var right = (_regionCopyDisplayRect.X + _regionCopyDisplayRect.Width) / _scale;
            var top = page.HeightPoints - (_regionCopyDisplayRect.Y / _scale);
            var bottom = page.HeightPoints - ((_regionCopyDisplayRect.Y + _regionCopyDisplayRect.Height) / _scale);
            var bounds = new PdfRect(
                Math.Min(left, right),
                Math.Min(bottom, top),
                Math.Max(left, right),
                Math.Max(bottom, top));
            _redaction.MarkRectangle(_document, _regionCopyPageIndex, bounds);
            RefreshPendingRedactionOverlay(_regionCopyPageIndex);
            var count = _redaction.GetPending(_document).Count;
            _status.Text = $"Marked region for redaction ({count} pending).";
        }
        catch (Exception ex)
        {
            _status.Text = "Mark failed: " + ex.Message;
        }
    }

    private void PendingRedactionRect_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Shapes.Rectangle { Tag: Guid id })
        {
            return;
        }

        if (!_redaction.RemovePending(_document, id))
        {
            return;
        }

        RefreshAllPendingRedactionOverlays();
        var remaining = _redaction.GetPending(_document).Count;
        _status.Text = remaining == 0
            ? "Removed redaction mark."
            : $"Removed redaction mark ({remaining} pending).";
        e.Handled = true;
    }

    private void RefreshAllPendingRedactionOverlays()
    {
        foreach (var pageIndex in _redactionOverlays.Keys.ToList())
        {
            RefreshPendingRedactionOverlay(pageIndex);
        }
    }

    private void RefreshPendingRedactionOverlay(int pageIndex)
    {
        if (!_redactionOverlays.TryGetValue(pageIndex, out var overlay))
        {
            return;
        }

        overlay.Children.Clear();
        var marks = _redaction.GetPending(_document).Where(m => m.PageIndex == pageIndex).ToList();
        overlay.IsHitTestVisible = marks.Count > 0;
        var page = _document.GetPage(pageIndex);
        foreach (var mark in marks)
        {
            var left = mark.Bounds.Left * _scale;
            var top = (page.HeightPoints - mark.Bounds.Top) * _scale;
            var width = mark.Bounds.Width * _scale;
            var height = mark.Bounds.Height * _scale;
            if (width < 1 || height < 1)
            {
                continue;
            }

            var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 0, 0, 0)),
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 200, 40, 40)),
                StrokeThickness = 1.5,
                Tag = mark.Id,
            };
            ToolTipService.SetToolTip(rect, "Click to remove pending redaction");
            rect.PointerPressed += PendingRedactionRect_PointerPressed;
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            overlay.Children.Add(rect);

            if (!string.IsNullOrWhiteSpace(mark.Label))
            {
                var label = new TextBlock
                {
                    Text = TrimForStatus(mark.Label!),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.White),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(label, left + 4);
                Canvas.SetTop(label, top + 2);
                overlay.Children.Add(label);
            }
        }
    }

    private async Task ApplyPendingRedactionsAsync()
    {
        var pending = _redaction.GetPending(_document);
        if (pending.Count == 0)
        {
            _status.Text = "No pending redactions.";
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for redaction apply.");

        var removeAnnotations = new CheckBox
        {
            Content = "Remove intersecting annotations",
            IsChecked = true,
        };
        var removeAttachments = new CheckBox
        {
            Content = "Remove embedded file attachments",
            IsChecked = true,
        };
        var removeMetadata = new CheckBox
        {
            Content = "Clear Info metadata (title/author/subject/keywords)",
            IsChecked = true,
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420,
                    Text =
                        $"Apply {pending.Count} redaction mark(s)? Underlying text and covered image content will be removed. This cannot be undone.",
                },
                removeAnnotations,
                removeAttachments,
                removeMetadata,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Apply redactions permanently?",
            Content = panel,
            PrimaryButtonText = "Apply",
            SecondaryButtonText = "Clear marks",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.Content.XamlRoot,
        };

        var applyChoice = await dialog.ShowAsync();
        if (applyChoice == ContentDialogResult.Secondary)
        {
            _redaction.ClearPending(_document);
            RefreshAllPendingRedactionOverlays();
            _status.Text = "Cleared pending redactions.";
            return;
        }

        if (applyChoice != ContentDialogResult.Primary)
        {
            _status.Text = "Apply cancelled.";
            return;
        }

        try
        {
            _status.Text = "Applying redactions…";
            var result = await _redaction.ApplyAsync(
                _document,
                new PdfRedactionApplyOptions(
                    RemoveIntersectingTextObjects: true,
                    RemoveIntersectingImageObjects: true,
                    RemoveIntersectingAnnotations: removeAnnotations.IsChecked == true,
                    RemoveEmbeddedAttachments: removeAttachments.IsChecked == true,
                    RemoveMetadata: removeMetadata.IsChecked == true));
            ClearRedactionMode();
            RefreshToolButtonChrome();
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            _pageChars.Clear();
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            RefreshAllPendingRedactionOverlays();
            if (result.MarksApplied == 0)
            {
                _status.Text = "Nothing to apply.";
            }
            else
            {
                _status.Text =
                    $"Applied {result.MarksApplied} redaction(s) on {result.PagesChanged} page(s); "
                    + $"removed {result.TextObjectsRemoved} text / {result.ImageObjectsRemoved} image / "
                    + $"{result.AnnotationsRemoved} annotation / {result.AttachmentsRemoved} attachment object(s)"
                    + (result.MetadataCleared ? "; metadata cleared" : "")
                    + ".";
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Apply failed: " + ex.Message;
        }
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
        var authorBox = new TextBox
        {
            Text = _annotationAuthor,
            PlaceholderText = "Author",
            Width = 220,
        };
        var colorList = new ListView
        {
            Height = 140,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.StickyNotePresets.Select(p => p.Name).ToList(),
            SelectedIndex = 0,
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                box,
                new TextBlock { Text = "Author", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                authorBox,
                new TextBlock { Text = "Color", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                colorList,
            },
        };
        var dialog = new ContentDialog
        {
            Title = "Sticky note",
            Content = panel,
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

        var colorIndex = Math.Clamp(colorList.SelectedIndex, 0, PdfAnnotationColor.StickyNotePresets.Count - 1);
        var color = PdfAnnotationColor.StickyNotePresets[colorIndex].Color;
        if (!string.IsNullOrWhiteSpace(authorBox.Text))
        {
            _annotationAuthor = authorBox.Text.Trim();
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
                color,
                author: _annotationAuthor);
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
            Height = 120,
            PlaceholderText = "Text box contents",
        };
        var fontSizeBox = new NumberBox
        {
            Header = "Font size (pt)",
            Value = 12,
            Minimum = 6,
            Maximum = 72,
            SmallChange = 1,
            LargeChange = 2,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var fontFamilyBox = new ComboBox
        {
            Header = "Font",
            ItemsSource = new[] { "Helvetica", "Times", "Courier" },
            SelectedIndex = 0,
            Width = 220,
        };
        var boldCheck = new CheckBox { Content = "Bold", IsChecked = false };
        var italicCheck = new CheckBox { Content = "Italic", IsChecked = false };
        var textColorList = new ListView
        {
            Height = 100,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.StrokePresets.Select(p => p.Name).ToList(),
            SelectedIndex = 5, // Black
        };
        var fillList = new ListView
        {
            Height = 120,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = new[] { "None", "White", "Yellow", "Light blue", "Light green" }.ToList(),
            SelectedIndex = 1,
        };
        var borderList = new ListView
        {
            Height = 100,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.StrokePresets.Select(p => p.Name).ToList(),
            SelectedIndex = 5, // Black
        };
        var panel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                box,
                fontSizeBox,
                fontFamilyBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { boldCheck, italicCheck },
                },
                new TextBlock { Text = "Text color", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                textColorList,
                new TextBlock { Text = "Fill", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                fillList,
                new TextBlock { Text = "Border", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                borderList,
            },
        };
        var dialog = new ContentDialog
        {
            Title = "Text box",
            Content = panel,
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

        PdfAnnotationColor? fill = fillList.SelectedIndex switch
        {
            1 => new PdfAnnotationColor(255, 255, 255),
            2 => new PdfAnnotationColor(255, 250, 180),
            3 => new PdfAnnotationColor(200, 230, 255),
            4 => new PdfAnnotationColor(210, 245, 210),
            _ => null,
        };
        var border = PdfAnnotationColor.StrokePresets[
            Math.Clamp(borderList.SelectedIndex, 0, PdfAnnotationColor.StrokePresets.Count - 1)].Color;
        var textColor = PdfAnnotationColor.StrokePresets[
            Math.Clamp(textColorList.SelectedIndex, 0, PdfAnnotationColor.StrokePresets.Count - 1)].Color;
        var fontSize = (float)(double.IsNaN(fontSizeBox.Value) ? 12 : Math.Clamp(fontSizeBox.Value, 6, 72));
        var fontResource = PdfFreeTextFont.ResolveResourceName(
            (PdfFreeTextFontFamily)Math.Clamp(fontFamilyBox.SelectedIndex, 0, 2),
            boldCheck.IsChecked == true,
            italicCheck.IsChecked == true);

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
                textColor,
                borderColor: border,
                fillColor: fill,
                fontSizePoints: fontSize,
                fontResourceName: fontResource);
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
        _selectedAnnot = item;
        DrawAnnotSelection(item);
        await GoToPageAsync(item.PageIndex, recordHistory: true);
        _status.Text = $"Jumped to {FormatAnnotationLabel(item)}.";
    }

    private void BeginAnnotDrag(
        Border border,
        PdfAnnotationInfo hit,
        Windows.Foundation.Point uiPoint,
        PointerRoutedEventArgs e)
    {
        _selectedAnnot = hit;
        _annotDragging = true;
        _annotResizeHandle = null;
        _annotDragOriginBounds = hit.Bounds;
        _annotDragOriginUi = uiPoint;
        border.CapturePointer(e.Pointer);
        SyncSidebarSelection(hit);
        DrawAnnotSelection(hit);
        _status.Text = $"Selected {FormatAnnotationLabel(hit)}. Drag to move; handles resize.";
    }

    private void BeginAnnotResize(
        Border border,
        PdfAnnotationInfo hit,
        string handle,
        Windows.Foundation.Point uiPoint,
        PointerRoutedEventArgs e)
    {
        _selectedAnnot = hit;
        _annotDragging = true;
        _annotResizeHandle = handle;
        _annotDragOriginBounds = hit.Bounds;
        _annotDragOriginUi = uiPoint;
        border.CapturePointer(e.Pointer);
        SyncSidebarSelection(hit);
        DrawAnnotSelection(hit);
        _status.Text = $"Resizing {FormatAnnotationLabel(hit)}…";
    }

    private void ContinueAnnotDrag(Border border, PointerRoutedEventArgs e)
    {
        if (_selectedAnnot is null)
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        var dx = (current.X - _annotDragOriginUi.X) / _scale;
        var dy = (_annotDragOriginUi.Y - current.Y) / _scale; // UI Y down → PDF Y up
        var moved = new PdfRect(
            _annotDragOriginBounds.Left + dx,
            _annotDragOriginBounds.Bottom + dy,
            _annotDragOriginBounds.Right + dx,
            _annotDragOriginBounds.Top + dy);
        DrawAnnotSelection(_selectedAnnot with { Bounds = moved });
    }

    private void ContinueAnnotResize(Border border, PointerRoutedEventArgs e)
    {
        if (_selectedAnnot is null || _annotResizeHandle is null)
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        var dx = (current.X - _annotDragOriginUi.X) / _scale;
        var dy = (_annotDragOriginUi.Y - current.Y) / _scale;
        var resized = PdfAnnotationResize.ComputeBounds(_annotDragOriginBounds, _annotResizeHandle, dx, dy);
        DrawAnnotSelection(_selectedAnnot with { Bounds = resized });
    }

    private async Task EndAnnotDragAsync(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        _annotDragging = false;
        _annotResizeHandle = null;
        if (_selectedAnnot is null)
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        var dxUi = current.X - _annotDragOriginUi.X;
        var dyUi = current.Y - _annotDragOriginUi.Y;
        if (Math.Abs(dxUi) + Math.Abs(dyUi) < 3)
        {
            DrawAnnotSelection(_selectedAnnot);
            return;
        }

        var dx = dxUi / _scale;
        var dy = -dyUi / _scale;
        var moved = new PdfRect(
            _annotDragOriginBounds.Left + dx,
            _annotDragOriginBounds.Bottom + dy,
            _annotDragOriginBounds.Right + dx,
            _annotDragOriginBounds.Top + dy);

        try
        {
            _status.Text = "Moving annotation…";
            await _annotations.MoveAsync(
                _document,
                _selectedAnnot.PageIndex,
                _selectedAnnot.AnnotIndex,
                moved);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _selectedAnnot = _annotationItems.FirstOrDefault(
                a => a.PageIndex == _selectedAnnot.PageIndex && a.AnnotIndex == _selectedAnnot.AnnotIndex)
                ?? _selectedAnnot with { Bounds = moved };
            SyncSidebarSelection(_selectedAnnot);
            DrawAnnotSelection(_selectedAnnot);
            _status.Text = "Annotation moved.";
        }
        catch (Exception ex)
        {
            DrawAnnotSelection(_selectedAnnot);
            _status.Text = "Move failed: " + ex.Message;
        }
    }

    private async Task EndAnnotResizeAsync(Border border, PointerRoutedEventArgs e)
    {
        try { border.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        var handle = _annotResizeHandle;
        _annotDragging = false;
        _annotResizeHandle = null;
        if (_selectedAnnot is null || handle is null)
        {
            return;
        }

        var current = e.GetCurrentPoint(border).Position;
        var dxUi = current.X - _annotDragOriginUi.X;
        var dyUi = current.Y - _annotDragOriginUi.Y;
        if (Math.Abs(dxUi) + Math.Abs(dyUi) < 3)
        {
            DrawAnnotSelection(_selectedAnnot);
            return;
        }

        var dx = dxUi / _scale;
        var dy = -dyUi / _scale;
        var resized = PdfAnnotationResize.ComputeBounds(_annotDragOriginBounds, handle, dx, dy);

        try
        {
            _status.Text = "Resizing annotation…";
            await _annotations.MoveAsync(
                _document,
                _selectedAnnot.PageIndex,
                _selectedAnnot.AnnotIndex,
                resized);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _selectedAnnot = _annotationItems.FirstOrDefault(
                a => a.PageIndex == _selectedAnnot.PageIndex && a.AnnotIndex == _selectedAnnot.AnnotIndex)
                ?? _selectedAnnot with { Bounds = resized };
            SyncSidebarSelection(_selectedAnnot);
            DrawAnnotSelection(_selectedAnnot);
            _status.Text = "Annotation resized.";
        }
        catch (Exception ex)
        {
            DrawAnnotSelection(_selectedAnnot);
            _status.Text = "Resize failed: " + ex.Message;
        }
    }

    private string? HitTestAnnotResizeHandle(
        PdfAnnotationInfo info,
        Windows.Foundation.Point uiPoint,
        IPdfPage page)
    {
        const double hitRadius = 10.0;
        foreach (var (id, x, y) in EnumerateAnnotHandleCenters(info, page))
        {
            if (Math.Abs(uiPoint.X - x) <= hitRadius && Math.Abs(uiPoint.Y - y) <= hitRadius)
            {
                return id;
            }
        }

        return null;
    }

    private IEnumerable<(string Id, double X, double Y)> EnumerateAnnotHandleCenters(
        PdfAnnotationInfo info,
        IPdfPage page)
    {
        var left = info.Bounds.Left * _scale;
        var top = (page.HeightPoints - info.Bounds.Top) * _scale;
        var right = info.Bounds.Right * _scale;
        var bottom = (page.HeightPoints - info.Bounds.Bottom) * _scale;
        var midX = (left + right) / 2;
        var midY = (top + bottom) / 2;
        yield return ("nw", left, top);
        yield return ("n", midX, top);
        yield return ("ne", right, top);
        yield return ("e", right, midY);
        yield return ("se", right, bottom);
        yield return ("s", midX, bottom);
        yield return ("sw", left, bottom);
        yield return ("w", left, midY);
    }

    private void SyncSidebarSelection(PdfAnnotationInfo info)
    {
        var index = _annotationItems.ToList().FindIndex(
            a => a.PageIndex == info.PageIndex && a.AnnotIndex == info.AnnotIndex);
        if (index < 0)
        {
            return;
        }

        _suppressAnnotationNav = true;
        _annotationList.SelectedIndex = index;
        _suppressAnnotationNav = false;
    }

    private void DrawAnnotSelection(PdfAnnotationInfo info)
    {
        ClearAnnotSelectionVisual();
        if (!_pageOverlays.TryGetValue(info.PageIndex, out var overlay))
        {
            return;
        }

        var page = _document.GetPage(info.PageIndex);
        var left = info.Bounds.Left * _scale;
        var top = (page.HeightPoints - info.Bounds.Top) * _scale;
        var width = Math.Max(4, info.Bounds.Width * _scale);
        var height = Math.Max(4, info.Bounds.Height * _scale);
        _annotSelectionRect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(_annotSelectionRect, left);
        Canvas.SetTop(_annotSelectionRect, top);
        overlay.Children.Add(_annotSelectionRect);

        foreach (var (_, hx, hy) in EnumerateAnnotHandleCenters(info, page))
        {
            var handle = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.White),
                Stroke = new SolidColorBrush(Colors.DodgerBlue),
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(handle, hx - 5);
            Canvas.SetTop(handle, hy - 5);
            overlay.Children.Add(handle);
            _annotResizeHandleVisuals.Add(handle);
        }
    }

    private void ClearAnnotSelectionVisual()
    {
        void RemoveFrom(Canvas overlay)
        {
            if (_annotSelectionRect is not null)
            {
                overlay.Children.Remove(_annotSelectionRect);
            }

            foreach (var handle in _annotResizeHandleVisuals)
            {
                overlay.Children.Remove(handle);
            }
        }

        if (_selectedAnnot is not null &&
            _pageOverlays.TryGetValue(_selectedAnnot.PageIndex, out var overlay))
        {
            RemoveFrom(overlay);
        }
        else
        {
            foreach (var pageOverlay in _pageOverlays.Values)
            {
                RemoveFrom(pageOverlay);
            }
        }

        _annotSelectionRect = null;
        _annotResizeHandleVisuals.Clear();
    }

    private async Task ConfigureAnnotationAuthorAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for author dialog.");

        var box = new TextBox
        {
            Text = _annotationAuthor,
            PlaceholderText = "Author name",
            Width = 260,
        };
        var dialog = new ContentDialog
        {
            Title = "Annotation author",
            Content = box,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _annotationAuthor = string.IsNullOrWhiteSpace(box.Text)
            ? Environment.UserName
            : box.Text.Trim();
        _status.Text = $"Annotation author set to {_annotationAuthor}.";
    }

    private async Task EditSelectedAnnotationContentsAsync()
    {
        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = "Select a sticky note, text box, or callout to edit.";
            return;
        }

        if (!item.IsStickyNote && !item.IsTextBox && !item.IsCallout)
        {
            _status.Text = "Edit applies to sticky notes, text boxes, and callouts.";
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for edit dialog.");

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 140,
            Text = item.Contents ?? string.Empty,
            PlaceholderText = item.IsStickyNote ? "Note text" : "Text contents",
        };
        var title = item.IsCallout ? "Edit callout" : item.IsStickyNote ? "Edit sticky note" : "Edit text box";
        var dialog = new ContentDialog
        {
            Title = title,
            Content = box,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Edit cancelled.";
            return;
        }

        try
        {
            await _annotations.SetContentsAsync(
                _document,
                item.PageIndex,
                item.AnnotIndex,
                box.Text ?? string.Empty);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            var updated = item with { Contents = box.Text ?? string.Empty };
            _selectedAnnot = updated;
            SyncSidebarSelection(updated);
            DrawAnnotSelection(updated);
            _status.Text = $"Updated {FormatAnnotationLabel(updated)}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Edit annotation failed: " + ex.Message;
        }
    }

    private async Task DuplicateSelectedAnnotationAsync()
    {
        var index = _annotationList.SelectedIndex;
        if (index < 0 || index >= _annotationItems.Count)
        {
            if (_selectedAnnot is null)
            {
                _status.Text = "Select an annotation to duplicate.";
                return;
            }
        }

        var item = index >= 0 && index < _annotationItems.Count
            ? _annotationItems[index]
            : _selectedAnnot!;
        try
        {
            var copy = await _annotations.DuplicateAsync(_document, item.PageIndex, item.AnnotIndex);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _selectedAnnot = copy;
            SyncSidebarSelection(copy);
            DrawAnnotSelection(copy);
            _status.Text = $"Duplicated {FormatAnnotationLabel(item)}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Duplicate annotation failed: " + ex.Message;
        }
    }

    private bool TryGetSelectedAnnotation(out PdfAnnotationInfo item)
    {
        var index = _annotationList.SelectedIndex;
        if (index >= 0 && index < _annotationItems.Count)
        {
            item = _annotationItems[index];
            return true;
        }

        if (_selectedAnnot is not null)
        {
            item = _selectedAnnot;
            return true;
        }

        item = null!;
        return false;
    }

    private void CopySelectedAnnotationToClipboard()
    {
        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = "Select an annotation to copy.";
            return;
        }

        _annotClipboard = (item.PageIndex, item.AnnotIndex);
        _annotClipboardIsCut = false;
        _status.Text = $"Copied {FormatAnnotationLabel(item)}.";
    }

    private void CutSelectedAnnotationToClipboard()
    {
        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = "Select an annotation to cut.";
            return;
        }

        _annotClipboard = (item.PageIndex, item.AnnotIndex);
        _annotClipboardIsCut = true;
        _status.Text = $"Cut {FormatAnnotationLabel(item)} (removed on paste).";
    }

    private async Task PasteAnnotationClipboardAsync()
    {
        if (_annotClipboard is not { } clip)
        {
            _status.Text = "Annotation clipboard is empty.";
            return;
        }

        try
        {
            var copy = await _annotations.DuplicateAsync(_document, clip.PageIndex, clip.AnnotIndex);
            var copyPage = copy.PageIndex;
            var copyIndex = copy.AnnotIndex;
            var wasCut = _annotClipboardIsCut;
            if (wasCut)
            {
                await _annotations.RemoveAsync(_document, clip.PageIndex, clip.AnnotIndex);
                if (clip.PageIndex == copyPage && clip.AnnotIndex < copyIndex)
                {
                    copyIndex--;
                }

                _annotClipboard = null;
                _annotClipboardIsCut = false;
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            var pasted = _annotationItems.FirstOrDefault(a => a.PageIndex == copyPage && a.AnnotIndex == copyIndex)
                ?? copy with { AnnotIndex = copyIndex };
            _selectedAnnot = pasted;
            SyncSidebarSelection(pasted);
            DrawAnnotSelection(pasted);
            _status.Text = wasCut
                ? $"Pasted {FormatAnnotationLabel(pasted)} (cut)."
                : $"Pasted {FormatAnnotationLabel(pasted)}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Paste annotation failed: " + ex.Message;
        }
    }

    private async Task SetSelectedAnnotationOpacityAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for opacity dialog.");

        var index = _annotationList.SelectedIndex;
        PdfAnnotationInfo? item = index >= 0 && index < _annotationItems.Count
            ? _annotationItems[index]
            : _selectedAnnot;
        if (item is null)
        {
            _status.Text = "Select an annotation to change opacity.";
            return;
        }

        var current = item.Color?.A / 255.0 ?? 1.0;
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(current * 100, 0, 100),
            Width = 280,
            TickFrequency = 5,
            IsThumbToolTipEnabled = true,
        };
        var label = new TextBlock { Text = $"Opacity: {(int)slider.Value}%", Margin = new Thickness(0, 0, 0, 8) };
        slider.ValueChanged += (_, args) =>
        {
            label.Text = $"Opacity: {(int)args.NewValue}%";
        };
        var panel = new StackPanel { Spacing = 4, Children = { label, slider } };
        var dialog = new ContentDialog
        {
            Title = $"Opacity — {FormatAnnotationLabel(item)}",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _annotations.SetOpacityAsync(
                _document,
                item.PageIndex,
                item.AnnotIndex,
                (float)(slider.Value / 100.0));
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = $"Opacity set to {(int)slider.Value}%.";
        }
        catch (Exception ex)
        {
            _status.Text = "Opacity failed: " + ex.Message;
        }
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
        ClearRedactionMode();
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
        _ocrOverlays.Clear();
        _redactionOverlays.Clear();
        _ocrVisualsByPage.Clear();
        _selectedOcrIndices.Clear();
        UpdateOcrOverlayChrome();
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

        if (_formOverlayMode)
        {
            DrawFormOverlays();
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
        var encrypted = _document.IsEncrypted ? "    Encrypted" : string.Empty;
        _status.Text =
            $"Page {CurrentPageIndex + 1} / {_document.PageCount}    Zoom {(int)Math.Round(_scale * 100)}%    {_layoutMode}{encrypted}";
    }

    private async Task ExportPagesAsImagesAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for export.");

        var formatBox = new ComboBox
        {
            Width = 180,
            SelectedIndex = 0,
            Items = { "PNG", "JPEG", "WebP", "TIFF", "BMP", "GIF", "AVIF", "JPEG 2000" },
        };
        var dpiBox = new TextBox { Width = 80, Text = "144" };
        var qualityBox = new Slider
        {
            Minimum = 1,
            Maximum = 100,
            Value = 85,
            Width = 180,
            Header = "JPEG/WebP/AVIF quality",
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"Export {indexes.Count} page(s) as image(s)" },
                new TextBlock { Text = "Format" },
                formatBox,
                new TextBlock { Text = "Render DPI" },
                dpiBox,
                qualityBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Export pages",
            Content = panel,
            PrimaryButtonText = "Export…",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Export cancelled.";
            return;
        }

        var formatName = formatBox.SelectedItem as string ?? "PNG";
        var (format, extension) = formatName switch
        {
            "JPEG" => (ImageEncodeFormat.Jpeg, ".jpg"),
            "WebP" => (ImageEncodeFormat.Webp, ".webp"),
            "TIFF" => (ImageEncodeFormat.Tiff, ".tif"),
            "BMP" => (ImageEncodeFormat.Bmp, ".bmp"),
            "GIF" => (ImageEncodeFormat.Gif, ".gif"),
            "AVIF" => (ImageEncodeFormat.Avif, ".avif"),
            "JPEG 2000" => (ImageEncodeFormat.Jpeg2000, ".jp2"),
            _ => (ImageEncodeFormat.Png, ".png"),
        };

        if (!double.TryParse(dpiBox.Text, out var dpi) || dpi < 36 || dpi > 600)
        {
            dpi = 144;
        }

        var scale = dpi / 72.0;
        ImageEncodeOptions? options = format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Webp or ImageEncodeFormat.Avif
            ? new ImageEncodeOptions(Quality: (int)qualityBox.Value, EmbedSrgbProfile: true)
            : new ImageEncodeOptions(EmbedSrgbProfile: true);

        try
        {
            var info = _documentInfo.GetInfo(_document);
            options = options with
            {
                Title = info.Title,
                Author = info.Author,
            };
        }
        catch
        {
            // Export still works without Info metadata.
        }

        // Formats without alpha: drop transparency. PNG/WebP/TIFF/AVIF keep BGRA alpha from the render.
        if (format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Jpeg2000 or ImageEncodeFormat.Bmp or ImageEncodeFormat.Gif)
        {
            // Encoder removes alpha for JPEG/JP2; BMP/GIF flatten via Magick defaults.
        }

        var baseName = _document.Path is null
            ? "page"
            : System.IO.Path.GetFileNameWithoutExtension(_document.Path);

        try
        {
            if (indexes.Count == 1)
            {
                var picker = new FileSavePicker();
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeChoices.Add(formatName, [extension]);
                picker.SuggestedFileName = $"{baseName}-p{indexes[0] + 1}";
                var file = await picker.PickSaveFileAsync();
                if (file is null)
                {
                    _status.Text = "Export cancelled.";
                    return;
                }

                _status.Text = "Exporting page…";
                await ExportPageImageAsync(indexes[0], scale, file.Path, format, options);
                _status.Text = $"Exported page {indexes[0] + 1} to {file.Name}.";
                return;
            }

            var folderPicker = new FolderPicker();
            var folderHwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(folderPicker, folderHwnd);
            folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder is null)
            {
                _status.Text = "Export cancelled.";
                return;
            }

            _status.Text = $"Exporting {indexes.Count} pages…";
            var written = 0;
            foreach (var pageIndex in indexes)
            {
                var name = $"{baseName}-p{pageIndex + 1}{extension}";
                var path = System.IO.Path.Combine(folder.Path, name);
                await ExportPageImageAsync(pageIndex, scale, path, format, options);
                written++;
            }

            _status.Text = $"Exported {written} page image(s) to {folder.Name}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed: " + ex.Message;
        }
    }

    private async Task ExportPageImageAsync(
        int pageIndex,
        double scale,
        string path,
        ImageEncodeFormat format,
        ImageEncodeOptions? options)
    {
        using var rendered = await _renderer.RenderPageAsync(
            _document,
            pageIndex,
            new PdfRenderRequest(scale));
        await _imageEncoder.WriteBgraAsync(
            rendered.Pixels.ToArray(),
            rendered.Width,
            rendered.Height,
            path,
            format,
            options);
    }

    private async Task ShowOptimizeDialogAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for optimize.");

        var presetBox = new ComboBox
        {
            Width = 260,
            SelectedIndex = 2,
            Items =
            {
                "Lossless (full rewrite)",
                "High quality (200 DPI)",
                "Balanced (150 DPI)",
                "Small file (96 DPI + strip attachments/metadata)",
                "Custom",
            },
        };

        var aboveDpiBox = new TextBox { Width = 80, Text = "225", IsEnabled = false };
        var targetDpiBox = new TextBox { Width = 80, Text = "150", IsEnabled = false };
        var stripAttachments = new CheckBox { Content = "Remove embedded files", IsEnabled = false };
        var preserveMono = new CheckBox { Content = "Preserve monochrome images", IsChecked = true, IsEnabled = false };
        var stripMetadata = new CheckBox { Content = "Remove metadata", IsEnabled = false };

        void SyncCustomEnabled()
        {
            var custom = presetBox.SelectedIndex == 4;
            aboveDpiBox.IsEnabled = custom;
            targetDpiBox.IsEnabled = custom;
            stripAttachments.IsEnabled = custom;
            preserveMono.IsEnabled = custom;
            stripMetadata.IsEnabled = custom;
            if (!custom)
            {
                var preset = SelectedPreset();
                var opts = PdfOptimizeOptions.FromPreset(preset);
                aboveDpiBox.Text = opts.DownsampleAboveDpi.ToString("0");
                targetDpiBox.Text = opts.TargetDpi.ToString("0");
                stripAttachments.IsChecked = opts.RemoveEmbeddedAttachments;
                preserveMono.IsChecked = opts.PreserveMonochrome;
                stripMetadata.IsChecked = opts.RemoveMetadata;
            }
        }

        presetBox.SelectionChanged += (_, _) => SyncCustomEnabled();
        SyncCustomEnabled();

        var estimateText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Text = "Choose a preset (or Custom), then Estimate or Apply. JPEG rewrite is pending a PDFiumCore FILEACCESS fix.",
        };

        PdfOptimizePreset SelectedPreset() => presetBox.SelectedIndex switch
        {
            0 => PdfOptimizePreset.Lossless,
            1 => PdfOptimizePreset.HighQuality,
            3 => PdfOptimizePreset.SmallFile,
            4 => PdfOptimizePreset.Custom,
            _ => PdfOptimizePreset.Balanced,
        };

        PdfOptimizeOptions BuildOptions()
        {
            var preset = SelectedPreset();
            if (preset != PdfOptimizePreset.Custom)
            {
                return PdfOptimizeOptions.FromPreset(preset);
            }

            _ = double.TryParse(aboveDpiBox.Text, out var above);
            _ = double.TryParse(targetDpiBox.Text, out var target);
            if (above < 36)
            {
                above = 225;
            }

            if (target < 36)
            {
                target = 150;
            }

            return new PdfOptimizeOptions(
                Preset: PdfOptimizePreset.Custom,
                DownsampleImages: true,
                DownsampleAboveDpi: above,
                TargetDpi: target,
                JpegQuality: 75,
                PreserveMonochrome: preserveMono.IsChecked == true,
                RemoveEmbeddedAttachments: stripAttachments.IsChecked == true,
                RemoveMetadata: stripMetadata.IsChecked == true);
        }

        var estimateButton = new Button { Content = "Estimate", Margin = new Thickness(0, 8, 8, 0) };
        estimateButton.Click += (_, _) =>
        {
            try
            {
                var estimate = _optimize.Estimate(_document, BuildOptions());
                estimateText.Text =
                    $"Current: {FormatBytes(estimate.CurrentBytes)} · Estimated: {FormatBytes(estimate.EstimatedBytes)} · "
                    + $"{estimate.ImagesEligibleForDownsample} image(s) above DPI threshold · "
                    + $"{estimate.AttachmentCount} embedded file(s).";
            }
            catch (Exception ex)
            {
                estimateText.Text = "Estimate failed: " + ex.Message;
            }
        };

        var customRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Above DPI", VerticalAlignment = VerticalAlignment.Center },
                aboveDpiBox,
                new TextBlock { Text = "Target DPI", VerticalAlignment = VerticalAlignment.Center },
                targetDpiBox,
            },
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Preset" },
                presetBox,
                customRow,
                stripAttachments,
                preserveMono,
                stripMetadata,
                estimateButton,
                estimateText,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Optimize PDF",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Optimize cancelled.";
            return;
        }

        try
        {
            _status.Text = "Optimizing…";
            var result = await _optimize.OptimizeAsync(_document, BuildOptions());
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            _status.Text =
                $"Optimized: {result.ImagesDownsampled} image(s) downsampled, "
                + $"{result.AttachmentsRemoved} attachment(s) removed; "
                + $"{FormatBytes(result.BytesBefore)} → {FormatBytes(result.BytesAfter)}. Save to keep.";
        }
        catch (Exception ex)
        {
            _status.Text = "Optimize failed: " + ex.Message;
        }

        static string FormatBytes(long size) =>
            size < 1024
                ? $"{size} B"
                : size < 1024 * 1024
                    ? $"{size / 1024.0:0.#} KB"
                    : $"{size / (1024.0 * 1024.0):0.##} MB";
    }

    private async Task ShowDocumentInfoAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for document info.");

        PdfDocumentInfo info;
        try
        {
            info = _documentInfo.GetInfo(_document);
        }
        catch (Exception ex)
        {
            _status.Text = "Info failed: " + ex.Message;
            return;
        }

        static string Val(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
        static string Bytes(long? size) =>
            size is null ? "—" : size.Value < 1024
                ? $"{size.Value} B"
                : size.Value < 1024 * 1024
                    ? $"{size.Value / 1024.0:0.#} KB"
                    : $"{size.Value / (1024.0 * 1024.0):0.##} MB";

        var perms = info.Permissions;
        var permissionLines =
            $"Print: {(perms.CanPrint ? "yes" : "no")}\n"
            + $"Modify: {(perms.CanModify ? "yes" : "no")}\n"
            + $"Copy: {(perms.CanCopy ? "yes" : "no")}\n"
            + $"Annotate: {(perms.CanAnnotate ? "yes" : "no")}\n"
            + $"Fill forms: {(perms.CanFillForms ? "yes" : "no")}\n"
            + $"Assemble: {(perms.CanAssemble ? "yes" : "no")}\n"
            + $"High-quality print: {(perms.CanPrintHighQuality ? "yes" : "no")}";

        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460,
            Text =
                $"Title: {Val(info.Title)}\n"
                + $"Author: {Val(info.Author)}\n"
                + $"Subject: {Val(info.Subject)}\n"
                + $"Keywords: {Val(info.Keywords)}\n"
                + $"Creator: {Val(info.Creator)}\n"
                + $"Producer: {Val(info.Producer)}\n"
                + $"Created: {Val(info.CreationDate)}\n"
                + $"Modified: {Val(info.ModificationDate)}\n"
                + $"Pages: {info.PageCount}\n"
                + $"PDF version: {Val(info.PdfVersion)}\n"
                + $"Page size: {(info.PageWidthPoints is null || info.PageHeightPoints is null
                    ? "—"
                    : $"{info.PageWidthPoints:0.#} × {info.PageHeightPoints:0.#} pt")}\n"
                + $"Fonts: {(info.Fonts.Count == 0 ? "—" : string.Join(", ", info.Fonts))}\n"
                + $"Embedded files: {info.EmbeddedAttachmentCount}\n"
                + $"File: {Val(info.FilePath is null ? null : System.IO.Path.GetFileName(info.FilePath))}\n"
                + $"Size: {Bytes(info.FileSizeBytes)}\n"
                + $"Encrypted: {(info.IsEncrypted ? "yes" : "no")}\n"
                + $"Security handler revision: {(info.SecurityHandlerRevision < 0 ? "none" : info.SecurityHandlerRevision.ToString())}\n"
                + $"Permission flags: 0x{info.PermissionFlags:X8}\n\n"
                + "Permissions (PDF flags — enforcement is advisory):\n"
                + permissionLines,
        };

        var dialog = new ContentDialog
        {
            Title = "Document info",
            Content = new ScrollViewer
            {
                Content = body,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = "Edit…",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await EditDocumentInfoAsync(info);
            return;
        }

        _status.Text = info.IsEncrypted
            ? "Document is encrypted — permissions shown are advisory."
            : "Document info.";
    }

    private async Task EditDocumentInfoAsync(PdfDocumentInfo current)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException("Main window unavailable for document info edit.");

        var titleBox = new TextBox { Text = current.Title ?? string.Empty, Width = 320 };
        var authorBox = new TextBox { Text = current.Author ?? string.Empty, Width = 320 };
        var subjectBox = new TextBox { Text = current.Subject ?? string.Empty, Width = 320 };
        var keywordsBox = new TextBox { Text = current.Keywords ?? string.Empty, Width = 320 };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Title" },
                titleBox,
                new TextBlock { Text = "Author" },
                authorBox,
                new TextBlock { Text = "Subject" },
                subjectBox,
                new TextBlock { Text = "Keywords" },
                keywordsBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Edit document info",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Info edit cancelled.";
            return;
        }

        try
        {
            _documentInfo.SetInfo(
                _document,
                new PdfDocumentInfoUpdate(
                    Title: titleBox.Text,
                    Author: authorBox.Text,
                    Subject: subjectBox.Text,
                    Keywords: keywordsBox.Text));
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            _status.Text = "Document info updated. Save the PDF to keep changes on disk.";
        }
        catch (Exception ex)
        {
            _status.Text = "Info edit failed: " + ex.Message;
        }
    }
}
