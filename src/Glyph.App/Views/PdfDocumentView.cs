using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.App.Capture;
using Glyph.App.Printing;
using Glyph.Core.Documents;
using Glyph.Core.IO;
using Glyph.Core.Ocr;
using Glyph.Core.Pdf;
using Glyph.Core.Printing;
using Glyph.Core.Signatures;
using Glyph.Core.Text;
using Glyph.Infrastructure.Forms;
using Glyph.Infrastructure.Settings;
using Glyph.Ocr.Abstractions;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Annotations;
using Glyph.Pdf.Editing;
using Glyph.Pdf.Forms;
using Glyph.Pdf.Info;
using Glyph.Pdf.Rendering;
using Glyph.Pdf.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    private const int OcrMaxEdgePixels = 2048;

    private readonly IPdfDocument _document;
    private readonly IPdfRenderer _renderer;
    private readonly PageRenderCache _cache;
    private readonly PdfSearchCoordinator _searchCoordinator;
    private readonly IPdfTextExtractor _textExtractor;
    private readonly IPdfOutlineService _outlineService;
    private readonly IPdfOutlineExportService _outlineExport;
    private readonly IPdfLinkService _linkService;
    private readonly IPdfPageEditor _pageEditor;
    private readonly IPdfAnnotationService _annotations;
    private readonly IPdfRedactionService _redaction;
    private readonly IPdfDocumentInfoService _documentInfo;
    private readonly IPdfOptimizeService _optimize;
    private readonly IPdfSecurityService _security;
    private readonly IPdfExportService _export;
    private readonly ISignatureLibrary _signatures;
    private readonly IPdfFormStore _forms;
    private readonly IFormValueHistory _formValueHistory;
    private readonly IFormAutofillProfileStore _formProfile;
    private readonly IPdfDocumentFactory _documentFactory;
    private readonly IOcrEngine? _ocr;
    private readonly Button _ocrCancelButton;
    private readonly ProgressBar _jobProgress;
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
    private readonly Action? _onEdited;
    private bool _hasUnsavedContent;
    private readonly PageSelection _pageSelection = new();
    private readonly string _documentKey;
    private readonly string _thumbnailKey;
    private Border? _dropHighlightBorder;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _continuousHost;
    private readonly StackPanel _spreadHost;
    private readonly StackPanel _contactSheetHost;
    private bool _contactSheetMode;
    private readonly StackPanel _thumbnailHost;
    private readonly ScrollViewer _thumbnailScroll;
    private Grid? _sidePanel;
    private ComboBox? _sidebarModeBox;
    private readonly List<UIElement> _sidebarSections = new();
    private readonly TreeView _outlineTree;
    private readonly ListView _bookmarkList;
    private readonly ListView _searchResults;
    private StackPanel? _toolbar;
    private IReadOnlyList<UIElement>? _toolbarDefaults;
    private readonly ListView _annotationList;
    private readonly ListView _attachmentList;
    private readonly TextBlock _propertiesSummary;
    private IReadOnlyList<PdfEmbeddedAttachmentInfo> _attachmentItems = [];
    private readonly TextBox _searchBox;
    private readonly TextBox _gotoBox;
    private readonly CheckBox _caseSensitiveBox;
    private readonly ComboBox _searchSortBox;
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
    private double _thumbnailWidth = ThumbnailWidthConstraints.Default;
    private double _scale = 1.25;
    private PageLayoutMode _layoutMode = PageLayoutMode.Continuous;
    private int _renderGeneration;
    private long _lastIntermediateRenderTick;
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
    private readonly AnnotationUndoStack _strokeUndoStack = new();
    /// <summary>Previous form field values for F49-11 Ctrl+Z undo.</summary>
    private readonly FormFillUndoStack _formUndoStack = new();
    /// <summary>Previous Info dictionary fields for F49-10 Ctrl+Z undo after Edit document info.</summary>
    private readonly DocumentInfoUndoStack _infoUndoStack = new();
    private PdfAnnotationColor _drawStrokeColor = PdfAnnotationColor.InkRed;
    private float _drawStrokeWidth = 2f;
    private PdfInkLineStyle _drawInkLineStyle = PdfInkLineStyle.Solid;
    private PdfArrowheadStyle _drawArrowheadStyle = PdfArrowheadStyle.Open;
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
    private Button? _bubbleButton;
    private Button? _loupeButton;
    private Button? _calloutButton;
    private readonly List<FrameworkElement> _loupePopupVisuals = [];
    private Button? _redactButton;
    private bool _calloutMode;
    private bool _calloutTipEditMode;
    private PdfAnnotationInfo? _calloutTipTarget;
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
    private readonly List<PdfAnnotationInfo> _selectedAnnots = [];
    private readonly List<(PdfAnnotationInfo Info, PdfRect OriginBounds)> _multiDragOrigins = [];
    private bool _annotDragging;
    private PdfRect _annotDragOriginBounds;
    private PdfPagePoint _annotDragOriginEndpointA;
    private PdfPagePoint _annotDragOriginEndpointB;
    private Windows.Foundation.Point _annotDragOriginUi;
    private string? _annotResizeHandle;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _annotSelectionRect;
    private readonly List<FrameworkElement> _annotSelectionVisuals = [];
    private readonly List<FrameworkElement> _annotResizeHandleVisuals = [];
    private readonly HashSet<(int PageIndex, int AnnotIndex)> _expandedStickyNotes = [];
    private readonly List<FrameworkElement> _stickyNotePopupVisuals = [];
    /// <summary>In-app annotation clipboard (page/annot index). Cut removes the source on paste.</summary>
    private (int PageIndex, int AnnotIndex)? _annotClipboard;
    private bool _annotClipboardIsCut;
    private string _searchQuery = string.Empty;
    private bool _searchCaseSensitive;
    private bool _cropMode;
    private bool _zoomAreaMode;
    private Button? _zoomAreaButton;
    private bool _viewLoupeMode;
    private Button? _viewLoupeButton;
    private Border? _viewLoupePopup;
    private Canvas? _viewLoupeHost;
    private bool _presentationMode;
    private Button? _presentButton;
    private PageLayoutMode _layoutBeforePresentation = PageLayoutMode.Continuous;
    private double _scaleBeforePresentation = 1.25;
    private bool _toolbarWasVisible = true;
    private Grid? _bodyGrid;
    private DispatcherTimer? _presentationTimer;
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
        IPdfOutlineExportService outlineExport,
        IPdfLinkService linkService,
        IPdfPageEditor pageEditor,
        IPdfAnnotationService annotations,
        IPdfRedactionService redaction,
        IPdfDocumentInfoService documentInfo,
        IPdfOptimizeService optimize,
        IPdfSecurityService security,
        IPdfExportService pdfExport,
        ISignatureLibrary signatures,
        IPdfFormStore forms,
        IFormValueHistory formValueHistory,
        IFormAutofillProfileStore formProfile,
        IPdfDocumentFactory documentFactory,
        DocumentViewState? viewState = null,
        Window? ownerWindow = null,
        IOcrEngine? ocr = null,
        Action? onEdited = null)
    {
        _document = document;
        _renderer = renderer;
        _cache = cache;
        _searchCoordinator = new PdfSearchCoordinator(searchService);
        if (!string.IsNullOrWhiteSpace(document.Path))
        {
            _ = _searchCoordinator.WarmIndexAsync(document.Path);
        }

        _textExtractor = textExtractor;
        _outlineService = outlineService;
        _outlineExport = outlineExport;
        _linkService = linkService;
        _pageEditor = pageEditor;
        _annotations = annotations;
        _redaction = redaction;
        _documentInfo = documentInfo;
        _optimize = optimize;
        _security = security;
        _export = pdfExport;
        _signatures = signatures;
        _forms = forms;
        _formValueHistory = formValueHistory;
        _formProfile = formProfile;
        _documentFactory = documentFactory;
        _ocr = ocr;
        _ownerWindow = ownerWindow;
        _onEdited = onEdited;
        _viewState = viewState ?? new DocumentViewState();
        var settings = TryGetSettings();
        if (!string.IsNullOrWhiteSpace(settings?.AnnotationAuthor))
        {
            _annotationAuthor = settings.AnnotationAuthor.Trim();
        }

        ApplyAnnotationDefaults(settings);
        if (settings is not null)
        {
            _thumbnailWidth = ThumbnailWidthConstraints.Clamp(settings.ThumbnailWidth);
        }

        var defaultZoom = settings?.DefaultZoom > 0 ? settings.DefaultZoom : 1.25;
        _scale = PdfZoomCalculator.Clamp(_viewState.Zoom <= 0 ? defaultZoom : _viewState.Zoom);
        _layoutMode = _viewState.PageLayout;
        CurrentPageIndex = Math.Clamp(_viewState.CurrentPageIndex, 0, Math.Max(0, document.PageCount - 1));
        _pageSelection.SelectOnly(CurrentPageIndex);
        _documentKey = PageDragSemantics.DocumentKey(document.Path, document.GetHashCode());
        _thumbnailKey = _documentKey + "|thumb";

        _continuousHost = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
        _spreadHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _contactSheetHost = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
        _scrollViewer = new ScrollViewer
        {
            Content = _continuousHost,
            ZoomMode = ZoomMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        _scrollViewer.PointerWheelChanged += ScrollViewer_PointerWheelChanged;
        // Precision-touchpad pinch often arrives as Ctrl+wheel; Manipulation Scale covers direct pinch
        // (TouchpadGesturePolicy.PreferPinchZoom).
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
        _outlineTree.KeyDown += OutlineTree_KeyDown;
        AutomationProperties.SetName(_outlineTree, PdfViewerAutomationNames.TableOfContents);

        _searchBox = new TextBox { PlaceholderText = PdfDialogPlaceholders.FindInDocument, Width = 160 };
        _searchBox.KeyDown += SearchBox_KeyDown;
        _caseSensitiveBox = new CheckBox { Content = PdfViewerChromeLabels.Aa, VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(_caseSensitiveBox, PdfViewerTooltips.MatchCase);
        _searchSortBox = new ComboBox
        {
            Width = 120,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new[]
            {
                PdfViewerChromeLabels.SortPageOrder,
                PdfViewerChromeLabels.SortRelevance,
            },
            SelectedIndex = 0,
        };
        ToolTipService.SetToolTip(_searchSortBox, PdfViewerTooltips.SortFindResults);
        _searchSortBox.SelectionChanged += async (_, _) => await ResortSearchHitsAsync();
        var searchButton = new Button { Content = PdfViewerChromeLabels.Find };
        searchButton.Click += async (_, _) => await RunSearchAsync();
        var findSelection = new Button { Content = PdfViewerChromeLabels.FindSelection };
        findSelection.Click += async (_, _) => await SearchSelectedTextAsync();
        ToolTipService.SetToolTip(findSelection, PdfViewerTooltips.SearchForTheCurrentlySelectedText);
        var ocrPage = new Button { Content = PdfViewerChromeLabels.Ocr };
        ocrPage.Click += async (_, _) => await OnOcrButtonClickAsync();
        ToolTipService.SetToolTip(ocrPage, PdfViewerTooltips.RunOfflineOcrOnSelectedPages);
        _ocrCancelButton = new Button { Content = PerformanceBehaviorPolicy.CancelOcrButton, Visibility = Visibility.Collapsed };
        _ocrCancelButton.Click += (_, _) => CancelOcr();
        ToolTipService.SetToolTip(_ocrCancelButton, PdfViewerTooltips.CancelTheInFlightOcrJob);
        _jobProgress = new ProgressBar
        {
            Width = 120,
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(_jobProgress, PdfViewerTooltips.LongRunningJobProgress);
        _copyOcrButton = new Button { Content = PdfViewerChromeLabels.CopyOcr, Visibility = Visibility.Collapsed };
        _copyOcrButton.Click += (_, _) => CopySelectedOcrText();
        ToolTipService.SetToolTip(_copyOcrButton, PdfViewerTooltips.CopySelectedOcrWordsOrAll);
        _clearOcrOverlayButton = new Button { Content = PdfViewerChromeLabels.ClearOcr, Visibility = Visibility.Collapsed };
        _clearOcrOverlayButton.Click += (_, _) => ClearOcrOverlays();
        ToolTipService.SetToolTip(_clearOcrOverlayButton, PdfViewerTooltips.HideOcrWordOverlaysKeepsFind);
        _ocrSavePdfButton = new Button { Content = PdfViewerChromeLabels.OcrToPdf, Visibility = Visibility.Collapsed };
        _ocrSavePdfButton.Click += async (_, _) => await SaveSearchableOcrPdfAsync();
        ToolTipService.SetToolTip(_ocrSavePdfButton, PdfViewerTooltips.ExportOcrDPagesAsA);
        _ocrEntitiesButton = new Button { Content = PdfViewerChromeLabels.Entities, Visibility = Visibility.Collapsed };
        _ocrEntitiesButton.Click += async (_, _) => await ShowOcrEntitiesAsync();
        ToolTipService.SetToolTip(_ocrEntitiesButton, PdfViewerTooltips.ReviewDetectedUrlsEmailsPhonesAddresses);
        var clearSearch = new Button { Content = PdfViewerChromeLabels.Clear };
        ToolTipService.SetToolTip(clearSearch, PdfViewerTooltips.ClearSearchResults);
        clearSearch.Click += async (_, _) => await ClearSearchAsync();
        var prevMatch = new Button { Content = PdfViewerChromeLabels.NavPrev };
        var nextMatch = new Button { Content = PdfViewerChromeLabels.NavNext };
        ToolTipService.SetToolTip(prevMatch, PdfViewerTooltips.PreviousMatch);
        ToolTipService.SetToolTip(nextMatch, PdfViewerTooltips.NextMatch);
        prevMatch.Click += async (_, _) => await GoToHitAsync(_activeHitIndex - 1);
        nextMatch.Click += async (_, _) => await GoToHitAsync(_activeHitIndex + 1);
        _searchResults = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
        };
        _searchResults.SelectionChanged += SearchResults_SelectionChanged;
        _searchResults.RightTapped += SearchResults_RightTapped;
        _annotationList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Extended,
        };
        _annotationList.SelectionChanged += AnnotationList_SelectionChanged;
        _annotationList.RightTapped += AnnotationList_RightTapped;
        _attachmentList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
        };
        _attachmentList.RightTapped += AttachmentList_RightTapped;

        _bookmarkList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
        };
        _bookmarkList.ItemClick += async (_, args) =>
        {
            if (args.ClickedItem is BookmarkListItem item)
            {
                await GoToPageAsync(item.PageIndex, recordHistory: true);
            }
        };
        _bookmarkList.RightTapped += BookmarkList_RightTapped;

        var bookmarkHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.Bookmarks,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
        var addBookmark = new Button { Content = PdfViewerChromeLabels.Plus, Width = 28, Padding = new Thickness(0) };
        ToolTipService.SetToolTip(addBookmark, PdfViewerTooltips.AddBookmarkAtCurrentPage);
        AutomationProperties.SetName(addBookmark, PdfViewerAutomationNames.AddBookmarkAtCurrentPage);
        addBookmark.Click += async (_, _) => await AddBookmarkAsync();
        var renameBookmark = new Button { Content = PdfViewerChromeLabels.Rename, Padding = new Thickness(4, 2, 4, 2) };
        ToolTipService.SetToolTip(renameBookmark, PdfViewerTooltips.RenameSelectedBookmark);
        AutomationProperties.SetName(renameBookmark, PdfViewerAutomationNames.RenameSelectedBookmark);
        renameBookmark.Click += async (_, _) => await RenameSelectedBookmarkAsync();
        var deleteBookmark = new Button { Content = PdfViewerChromeLabels.DeleteShort, Padding = new Thickness(4, 2, 4, 2) };
        ToolTipService.SetToolTip(deleteBookmark, PdfViewerTooltips.DeleteSelectedBookmark);
        AutomationProperties.SetName(deleteBookmark, PdfViewerAutomationNames.DeleteSelectedBookmark);
        deleteBookmark.Click += (_, _) => DeleteSelectedBookmark();
        var upBookmark = new Button { Content = PdfViewerChromeLabels.MoveUp, Width = 28, Padding = new Thickness(0) };
        ToolTipService.SetToolTip(upBookmark, PdfViewerTooltips.MoveBookmarkUp);
        AutomationProperties.SetName(upBookmark, PdfViewerAutomationNames.MoveBookmarkUp);
        upBookmark.Click += (_, _) => MoveSelectedBookmark(-1);
        var downBookmark = new Button { Content = PdfViewerChromeLabels.MoveDown, Width = 28, Padding = new Thickness(0) };
        ToolTipService.SetToolTip(downBookmark, PdfViewerTooltips.MoveBookmarkDown);
        AutomationProperties.SetName(downBookmark, PdfViewerAutomationNames.MoveBookmarkDown);
        downBookmark.Click += (_, _) => MoveSelectedBookmark(1);
        var exportBookmarks = new Button { Content = PdfViewerChromeLabels.Pdf, Padding = new Thickness(4, 2, 4, 2) };
        ToolTipService.SetToolTip(exportBookmarks, PdfViewerTooltips.WriteBookmarksIntoThisPdfAs);
        AutomationProperties.SetName(exportBookmarks, PdfViewerAutomationNames.ExportBookmarksToPdfOutline);
        exportBookmarks.Click += async (_, _) => await ExportBookmarksToPdfOutlineAsync();
        bookmarkHeader.Children.Add(addBookmark);
        bookmarkHeader.Children.Add(renameBookmark);
        bookmarkHeader.Children.Add(deleteBookmark);
        bookmarkHeader.Children.Add(upBookmark);
        bookmarkHeader.Children.Add(downBookmark);
        bookmarkHeader.Children.Add(exportBookmarks);

        var tocHeader = new TextBlock
        {
            Text = PdfViewerTextLabels.Contents,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(8, 8, 8, 4),
        };

        var searchHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.Search,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                _searchSortBox,
            },
        };

        var annotHeaderRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.Annotations,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
        var removeAnnot = new Button { Content = PdfViewerChromeLabels.Delete, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(removeAnnot, PdfViewerTooltips.DeleteSelectedAnnotation);
        removeAnnot.Click += async (_, _) => await RemoveSelectedAnnotationAsync();
        var duplicateAnnot = new Button { Content = PdfViewerChromeLabels.Duplicate, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(duplicateAnnot, PdfViewerTooltips.DuplicateSelectedAnnotationOffsetCopy);
        duplicateAnnot.Click += async (_, _) => await DuplicateSelectedAnnotationAsync();
        var copyAnnot = new Button { Content = PdfViewerChromeLabels.Copy, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(copyAnnot, PdfViewerTooltips.CopySelectedAnnotationCtrlCWhen);
        copyAnnot.Click += (_, _) => CopySelectedAnnotationToClipboard();
        var cutAnnot = new Button { Content = PdfViewerChromeLabels.Cut, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(cutAnnot, PdfViewerTooltips.CutSelectedAnnotationCtrlXWhen);
        cutAnnot.Click += (_, _) => CutSelectedAnnotationToClipboard();
        var pasteAnnot = new Button { Content = PdfViewerChromeLabels.Paste, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(pasteAnnot, PdfViewerTooltips.PasteAnnotationClipboardCtrlVWhen);
        pasteAnnot.Click += async (_, _) => await PasteAnnotationClipboardAsync();
        var editAnnot = new Button { Content = PdfViewerChromeLabels.Edit, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(editAnnot, PdfViewerTooltips.EditContentsOfSelectedStickyNote);
        editAnnot.Click += async (_, _) => await EditSelectedAnnotationContentsAsync();
        var authorAnnot = new Button { Content = PdfViewerChromeLabels.Author, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(authorAnnot, PdfViewerTooltips.SetDefaultAnnotationAuthorNameFor);
        authorAnnot.Click += async (_, _) => await ConfigureAnnotationAuthorAsync();
        annotHeaderRow.Children.Add(duplicateAnnot);
        annotHeaderRow.Children.Add(editAnnot);
        annotHeaderRow.Children.Add(copyAnnot);
        annotHeaderRow.Children.Add(cutAnnot);
        annotHeaderRow.Children.Add(pasteAnnot);
        annotHeaderRow.Children.Add(authorAnnot);
        var expandNote = new Button { Content = PdfViewerChromeLabels.Expand, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(expandNote, PdfViewerTooltips.ExpandSelectedStickyNoteShowPopup);
        expandNote.Click += (_, _) => ExpandSelectedStickyNote();
        annotHeaderRow.Children.Add(expandNote);
        var collapseNote = new Button { Content = PdfViewerChromeLabels.Collapse, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(collapseNote, PdfViewerTooltips.CollapseExpandedStickyNotePopup);
        collapseNote.Click += (_, _) => CollapseSelectedStickyNote();
        annotHeaderRow.Children.Add(collapseNote);
        var exportNotes = new Button { Content = PdfViewerChromeLabels.ExportNotes, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(exportNotes, PdfViewerTooltips.SaveStickyNotesAsAPrintable);
        exportNotes.Click += async (_, _) => await ExportNotesAsync();
        annotHeaderRow.Children.Add(exportNotes);
        var underlineAnnot = new Button { Content = PdfViewerChromeLabels.Underline, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(underlineAnnot, PdfViewerTooltips.ToggleUnderlineOnSelectedTextBox);
        underlineAnnot.Click += async (_, _) => await ToggleSelectedTextUnderlineAsync();
        annotHeaderRow.Children.Add(underlineAnnot);
        var alignAnnot = new Button { Content = PdfViewerChromeLabels.Align, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(alignAnnot, PdfViewerTooltips.SetTextAlignmentLeftCenterRight);
        alignAnnot.Click += async (_, _) => await SetSelectedTextQuaddingAsync();
        annotHeaderRow.Children.Add(alignAnnot);
        var colorAnnot = new Button { Content = PdfViewerChromeLabels.Color, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(colorAnnot, PdfViewerTooltips.ChangeSelectedAnnotationColor);
        colorAnnot.Click += async (_, _) => await SetSelectedAnnotationColorAsync();
        annotHeaderRow.Children.Add(colorAnnot);
        var fillAnnot = new Button { Content = PdfViewerChromeLabels.Fill, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(fillAnnot, PdfViewerTooltips.ChangeFillColorForShapesAnd);
        fillAnnot.Click += async (_, _) => await SetSelectedAnnotationFillAsync();
        annotHeaderRow.Children.Add(fillAnnot);
        var tipAnnot = new Button { Content = PdfViewerChromeLabels.CalloutTip, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(tipAnnot, PdfViewerTooltips.RepositionCalloutPointerTipClickOn);
        tipAnnot.Click += (_, _) => BeginCalloutTipEdit();
        annotHeaderRow.Children.Add(tipAnnot);
        var groupAnnot = new Button { Content = PdfViewerChromeLabels.Group, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(groupAnnot, PdfViewerTooltips.GroupSelectedAnnotationsSoTheyMove);
        groupAnnot.Click += async (_, _) => await GroupSelectedAnnotationsAsync();
        annotHeaderRow.Children.Add(groupAnnot);
        var ungroupAnnot = new Button { Content = PdfViewerChromeLabels.Ungroup, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(ungroupAnnot, PdfViewerTooltips.RemoveGroupFromSelectedAnnotations);
        ungroupAnnot.Click += async (_, _) => await UngroupSelectedAnnotationsAsync();
        annotHeaderRow.Children.Add(ungroupAnnot);
        var opacityAnnot = new Button { Content = PdfViewerChromeLabels.Opacity, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(opacityAnnot, PdfViewerTooltips.ChangeSelectedAnnotationOpacity);
        opacityAnnot.Click += async (_, _) => await SetSelectedAnnotationOpacityAsync();
        annotHeaderRow.Children.Add(opacityAnnot);
        var widthAnnot = new Button { Content = PdfViewerChromeLabels.Width, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(widthAnnot, PdfViewerTooltips.ChangeStrokeOrBorderWidthFor);
        widthAnnot.Click += async (_, _) => await SetSelectedAnnotationBorderWidthAsync();
        annotHeaderRow.Children.Add(widthAnnot);
        var rotateAnnot = new Button { Content = PdfViewerChromeLabels.Rotate, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(rotateAnnot, PdfViewerTooltips.RotateSelectedStampInkShapeOr);
        rotateAnnot.Click += async (_, _) => await RotateSelectedAnnotationAsync();
        annotHeaderRow.Children.Add(rotateAnnot);
        annotHeaderRow.Children.Add(removeAnnot);

        _status = new TextBlock { Opacity = 0.75, FontSize = 12, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

        var propertiesHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.Properties,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
        var propertiesMore = new Button { Content = PdfViewerChromeLabels.MoreEllipsis, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(propertiesMore, PdfViewerTooltips.OpenFullDocumentInfoDialog);
        propertiesMore.Click += async (_, _) => await ShowDocumentInfoAsync();
        var propertiesEdit = new Button { Content = PdfViewerChromeLabels.EditEllipsis, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(propertiesEdit, PdfViewerTooltips.EditTitleAuthorSubjectAndKeywords);
        propertiesEdit.Click += async (_, _) =>
        {
            try
            {
                var info = _documentInfo.GetInfo(_document);
                await EditDocumentInfoAsync(info);
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.EditProperties, ex.Message);
            }
        };
        propertiesHeader.Children.Add(propertiesMore);
        propertiesHeader.Children.Add(propertiesEdit);
        _propertiesSummary = new TextBlock
        {
            Margin = new Thickness(8, 0, 8, 8),
            FontSize = 11,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap,
            Text = PdfViewerTextLabels.LoadingEllipsis,
        };

        var attachmentHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.Attachments,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
        var saveAttachment = new Button { Content = PdfViewerChromeLabels.SaveEllipsis, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(saveAttachment, PdfViewerTooltips.SaveSelectedEmbeddedAttachmentToDisk);
        saveAttachment.Click += async (_, _) => await SaveSelectedAttachmentAsync();
        var refreshAttachments = new Button { Content = PdfViewerChromeLabels.RotateRefresh, Padding = new Thickness(6, 2, 6, 2) };
        ToolTipService.SetToolTip(refreshAttachments, PdfViewerTooltips.RefreshAttachmentList);
        refreshAttachments.Click += (_, _) => RefreshAttachmentsSidebar();
        attachmentHeader.Children.Add(saveAttachment);
        attachmentHeader.Children.Add(refreshAttachments);

        static Grid BuildSidebarSection(UIElement header, UIElement body)
        {
            var section = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                },
            };
            if (header is FrameworkElement headerFe)
            {
                Grid.SetRow(headerFe, 0);
            }

            section.Children.Add(header);
            if (body is FrameworkElement bodyFe)
            {
                Grid.SetRow(bodyFe, 1);
            }

            section.Children.Add(body);
            return section;
        }

        var pagesSection = BuildSidebarSection(BuildPagesHeader(), _thumbnailScroll);
        var tocSection = BuildSidebarSection(tocHeader, _outlineTree);
        var bookmarksSection = BuildSidebarSection(bookmarkHeader, _bookmarkList);
        var searchSection = BuildSidebarSection(searchHeader, _searchResults);
        var annotSection = BuildSidebarSection(annotHeaderRow, _annotationList);
        var propertiesSection = BuildSidebarSection(propertiesHeader, _propertiesSummary);
        var attachmentSection = BuildSidebarSection(attachmentHeader, _attachmentList);

        _sidebarSections.Clear();
        _sidebarSections.Add(pagesSection);
        _sidebarSections.Add(tocSection);
        _sidebarSections.Add(bookmarksSection);
        _sidebarSections.Add(searchSection);
        _sidebarSections.Add(annotSection);
        _sidebarSections.Add(propertiesSection);
        _sidebarSections.Add(attachmentSection);

        _sidebarModeBox = new ComboBox
        {
            Margin = new Thickness(8, 8, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = SidebarModeCombo.Labels.ToArray(),
            SelectedIndex = SidebarModeCombo.FromSidebarMode(_viewState.SidebarMode),
        };
        AutomationProperties.SetName(_sidebarModeBox, PdfViewerAutomationNames.SidebarMode);
        ToolTipService.SetToolTip(_sidebarModeBox, PdfViewerTooltips.SwitchSidebarModeWithoutOpeningAnother);
        _sidebarModeBox.SelectionChanged += (_, _) => ApplySidebarMode();

        var sideContentHost = new Grid();
        foreach (var section in _sidebarSections)
        {
            sideContentHost.Children.Add(section);
        }

        var sidePanel = new Grid
        {
            Width = Math.Max(180, _thumbnailWidth + 48),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        _sidePanel = sidePanel;
        Grid.SetRow(_sidebarModeBox, 0);
        sidePanel.Children.Add(_sidebarModeBox);
        Grid.SetRow(sideContentHost, 1);
        sidePanel.Children.Add(sideContentHost);
        ApplySidebarMode();

        _status = new TextBlock { Opacity = 0.75, FontSize = 12, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        _gotoBox = new TextBox { PlaceholderText = PdfDialogPlaceholders.GotoPage, Width = 48 };
        _gotoBox.KeyDown += GotoBox_KeyDown;
        _layoutBox = new ComboBox
        {
            Width = 150,
            ItemsSource = PdfDialogOptions.LayoutModes.ToList(),
            SelectedIndex = PageLayoutCombo.ToComboIndex(_layoutMode),
        };
        _layoutBox.SelectionChanged += async (_, _) =>
        {
            if (_layoutBox.SelectedIndex == PageLayoutCombo.ContactSheetIndex)
            {
                await EnterContactSheetAsync();
                return;
            }

            if (_contactSheetMode)
            {
                _contactSheetMode = false;
            }

            await SetLayoutModeAsync(SelectedLayout());
        };

        var first = new Button { Content = PdfViewerChromeLabels.First };
        var prev = new Button { Content = PdfViewerChromeLabels.Prev };
        var next = new Button { Content = PdfViewerChromeLabels.Next };
        var last = new Button { Content = PdfViewerChromeLabels.Last };
        var back = new Button { Content = PdfViewerChromeLabels.Back };
        var forward = new Button { Content = PdfViewerChromeLabels.Fwd };
        var zoomOut = new Button { Content = PdfViewerChromeLabels.Minus, Width = 36 };
        var zoomIn = new Button { Content = PdfViewerChromeLabels.Plus, Width = 36 };
        var fitWidth = new Button { Content = PdfViewerChromeLabels.FitWidth };
        var fitPage = new Button { Content = PdfViewerChromeLabels.FitPage };
        var actual = new Button { Content = PdfViewerChromeLabels.Zoom100 };
        _zoomAreaButton = new Button { Content = PdfViewerChromeLabels.ZoomArea };
        ToolTipService.SetToolTip(_zoomAreaButton, PdfViewerTooltips.RectangularZoomToAreaDragOn);
        AutomationProperties.SetName(_zoomAreaButton, PdfViewerAutomationNames.ZoomToArea);
        _viewLoupeButton = new Button { Content = PdfViewerChromeLabels.LoupeGlass };
        ToolTipService.SetToolTip(_viewLoupeButton, PdfViewerTooltips.MagnifierLoupeMoveOverThePage);
        AutomationProperties.SetName(_viewLoupeButton, PdfViewerAutomationNames.MagnifierLoupe);
        _presentButton = new Button { Content = PdfViewerChromeLabels.Present };
        ToolTipService.SetToolTip(_presentButton, PdfViewerTooltips.PresentationModeFullscreenHideChromeSingle);
        AutomationProperties.SetName(_presentButton, PdfViewerAutomationNames.PresentationMode);
        var copy = new Button { Content = PdfViewerChromeLabels.Copy };
        ToolTipService.SetToolTip(copy, PdfViewerTooltips.CopySelectedTextOrTheCurrent);
        var rotateLeft = new Button { Content = PdfViewerChromeLabels.RotateCcw };
        var rotateRight = new Button { Content = PdfViewerChromeLabels.RotateCw };
        var deletePages = new Button { Content = PdfViewerChromeLabels.Delete };
        var moveUp = new Button { Content = PdfViewerChromeLabels.MoveUp };
        var moveDown = new Button { Content = PdfViewerChromeLabels.MoveDown };
        var insertBlank = new Button { Content = PdfViewerChromeLabels.Blank };
        var duplicate = new Button { Content = PdfViewerChromeLabels.Duplicate };
        var extract = new Button { Content = PdfViewerChromeLabels.Extract };
        var merge = new Button { Content = PdfViewerChromeLabels.Merge };
        var split = new Button { Content = PdfViewerChromeLabels.Split };
        var crop = new Button { Content = PdfViewerChromeLabels.Crop };
        var highlight = new Button { Content = PdfViewerChromeLabels.Highlight };
        var underline = new Button { Content = PdfViewerChromeLabels.Underline };
        var strikeout = new Button { Content = PdfViewerChromeLabels.Strike };
        var stickyNote = new Button { Content = PdfViewerChromeLabels.Note };
        var textBox = new Button { Content = PdfViewerChromeLabels.TextBox };
        var callout = new Button { Content = PdfViewerChromeLabels.Callout };
        var flatten = new Button { Content = PdfViewerChromeLabels.Flatten };
        var redact = new Button { Content = PdfViewerChromeLabels.Redact };
        var info = new Button { Content = PdfViewerChromeLabels.Info };
        var optimizeButton = new Button { Content = PdfViewerChromeLabels.Optimize };
        var protectButton = new Button { Content = PdfViewerChromeLabels.Protect };
        var export = new Button { Content = PdfViewerChromeLabels.Export };
        var print = new Button { Content = PdfViewerChromeLabels.Print };
        var camera = new Button { Content = WebcamCaptureUi.CaptureButton };
        var sign = new Button { Content = PdfViewerChromeLabels.Sign };
        var share = new Button { Content = PdfViewerChromeLabels.Share };
        var sidebarToggle = new Button { Content = PdfViewerChromeLabels.Sidebar };
        var formFill = new Button { Content = PdfViewerChromeLabels.Form };
        var ink = new Button { Content = PdfViewerChromeLabels.Ink };
        var freeform = new Button { Content = PdfViewerChromeLabels.Freeform };
        var polygon = new Button { Content = PdfViewerChromeLabels.Polygon };
        var eraser = new Button { Content = PdfViewerChromeLabels.Eraser };
        var rect = new Button { Content = PdfViewerChromeLabels.Rect };
        var roundRect = new Button { Content = PdfViewerChromeLabels.Round };
        var hiRect = new Button { Content = PdfViewerChromeLabels.Area };
        var ellipse = new Button { Content = PdfViewerChromeLabels.Ellipse };
        var line = new Button { Content = PdfViewerChromeLabels.Line };
        var arrow = new Button { Content = PdfViewerChromeLabels.Arrow };
        var star = new Button { Content = PdfViewerChromeLabels.Star };
        var bubble = new Button { Content = PdfViewerChromeLabels.Bubble };
        var loupe = new Button { Content = PdfViewerChromeLabels.Loupe };
        var fullscreen = new Button { Content = PdfViewerChromeLabels.Fullscreen };
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
        _bubbleButton = bubble;
        _loupeButton = loupe;
        _calloutButton = callout;
        _redactButton = redact;
        var undoEdit = new Button { Content = PdfViewerChromeLabels.Undo };
        var redoEdit = new Button { Content = PdfViewerChromeLabels.Redo };
        ToolTipService.SetToolTip(rotateLeft, PdfViewerTooltips.RotateSelectedPagesLeft);
        ToolTipService.SetToolTip(rotateRight, PdfViewerTooltips.RotateSelectedPagesRight);
        ToolTipService.SetToolTip(deletePages, PdfViewerTooltips.DeleteSelectedPages);
        ToolTipService.SetToolTip(moveUp, PdfViewerTooltips.MoveSelectedPagesEarlier);
        ToolTipService.SetToolTip(moveDown, PdfViewerTooltips.MoveSelectedPagesLater);
        ToolTipService.SetToolTip(insertBlank, PdfViewerTooltips.InsertBlankPageAfterSelection);
        ToolTipService.SetToolTip(duplicate, PdfViewerTooltips.DuplicateSelectedPages);
        ToolTipService.SetToolTip(extract, PdfViewerTooltips.ExtractSelectedPagesToANew);
        ToolTipService.SetToolTip(merge, PdfViewerTooltips.MergeOtherPdfFilesIntoThis);
        ToolTipService.SetToolTip(split, PdfViewerTooltips.SplitDocumentBeforeEachSelectedPage);
        ToolTipService.SetToolTip(crop, PdfViewerTooltips.InteractiveCropboxCropVisualHandlesNumeric);
        ToolTipService.SetToolTip(highlight, PdfViewerTooltips.HighlightSelectedTextOrTogglePersistent);
        ToolTipService.SetToolTip(underline, PdfViewerTooltips.UnderlineSelectedText);
        ToolTipService.SetToolTip(strikeout, PdfViewerTooltips.StrikeThroughSelectedText);
        ToolTipService.SetToolTip(stickyNote, PdfViewerTooltips.AddAStickyNoteOnThe);
        ToolTipService.SetToolTip(textBox, PdfViewerTooltips.AddAFreetextTextBoxOn);
        ToolTipService.SetToolTip(callout, PdfViewerTooltips.DrawACalloutDragFromTip);
        ToolTipService.SetToolTip(flatten, PdfViewerTooltips.FlattenAnnotationsIntoPageContentPermanent);
        ToolTipService.SetToolTip(redact, PdfRedactionUiCopy.ToolbarTooltip);
        ToolTipService.SetToolTip(info, PdfViewerTooltips.DocumentMetadataEncryptionAndPermissions);
        ToolTipService.SetToolTip(optimizeButton, PdfViewerTooltips.DownsampleImagesShrinkPdfPresets);
        ToolTipService.SetToolTip(protectButton, PdfSecurityWriteUiCopy.ToolbarTooltip);
        ToolTipService.SetToolTip(export, PdfViewerTooltips.ExportSelectedCurrentPageSAs);
        ToolTipService.SetToolTip(print, PdfViewerTooltips.PrintCurrentSelectedRangeOrAll);
        ToolTipService.SetToolTip(camera, PdfViewerTooltips.CaptureFromWebcamAndInsertOnto);
        ToolTipService.SetToolTip(share, PdfViewerTooltips.ShareViaWindowsShareUi);
        ToolTipService.SetToolTip(sidebarToggle, PdfViewerTooltips.ShowOrHideTheAppSidebar);
        ToolTipService.SetToolTip(sign, PdfViewerTooltips.SignatureDrawImportPngJpegOr);
        ToolTipService.SetToolTip(formFill, PdfViewerTooltips.FormFillOverlayModeOrField);
        ToolTipService.SetToolTip(ink, PdfViewerTooltips.ToggleFreehandInkDrawingOnThe);
        ToolTipService.SetToolTip(freeform, PdfViewerTooltips.DrawAClosedFreeformShapeAuto);
        ToolTipService.SetToolTip(eraser, PdfViewerTooltips.EraseAnnotationsByClickingThemInk);
        ToolTipService.SetToolTip(rect, PdfViewerTooltips.DrawARectangleAnnotation);
        ToolTipService.SetToolTip(roundRect, PdfViewerTooltips.DrawARoundedRectangleAnnotation);
        ToolTipService.SetToolTip(hiRect, PdfViewerTooltips.DrawATranslucentHighlightRectangleArea);
        ToolTipService.SetToolTip(ellipse, PdfViewerTooltips.DrawAnEllipseAnnotation);
        ToolTipService.SetToolTip(line, PdfViewerTooltips.DrawALineStoredAsA);
        ToolTipService.SetToolTip(arrow, PdfViewerTooltips.DrawAnArrowInkShaftArrowhead);
        ToolTipService.SetToolTip(star, PdfViewerTooltips.DrawA5PointStarOutline);
        ToolTipService.SetToolTip(bubble, PdfViewerTooltips.DrawASpeechBubbleOutline);
        ToolTipService.SetToolTip(loupe, PdfViewerTooltips.DrawALoupeMagnificationMarkerSelect);
        ToolTipService.SetToolTip(fullscreen, PdfViewerTooltips.ToggleWindowFullscreenF11);
        ToolTipService.SetToolTip(undoEdit, PdfViewerTooltips.UndoLastStrokeIfAnyOr);
        ToolTipService.SetToolTip(redoEdit, PdfViewerTooltips.RedoPageEditCtrlY);
        ToolTipService.SetToolTip(first, PdfViewerTooltips.GoToFirstPage);
        ToolTipService.SetToolTip(prev, PdfViewerTooltips.PreviousPage);
        ToolTipService.SetToolTip(next, PdfViewerTooltips.NextPage);
        ToolTipService.SetToolTip(last, PdfViewerTooltips.GoToLastPage);
        ToolTipService.SetToolTip(back, PdfViewerTooltips.NavigateBackInPageHistory);
        ToolTipService.SetToolTip(forward, PdfViewerTooltips.NavigateForwardInPageHistory);
        ToolTipService.SetToolTip(zoomOut, PdfViewerTooltips.ZoomOut);
        ToolTipService.SetToolTip(zoomIn, PdfViewerTooltips.ZoomIn);
        ToolTipService.SetToolTip(fitWidth, PdfViewerTooltips.FitPageWidth);
        ToolTipService.SetToolTip(fitPage, PdfViewerTooltips.FitPage);
        ToolTipService.SetToolTip(actual, PdfViewerTooltips.ZoomTo100);
        ToolTipService.SetToolTip(_layoutBox, PdfViewerTooltips.PageLayoutMode);
        ToolTipService.SetToolTip(_gotoBox, PdfViewerTooltips.GoToPageNumber);
        ApplyToolbarAccessibleNames(
            first, prev, next, last, back, forward, zoomOut, zoomIn, fitWidth, fitPage, actual, _zoomAreaButton, _viewLoupeButton, _presentButton, copy,
            rotateLeft, rotateRight, deletePages, moveUp, moveDown, insertBlank, duplicate, extract,
            merge, split, crop, highlight, underline, strikeout, stickyNote, textBox, callout, flatten,
            redact, info, optimizeButton, protectButton, export, print, share, sidebarToggle, camera, sign, formFill, ink, freeform, eraser, rect,
            roundRect, hiRect, ellipse, line, arrow, star, bubble, loupe, fullscreen, undoEdit, redoEdit,
            _layoutBox, _gotoBox,
            _caseSensitiveBox, _searchSortBox, findSelection, ocrPage, _ocrCancelButton, _copyOcrButton,
            _clearOcrOverlayButton, _ocrSavePdfButton, _ocrEntitiesButton, clearSearch, prevMatch, nextMatch,
            removeAnnot, duplicateAnnot, copyAnnot, cutAnnot, pasteAnnot, editAnnot, authorAnnot,
            expandNote, collapseNote, exportNotes, underlineAnnot, colorAnnot, fillAnnot, tipAnnot,
            groupAnnot, ungroupAnnot, opacityAnnot, widthAnnot, rotateAnnot);

        first.Click += async (_, _) => await GoToPageAsync(
            PageLayoutCalculator.FirstPageIndex(_layoutMode, _document.PageCount),
            recordHistory: true);
        last.Click += async (_, _) => await GoToPageAsync(
            PageLayoutCalculator.LastPageIndex(_layoutMode, _document.PageCount),
            recordHistory: true);
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
        _zoomAreaButton.Click += (_, _) => ToggleZoomAreaMode();
        _viewLoupeButton.Click += (_, _) => ToggleViewLoupeMode();
        _presentButton.Click += async (_, _) => await TogglePresentationModeAsync();
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
        optimizeButton.Click += async (_, _) => await ShowOptimizeDialogAsync();
        protectButton.Click += async (_, _) => await ShowProtectDialogAsync();
        export.Click += async (_, _) => await ExportPagesAsImagesAsync();
        print.Click += async (_, _) => await PrintDocumentAsync();
        camera.Click += async (_, _) => await CaptureCameraIntoDocumentAsync();
        sign.Click += async (_, _) => await BeginSignatureAsync();
        share.Click += (_, _) =>
        {
            if (App.CurrentApp.MainWindowInstance is MainWindow mw)
            {
                mw.ShareActiveDocumentFromToolbar();
            }
            else
            {
                _status.Text = DocumentShareStatus.ShareUnavailable;
            }
        };
        sidebarToggle.Click += async (_, _) =>
        {
            if (App.CurrentApp.MainWindowInstance is MainWindow mw)
            {
                await mw.ToggleSidebarFromToolbarAsync();
            }
            else
            {
                _status.Text = SidebarModeCombo.ToggleUnavailable;
            }
        };
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
        bubble.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.SpeechBubble);
        loupe.Click += async (_, _) => await ToggleShapeModeAsync(PdfShapeKind.Loupe);
        fullscreen.Click += (_, _) => ToggleFullscreen();
        undoEdit.Click += async (_, _) => await UndoMostRecentAsync();
        redoEdit.Click += async (_, _) => await RedoPageEditAsync();

        _toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = settings?.CompactToolbar == true ? 2 : 6,
            Padding = settings?.CompactToolbar == true ? new Thickness(4, 2, 4, 2) : new Thickness(8),
            Children =
            {
                sidebarToggle, first, prev, _gotoBox, next, last, back, forward,
                zoomOut, zoomIn, fitWidth, fitPage, actual, _zoomAreaButton, _viewLoupeButton, _presentButton, _layoutBox, copy,
                undoEdit, redoEdit,
                rotateLeft, rotateRight, deletePages, moveUp, moveDown, insertBlank, duplicate, extract, merge, split, crop,
                highlight, underline, strikeout, stickyNote, textBox, callout, flatten, redact, info, optimizeButton, protectButton, export, print, share, camera, sign, formFill, ink, freeform, polygon, eraser, rect, roundRect, hiRect, ellipse, line, arrow, star, bubble, loupe, fullscreen,
                _searchBox, _caseSensitiveBox, searchButton, findSelection, ocrPage, _ocrCancelButton, _copyOcrButton, _clearOcrOverlayButton, _ocrSavePdfButton, _ocrEntitiesButton, clearSearch, prevMatch, nextMatch, _jobProgress, _status,
            },
        };
        ToolbarCommandApplicator.Tag(sidebarToggle, ToolbarCommands.Sidebar);
        ToolbarCommandApplicator.Tag(prev, ToolbarCommands.Previous);
        ToolbarCommandApplicator.Tag(next, ToolbarCommands.Next);
        ToolbarCommandApplicator.Tag(_gotoBox, ToolbarCommands.PageNumber);
        ToolbarCommandApplicator.Tag(zoomOut, ToolbarCommands.Zoom);
        ToolbarCommandApplicator.Tag(zoomIn, ToolbarCommands.Zoom);
        ToolbarCommandApplicator.Tag(fitPage, ToolbarCommands.FitPage);
        ToolbarCommandApplicator.Tag(fitWidth, ToolbarCommands.FitWidth);
        ToolbarCommandApplicator.Tag(_zoomAreaButton, ToolbarCommands.Zoom);
        if (_viewLoupeButton is not null)
        {
            ToolbarCommandApplicator.Tag(_viewLoupeButton, ToolbarCommands.Zoom);
        }

        if (_presentButton is not null)
        {
            ToolbarCommandApplicator.Tag(_presentButton, ToolbarCommands.FitPage);
        }
        ToolbarCommandApplicator.Tag(_searchBox, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(_caseSensitiveBox, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(searchButton, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(findSelection, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(clearSearch, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(prevMatch, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(nextMatch, ToolbarCommands.Search);
        ToolbarCommandApplicator.Tag(ink, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(freeform, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(polygon, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(eraser, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(rect, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(roundRect, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(hiRect, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(ellipse, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(line, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(arrow, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(star, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(bubble, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(loupe, ToolbarCommands.Markup);
        ToolbarCommandApplicator.Tag(highlight, ToolbarCommands.Highlight);
        ToolbarCommandApplicator.Tag(underline, ToolbarCommands.Highlight);
        ToolbarCommandApplicator.Tag(strikeout, ToolbarCommands.Highlight);
        ToolbarCommandApplicator.Tag(rotateLeft, ToolbarCommands.Rotate);
        ToolbarCommandApplicator.Tag(rotateRight, ToolbarCommands.Rotate);
        ToolbarCommandApplicator.Tag(crop, ToolbarCommands.Crop);
        ToolbarCommandApplicator.Tag(sign, ToolbarCommands.Signature);
        ToolbarCommandApplicator.Tag(print, ToolbarCommands.Print);
        ToolbarCommandApplicator.Tag(info, ToolbarCommands.Inspector);
        ToolbarCommandApplicator.Tag(share, ToolbarCommands.Share);
        ToolbarCommandApplicator.Tag(ocrPage, ToolbarCommands.Ocr);
        ToolbarCommandApplicator.Tag(_ocrCancelButton, ToolbarCommands.Ocr);
        ToolbarCommandApplicator.Tag(_copyOcrButton, ToolbarCommands.Ocr);
        ToolbarCommandApplicator.Tag(_clearOcrOverlayButton, ToolbarCommands.Ocr);
        ToolbarCommandApplicator.Tag(_ocrSavePdfButton, ToolbarCommands.Ocr);
        ToolbarCommandApplicator.Tag(_ocrEntitiesButton, ToolbarCommands.Ocr);
        _toolbarDefaults = _toolbar.Children.Cast<UIElement>().ToList();
        ToolbarCommandApplicator.Apply(_toolbar, settings, _toolbarDefaults);
        SetToolbarVisible(settings?.ToolbarVisible != false);

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(170) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        _bodyGrid = body;
        body.Children.Add(sidePanel);
        Grid.SetColumn(_scrollViewer, 1);
        body.Children.Add(_scrollViewer);
        _viewLoupeHost = new Canvas { IsHitTestVisible = false };
        Grid.SetColumnSpan(_viewLoupeHost, 2);
        body.Children.Add(_viewLoupeHost);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        root.Children.Add(_toolbar);
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
        RefreshBookmarkList();
        _ = RefreshAnnotationSidebarAsync();
        RefreshPropertiesSidebar();
        RefreshAttachmentsSidebar();
    }

    private void PdfDocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        PdfPageDragRegistry.Unregister(_documentKey);
        ClearDropHighlight();
        _presentationTimer?.Stop();
        _presentationTimer = null;
        if (_cropMode)
        {
            CancelCropMode();
        }

        _searchCoordinator.Cancel();
        _cache.ClearDocument(_documentKey);
        _cache.ClearDocument(_thumbnailKey);
    }

    private async Task EnterContactSheetAsync()
    {
        _contactSheetMode = true;
        _status.Text = ContactSheetLayout.OpenHint;
        BuildContactSheet();
        _scrollViewer.Content = _contactSheetHost;
        SyncViewState();
        await RenderContactSheetAsync();
    }

    private void BuildContactSheet()
    {
        _contactSheetHost.Children.Clear();
        const int columns = ContactSheetLayout.DefaultColumns;
        StackPanel? row = null;
        for (var i = 0; i < _document.PageCount; i++)
        {
            if (ContactSheetLayout.StartsRow(i, columns))
            {
                row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = ContactSheetLayout.DefaultSpacing,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                _contactSheetHost.Children.Add(row);
            }

            var pageIndex = i;
            var image = new Image
            {
                Width = ContactSheetLayout.DefaultCellWidth,
                Stretch = Stretch.Uniform,
                Tag = pageIndex,
            };
            if (_thumbnailImages.TryGetValue(pageIndex, out var thumb) && thumb.Source is not null)
            {
                image.Source = thumb.Source;
            }

            var label = new TextBlock
            {
                Text = $"{pageIndex + 1}",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.8,
            };
            var stack = new StackPanel { Spacing = 4, Children = { image, label } };
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(
                    pageIndex == CurrentPageIndex ? Colors.DodgerBlue : Colors.Transparent),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(4),
                Child = stack,
                Tag = pageIndex,
            };
            border.PointerPressed += async (_, e) =>
            {
                e.Handled = true;
                _contactSheetMode = false;
                // SelectionChanged → SetLayoutModeAsync rebuilds continuous view.
                if (_layoutBox.SelectedIndex != 0)
                {
                    _layoutBox.SelectedIndex = 0;
                }
                else
                {
                    await SetLayoutModeAsync(PageLayoutMode.Continuous);
                }

                await GoToPageAsync(pageIndex, recordHistory: true);
            };
            row!.Children.Add(border);
        }
    }

    private async Task RenderContactSheetAsync()
    {
        // Prefer already-rendered sidebar thumbs; fill any missing at contact-sheet size.
        for (var i = 0; i < _document.PageCount; i++)
        {
            if (_thumbnailImages.TryGetValue(i, out var existing) && existing.Source is not null)
            {
                continue;
            }

            try
            {
                await RenderThumbnailAsync(i);
            }
            catch
            {
                // Contact sheet still usable with missing cells.
            }
        }

        // Refresh sources after thumbnail pass.
        foreach (var border in _contactSheetHost.Children
                     .OfType<StackPanel>()
                     .SelectMany(r => r.Children.OfType<Border>()))
        {
            if (border.Tag is not int pageIndex
                || border.Child is not StackPanel stack
                || stack.Children.OfType<Image>().FirstOrDefault() is not { } image)
            {
                continue;
            }

            if (_thumbnailImages.TryGetValue(pageIndex, out var thumb) && thumb.Source is not null)
            {
                image.Source = thumb.Source;
            }
        }
    }

    private PageLayoutMode SelectedLayout() => PageLayoutCombo.FromComboIndex(_layoutBox.SelectedIndex);

    private async Task SetLayoutModeAsync(PageLayoutMode mode)
    {
        _contactSheetMode = false;
        _layoutMode = mode;
        if (_layoutBox.SelectedIndex != PageLayoutCombo.ToComboIndex(mode) && _layoutBox.SelectedIndex != PageLayoutCombo.ContactSheetIndex)
        {
            _layoutBox.SelectedIndex = PageLayoutCombo.ToComboIndex(mode);
        }
        else if (_layoutBox.SelectedIndex == PageLayoutCombo.ContactSheetIndex)
        {
            // Leaving contact sheet via SetLayoutMode — sync combo to mode.
            _layoutBox.SelectedIndex = PageLayoutCombo.ToComboIndex(mode);
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
            var thumbScale = _thumbnailWidth / Math.Max(1, page.WidthPoints);
            var image = new Image
            {
                Width = _thumbnailWidth,
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
            border.RightTapped += Thumbnail_RightTapped;
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

    private void Thumbnail_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int index } target)
        {
            return;
        }

        if (!_pageSelection.Contains(index))
        {
            _pageSelection.SelectOnly(index);
            RefreshThumbnailSelectionChrome();
        }

        var count = Math.Max(1, _pageSelection.Count);
        var flyout = new MenuFlyout();

        var rotateLeft = new MenuFlyoutItem { Text = ThumbnailContextMenu.RotateLeft(count) };
        rotateLeft.Click += async (_, _) => await RotateSelectedAsync(-90);
        var rotateRight = new MenuFlyoutItem { Text = ThumbnailContextMenu.RotateRight(count) };
        rotateRight.Click += async (_, _) => await RotateSelectedAsync(90);
        var duplicate = new MenuFlyoutItem { Text = ThumbnailContextMenu.Duplicate(count) };
        duplicate.Click += async (_, _) => await DuplicateSelectedAsync();
        var extract = new MenuFlyoutItem { Text = ThumbnailContextMenu.Extract(count) };
        extract.Click += async (_, _) => await ExtractSelectedAsync();
        var copyPages = new MenuFlyoutItem { Text = ThumbnailContextMenu.CopyPages(count) };
        copyPages.Click += async (_, _) => await CopySelectedPagesAsync();
        var insertBlank = new MenuFlyoutItem { Text = ThumbnailContextMenu.InsertBlankAfter };
        insertBlank.Click += async (_, _) => await InsertBlankAfterSelectionAsync();
        var delete = new MenuFlyoutItem { Text = ThumbnailContextMenu.Delete(count) };
        delete.Click += async (_, _) => await DeleteSelectedAsync();

        flyout.Items.Add(rotateLeft);
        flyout.Items.Add(rotateRight);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(duplicate);
        flyout.Items.Add(extract);
        flyout.Items.Add(copyPages);
        flyout.Items.Add(insertBlank);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(delete);
        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void AnnotationList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (!TryGetSelectedAnnotation(out _))
        {
            _status.Text = AnnotationSelectionStatus.AnnotationContextHint;
            return;
        }

        if (sender is not FrameworkElement target)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var styleItem = new MenuFlyoutItem { Text = PageContextMenu.Style };
        styleItem.Click += async (_, _) => await SetSelectedAnnotationColorAsync();
        var alignItem = new MenuFlyoutItem { Text = PageContextMenu.Align };
        alignItem.Click += async (_, _) => await SetSelectedTextQuaddingAsync();
        var duplicateItem = new MenuFlyoutItem { Text = PageContextMenu.Duplicate };
        duplicateItem.Click += async (_, _) => await DuplicateSelectedAnnotationAsync();
        var editItem = new MenuFlyoutItem { Text = PageContextMenu.Edit };
        editItem.Click += async (_, _) => await EditSelectedAnnotationContentsAsync();
        var copyItem = new MenuFlyoutItem { Text = PageContextMenu.Copy };
        copyItem.Click += (_, _) => CopySelectedAnnotationToClipboard();
        var deleteItem = new MenuFlyoutItem { Text = PageContextMenu.Delete };
        deleteItem.Click += async (_, _) => await RemoveSelectedAnnotationAsync();
        flyout.Items.Add(styleItem);
        if (TryGetSelectedAnnotation(out var selected) && selected.IsTextBox)
        {
            flyout.Items.Add(alignItem);
        }

        flyout.Items.Add(duplicateItem);
        flyout.Items.Add(editItem);
        flyout.Items.Add(copyItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(deleteItem);
        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void BookmarkList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_bookmarkList.SelectedItem is not BookmarkListItem)
        {
            _status.Text = AnnotationSelectionStatus.BookmarkContextHint;
            return;
        }

        if (sender is not FrameworkElement target)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var goItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.GoToPage };
        goItem.Click += async (_, _) =>
        {
            if (_bookmarkList.SelectedItem is BookmarkListItem item)
            {
                await GoToPageAsync(item.PageIndex, recordHistory: true);
            }
        };
        var renameItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.RenameEllipsis };
        renameItem.Click += async (_, _) => await RenameSelectedBookmarkAsync();
        var upItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.MoveUp };
        upItem.Click += (_, _) => MoveSelectedBookmark(-1);
        var downItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.MoveDown };
        downItem.Click += (_, _) => MoveSelectedBookmark(1);
        var deleteItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.Delete };
        deleteItem.Click += (_, _) => DeleteSelectedBookmark();
        flyout.Items.Add(goItem);
        flyout.Items.Add(renameItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(upItem);
        flyout.Items.Add(downItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(deleteItem);
        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void SearchResults_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_hits.Count == 0)
        {
            _status.Text = PdfFindStatus.NoResults;
            return;
        }

        if (sender is not FrameworkElement target)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var goItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.GoToMatch };
        goItem.Click += async (_, _) =>
        {
            var index = _searchResults.SelectedIndex >= 0 ? _searchResults.SelectedIndex : _activeHitIndex;
            await GoToHitAsync(index);
        };
        var clearItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.ClearSearch };
        clearItem.Click += async (_, _) => await ClearSearchAsync();
        flyout.Items.Add(goItem);
        flyout.Items.Add(clearItem);
        flyout.ShowAt(target, e.GetPosition(target));
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
        args.Data.Properties.Title = PageDragDisplay.DragTitle(indexes.Count);

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
            var tempPath = PageExtractFileNames.TempPdfPath(
                System.IO.Path.GetTempPath(),
                Guid.NewGuid());
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
            var insertAfter = PageDropPlacement.IsInsertAfter(e.GetPosition(border).Y, border.ActualHeight);
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

        var fe = (FrameworkElement)sender;
        var insertBefore = PageDropPlacement.IsInsertAfter(e.GetPosition((UIElement)sender).Y, fe.ActualHeight)
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
        insertBefore = PageInsertIndex.Clamp(insertBefore, _document.PageCount);

        try
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                if (PageDragPayload.TryParse(text, out var payload) && payload is not null)
                {
                    if (PageDragSemantics.IsSameDocument(payload.DocumentKey, _documentKey))
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
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Drop, ex.Message);
        }
    }

    private static bool CanAcceptPageDrop(DataPackageView data) =>
        PageDropPlacement.AcceptsDrop(
            data.Contains(StandardDataFormats.Text),
            data.Contains(StandardDataFormats.StorageItems));

    private static DataPackageOperation PreferredDropOperation(DragEventArgs e)
    {
        var hasText = e.DataView.Contains(StandardDataFormats.Text);
        var hasStorage = e.DataView.Contains(StandardDataFormats.StorageItems);
        if (PageDropPlacement.PreferCopyOperation(
                e.Modifiers.HasFlag(DragDropModifiers.Control),
                hasStorage,
                hasText))
        {
            return DataPackageOperation.Copy;
        }

        return DataPackageOperation.Move | DataPackageOperation.Copy;
    }

    private static string DropCaption(DataPackageView data) =>
        PageDropPlacement.Caption(
            data.Contains(StandardDataFormats.StorageItems),
            data.Contains(StandardDataFormats.Text));

    private void ShowDropHighlight(Border border, bool insertAfter)
    {
        if (!ReferenceEquals(_dropHighlightBorder, border))
        {
            ClearDropHighlight();
            _dropHighlightBorder = border;
        }

        border.BorderBrush = new SolidColorBrush(Colors.Orange);
        var t = PageDropPlacement.HighlightThickness(insertAfter);
        border.BorderThickness = new Thickness(t.Left, t.Top, t.Right, t.Bottom);
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

        _status.Text = PageEditStatus.Reordering;
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

        _status.Text = PageEditStatus.PagesReordered;
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
        _status.Text = PageEditStatus.FormatInserting(count);
        await RunPageEditAsync(() => _pageEditor.InsertPagesAsync(_document, source, sourceIndexes, insertBefore));

        _pageSelection.Clear();
        for (var i = 0; i < count; i++)
        {
            _pageSelection.Toggle(insertBefore + i);
        }

        await ReloadAfterPageEditAsync();
        await GoToPageAsync(insertBefore, recordHistory: true);
        _status.Text = PageEditStatus.FormatInserted(count);
    }

    private async Task InsertPdfFilesAsync(IReadOnlyList<StorageFile> pdfFiles, int insertBefore)
    {
        var cursor = insertBefore;
        var totalInserted = 0;
        foreach (var file in pdfFiles)
        {
            _status.Text = PageClipboardStatus.FormatInserting(file.Name);
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
            _status.Text = PageEditStatus.NoPagesToInsert;
            return;
        }

        _pageSelection.Clear();
        for (var i = 0; i < totalInserted; i++)
        {
            _pageSelection.Toggle(insertBefore + i);
        }

        await ReloadAfterPageEditAsync();
        await GoToPageAsync(insertBefore, recordHistory: true);
        _status.Text = PageEditStatus.FormatInsertedFromFile(totalInserted, pdfFiles.Count);
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

        if (PageGotoParser.TryParseZeroBased(_gotoBox.Text, _document.PageCount) is { } pageIndex)
        {
            await GoToPageAsync(pageIndex, recordHistory: true);
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

        if (_zoomAreaMode && e.Key == VirtualKey.Escape)
        {
            ClearZoomAreaMode();
            e.Handled = true;
            return;
        }

        if (_viewLoupeMode && e.Key == VirtualKey.Escape)
        {
            ClearViewLoupeMode();
            e.Handled = true;
            return;
        }

        if (_presentationMode && e.Key == VirtualKey.Escape)
        {
            await ExitPresentationModeAsync();
            e.Handled = true;
            return;
        }

        if (PersistentHighlightMode.ExitOnEscape(_highlightMode) && e.Key == VirtualKey.Escape)
        {
            ClearHighlightMode();
            RefreshToolButtonChrome();
            _status.Text = AnnotationToolModeStatus.HighlightOff;
            e.Handled = true;
            return;
        }

        if (_redactionMode && e.Key == VirtualKey.Escape)
        {
            ClearRedactionMode();
            RefreshToolButtonChrome();
            _status.Text = PdfRedactionUiCopy.ModeOff;
            e.Handled = true;
            return;
        }

        if (_formOverlayMode && e.Key == VirtualKey.Escape)
        {
            ClearFormOverlayMode();
            RefreshToolButtonChrome();
            _status.Text = FormOverlayModePolicy.Exited;
            e.Handled = true;
            return;
        }

        if (_eraserMode && e.Key == VirtualKey.Escape)
        {
            ClearEraserMode();
            RefreshToolButtonChrome();
            _status.Text = AnnotationToolModeStatus.EraserOff;
            e.Handled = true;
            return;
        }

        if (_calloutTipEditMode && e.Key == VirtualKey.Escape)
        {
            ClearCalloutTipEditMode();
            _status.Text = AnnotationToolModeStatus.CalloutTipEditCancelled;
            e.Handled = true;
            return;
        }

        if (_polygonMode && e.Key == VirtualKey.Escape)
        {
            ClearPolygonMode();
            RefreshToolButtonChrome();
            _status.Text = AnnotationToolModeStatus.PolygonCancelled;
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
        var altDown = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shortcutOverrides = TryGetSettings()?.ShortcutOverrides;

        bool Hit(string command) =>
            WinUiKeyboardGestures.MatchesCommand(command, shortcutOverrides, e.Key, ctrlDown, shiftDown, altDown);

        if (Hit("Copy"))
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

        if (Hit("Print"))
        {
            await PrintDocumentAsync();
            e.Handled = true;
            return;
        }

        if (Hit("Cut"))
        {
            if (TryGetSelectedAnnotation(out _))
            {
                CutSelectedAnnotationToClipboard();
                e.Handled = true;
                return;
            }
        }

        if (Hit("Paste"))
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

        if (Hit("Select all pages"))
        {
            SelectAllPages();
            e.Handled = true;
            return;
        }

        if (Hit("Select all"))
        {
            // Prefers full-page text when the current page has extractable glyphs (F07-05);
            // otherwise pages (F52-13).
            await SelectAllTextOrPagesAsync();
            e.Handled = true;
            return;
        }

        if (Hit("Undo"))
        {
            await UndoMostRecentAsync();
            e.Handled = true;
            return;
        }

        if (Hit("Redo"))
        {
            await RedoPageEditAsync();
            e.Handled = true;
            return;
        }

        if (Hit("Find"))
        {
            _searchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (Hit("Zoom in"))
        {
            await SetScaleAsync(PdfZoomCalculator.ZoomIn(_scale));
            e.Handled = true;
            return;
        }

        if (Hit("Zoom out"))
        {
            await SetScaleAsync(PdfZoomCalculator.ZoomOut(_scale));
            e.Handled = true;
            return;
        }

        if (Hit("Fit / actual size"))
        {
            await FitPageAsync();
            e.Handled = true;
            return;
        }

        if (Hit("Find previous"))
        {
            await GoToHitAsync(_activeHitIndex - 1);
            e.Handled = true;
            return;
        }

        if (Hit("Find next"))
        {
            await GoToHitAsync(_activeHitIndex + 1);
            e.Handled = true;
            return;
        }

        if (Hit("Full Screen"))
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (Hit("Delete selection") || e.Key == VirtualKey.Back)
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

        if (e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right)
        {
            var delta = e.Key is VirtualKey.Up or VirtualKey.Left ? -1 : 1;
            if (_presentationMode || e.Key is VirtualKey.Left or VirtualKey.Right)
            {
                var next = Math.Clamp(CurrentPageIndex + delta, 0, Math.Max(0, _document.PageCount - 1));
                await GoToPageAsync(next, recordHistory: true);
                e.Handled = true;
                return;
            }

            var focus = _pageSelection.SelectedIndexes.DefaultIfEmpty(CurrentPageIndex).Max();
            if (shiftDown && _pageSelection.Count > 0)
            {
                focus = delta < 0
                    ? _pageSelection.SelectedIndexes.Min()
                    : _pageSelection.SelectedIndexes.Max();
            }

            var moveTo = Math.Clamp(focus + delta, 0, Math.Max(0, _document.PageCount - 1));
            _pageSelection.ApplyKeyboardMove(moveTo, extendRange: shiftDown);
            RefreshThumbnailSelectionChrome();
            await GoToPageAsync(moveTo, recordHistory: !shiftDown);
            e.Handled = true;
            return;
        }

        if (Hit("Next page"))
        {
            await GoToPageAsync(
                PageLayoutCalculator.NextPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount),
                recordHistory: true);
            e.Handled = true;
            return;
        }

        if (Hit("Previous page"))
        {
            await GoToPageAsync(
                PageLayoutCalculator.PreviousPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount),
                recordHistory: true);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Home:
                await GoToPageAsync(
                    PageLayoutCalculator.FirstPageIndex(_layoutMode, _document.PageCount),
                    recordHistory: true);
                e.Handled = true;
                break;
            case VirtualKey.End:
                await GoToPageAsync(
                    PageLayoutCalculator.LastPageIndex(_layoutMode, _document.PageCount),
                    recordHistory: true);
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
            IsExpanded = OutlineExpandPolicy.DefaultIsExpanded,
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

        if (OutlineNavigation.TryGetPageIndex(item?.PageIndex, out var page))
        {
            await GoToPageAsync(page, recordHistory: true);
        }
    }

    private async void OutlineTree_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Arrow keys expand/collapse and move focus via TreeView defaults.
        // Enter/Space navigate to the selected outline destination (F05-05).
        if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
        {
            return;
        }

        var selected = _outlineTree.SelectedNodes.FirstOrDefault()?.Content as OutlineItem;
        if (OutlineNavigation.TryGetPageIndex(selected?.PageIndex, out var page))
        {
            await GoToPageAsync(page, recordHistory: true);
            e.Handled = true;
        }
    }

    private void SelectAllPages()
    {
        _pageSelection.SelectAll(_document.PageCount);
        RefreshThumbnailSelectionChrome();
        _status.Text = PageEditStatus.FormatSelectedPages(_document.PageCount);
    }

    private async Task SelectAllTextOrPagesAsync()
    {
        // Second Ctrl+A after a full-page text selection expands to the whole document (copy buffer).
        var fullPageText = _pageChars.TryGetValue(CurrentPageIndex, out var currentChars) && currentChars.Count > 0
            ? PdfTextSelection.CopyAll(currentChars)
            : null;
        if (PdfTextSelectAllPolicy.ShouldExpandToDocument(
                _selectionPageIndex,
                CurrentPageIndex,
                _selectedText,
                fullPageText,
                _document.PageCount))
        {
            await SelectAllTextInDocumentAsync();
            return;
        }

        if (await TrySelectAllTextOnPageAsync(CurrentPageIndex))
        {
            return;
        }

        SelectAllPages();
    }

    private async Task SelectAllTextInDocumentAsync()
    {
        var parts = new List<string>();
        for (var i = 0; i < _document.PageCount; i++)
        {
            if (!_pageChars.ContainsKey(i))
            {
                _pageChars[i] = await _textExtractor.GetCharsAsync(_document, i);
            }

            var pageText = PdfTextSelection.CopyAll(_pageChars[i]);
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                parts.Add(pageText);
            }
        }

        if (parts.Count == 0)
        {
            _status.Text = PdfFindStatus.NoExtractableText;
            return;
        }

        _selectedText = PdfTextSelectAllPolicy.JoinDocumentParts(parts);
        // Keep a visual selection on the current page when it has glyphs.
        _ = await TrySelectAllTextOnPageAsync(CurrentPageIndex);
        // Restore the document-wide clipboard buffer after page select overwrote it.
        _selectedText = PdfTextSelectAllPolicy.JoinDocumentParts(parts);
        _status.Text = PdfTextSelectAllPolicy.DocumentStatus(parts.Count, _selectedText.Length);
    }

    private async Task<bool> TrySelectAllTextOnPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _document.PageCount)
        {
            return false;
        }

        if (!_pageChars.ContainsKey(pageIndex))
        {
            _pageChars[pageIndex] = await _textExtractor.GetCharsAsync(_document, pageIndex);
        }

        var chars = _pageChars[pageIndex];
        if (chars.Count == 0)
        {
            return false;
        }

        _selectedText = PdfTextSelection.CopyAll(chars);
        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            _selectedText = string.Empty;
            return false;
        }

        _selectionPageIndex = pageIndex;
        _selectionQuads = PdfTextMarkupQuads.FromChars(chars);
        _pageSelection.Clear();
        RefreshThumbnailSelectionChrome();

        if (_pageOverlays.TryGetValue(pageIndex, out var overlay))
        {
            overlay.Children.Clear();
        }

        await RefreshSearchHighlightsAsync();
        DrawSelectionOverlayFromRange(pageIndex, chars, 0, chars.Count - 1);
        _status.Text = PdfTextSelectAllPolicy.PageStatus(pageIndex, _selectedText.Length);
        return true;
    }

    private async Task CopyTextAsync()
    {
        if (string.IsNullOrEmpty(_selectedText))
        {
            _selectedText = await _textExtractor.GetTextAsync(_document, CurrentPageIndex);
        }

        if (string.IsNullOrEmpty(_selectedText))
        {
            _status.Text = PdfFindStatus.NoExtractableTextToCopy;
            return;
        }

        var package = new DataPackage();
        package.SetText(_selectedText);
        Clipboard.SetContent(package);
        _status.Text = PdfTextInteractionUi.CopiedCharacters(_selectedText.Length);
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
            _status.Text = PageDragDisplay.CopiedPagesStatus(indexes.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.CopyPages, ex.Message);
        }
    }

    /// <summary>Paste annotation clipboard if set; otherwise paste copied PDF pages.</summary>
    public async Task PasteFromClipboardAsync()
    {
        if (_annotClipboard is not null)
        {
            await PasteAnnotationClipboardAsync();
            return;
        }

        await PastePagesAsync();
    }

    private async Task PastePagesAsync()
    {
        if (!PdfPageClipboard.HasPages)
        {
            _status.Text = PageClipboardStatus.NoPagesOnClipboard;
            return;
        }

        var insertAt = PagePastePlacement.InsertAfterSelection(
            SelectedOrCurrentPages(),
            CurrentPageIndex,
            _document.PageCount);
        string? tempPath = null;
        try
        {
            var (source, path) = await PdfPageClipboard.OpenCopyAsync(_documentFactory);
            tempPath = path;
            if (source is null || source.PageCount == 0)
            {
                _status.Text = PageClipboardStatus.ClipboardPagesUnavailable;
                return;
            }

            await using (source)
            {
                var indexes = Enumerable.Range(0, source.PageCount).ToList();
                _status.Text = PageDragDisplay.PastingPagesStatus(indexes.Count);
                await RunPageEditAsync(() => _pageEditor.InsertPagesAsync(_document, source, indexes, insertAt));

                _pageSelection.Clear();
                for (var i = 0; i < indexes.Count; i++)
                {
                    _pageSelection.Toggle(insertAt + i);
                }

                await ReloadAfterPageEditAsync();
                await GoToPageAsync(insertAt, recordHistory: true);
                _status.Text = PageDragDisplay.PastedPagesStatus(indexes.Count);
            }
        }
        catch (Exception ex)
        {
            _status.Text = PageEditStatus.FormatPastePagesFailed(ex.Message);
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

        if (_zoomAreaMode)
        {
            _dragSelecting = true;
            _dragPageIndex = pageIndex;
            _dragStart = e.GetCurrentPoint(border).Position;
            border.CapturePointer(e.Pointer);
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

        if (_calloutTipEditMode && _calloutTipTarget is not null)
        {
            await FinishCalloutTipEditAsync(border, pageIndex, e);
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
            var ctrl = (e.KeyModifiers & VirtualKeyModifiers.Control) != 0;
            if (ctrl)
            {
                ToggleAnnotInSelection(hit);
                e.Handled = true;
                return;
            }

            BeginAnnotDrag(border, hit, pressPoint, e);
            e.Handled = true;
            return;
        }

        ClearAnnotSelectionVisual();
        _selectedAnnot = null;
        _selectedAnnots.Clear();
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

        var hasText = !string.IsNullOrWhiteSpace(_selectedText)
            && _selectionPageIndex >= 0
            && _selectionQuads.Count > 0;
        var hasRegion = PdfRegionCopyPolicy.HasValidRegion(
            _regionCopyPageIndex,
            _regionCopyDisplayRect.Width,
            _regionCopyDisplayRect.Height);
        var hasAnnot = TryGetSelectedAnnotation(out _);
        var pageIndex = target is Border { Tag: int taggedPage }
            ? taggedPage
            : CurrentPageIndex;

        var flyout = new MenuFlyout();
        var selectAllItem = new MenuFlyoutItem { Text = PdfTextInteractionUi.SelectAllText };
        selectAllItem.Click += async (_, _) =>
        {
            if (!await TrySelectAllTextOnPageAsync(pageIndex))
            {
                _status.Text = PdfTextInteractionUi.NoExtractableText;
            }
        };
        flyout.Items.Add(selectAllItem);
        if (_document.PageCount > 1)
        {
            var selectDocItem = new MenuFlyoutItem { Text = PdfTextInteractionUi.SelectAllTextInDocument };
            selectDocItem.Click += async (_, _) => await SelectAllTextInDocumentAsync();
            flyout.Items.Add(selectDocItem);
        }

        if (!hasText && !hasRegion && !hasAnnot)
        {
            flyout.ShowAt(target, e.GetPosition(target));
            e.Handled = true;
            return;
        }

        if (hasText)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            var copyItem = new MenuFlyoutItem { Text = PdfTextInteractionUi.Copy };
            copyItem.Click += async (_, _) => await CopyTextAsync();
            var highlightItem = new MenuFlyoutItem { Text = PageContextMenu.Highlight };
            highlightItem.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.Highlight);
            var underlineItem = new MenuFlyoutItem { Text = PageContextMenu.Underline };
            underlineItem.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.Underline);
            var strikeItem = new MenuFlyoutItem { Text = PageContextMenu.Strikethrough };
            strikeItem.Click += async (_, _) => await ApplyTextMarkupAsync(PdfTextMarkupKind.StrikeOut);
            var findItem = new MenuFlyoutItem { Text = PdfTextInteractionUi.FindSelection };
            findItem.Click += async (_, _) => await SearchSelectedTextAsync();
            var webItem = new MenuFlyoutItem { Text = PdfTextInteractionUi.SearchWeb };
            webItem.Click += async (_, _) => await SearchWebAsync(_selectedText);
            var redactTextItem = new MenuFlyoutItem { Text = PdfRedactionUiCopy.MarkForRedactionMenu };
            redactTextItem.Click += (_, _) => MarkSelectionForRedaction();
            flyout.Items.Add(copyItem);
            flyout.Items.Add(highlightItem);
            flyout.Items.Add(underlineItem);
            flyout.Items.Add(strikeItem);
            flyout.Items.Add(findItem);
            flyout.Items.Add(webItem);
            flyout.Items.Add(redactTextItem);
        }

        if (hasAnnot)
        {
            if (flyout.Items.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var styleItem = new MenuFlyoutItem { Text = PageContextMenu.Style };
            styleItem.Click += async (_, _) => await SetSelectedAnnotationColorAsync();
            var duplicateItem = new MenuFlyoutItem { Text = PageContextMenu.Duplicate };
            duplicateItem.Click += async (_, _) => await DuplicateSelectedAnnotationAsync();
            var deleteItem = new MenuFlyoutItem { Text = PageContextMenu.Delete };
            deleteItem.Click += async (_, _) => await RemoveSelectedAnnotationAsync();
            var copyAnnotItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.CopyAnnotation };
            copyAnnotItem.Click += (_, _) => CopySelectedAnnotationToClipboard();
            flyout.Items.Add(styleItem);
            flyout.Items.Add(duplicateItem);
            flyout.Items.Add(deleteItem);
            flyout.Items.Add(copyAnnotItem);
        }

        if (hasRegion)
        {
            if (flyout.Items.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var imageItem = new MenuFlyoutItem { Text = PdfTextInteractionUi.CopyRegionAsImage };
            imageItem.Click += async (_, _) => await CopyRegionAsBitmapAsync();
            var redactRegionItem = new MenuFlyoutItem { Text = PdfRedactionUiCopy.MarkRegionForRedactionMenu };
            redactRegionItem.Click += (_, _) => MarkRegionForRedaction();
            flyout.Items.Add(imageItem);
            flyout.Items.Add(redactRegionItem);
        }

        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void PageBorder_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (!PdfTextDragPolicy.CanStartTextDrag(_selectedText))
        {
            args.Cancel = true;
            return;
        }

        args.Data.SetText(_selectedText);
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        _status.Text = PdfTextDragPolicy.DraggingStatus;
    }

    private void PageBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_viewLoupeMode && sender is Border { Tag: int loupePage } loupeBorder)
        {
            UpdateViewLoupe(loupeBorder, loupePage, e.GetCurrentPoint(loupeBorder).Position);
        }

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
        var zoomAreaActive = _zoomAreaMode;
        _dragSelecting = false;
        border.ReleasePointerCapture(e.Pointer);

        var dragDistance = Math.Abs(point.Position.X - dragStart.X) + Math.Abs(point.Position.Y - dragStart.Y);
        if (zoomAreaActive)
        {
            if (wasDragging && dragDistance >= 8)
            {
                var leftUi = Math.Min(dragStart.X, point.Position.X);
                var topUi = Math.Min(dragStart.Y, point.Position.Y);
                var widthUi = Math.Max(1, Math.Abs(point.Position.X - dragStart.X));
                var heightUi = Math.Max(1, Math.Abs(point.Position.Y - dragStart.Y));
                await ZoomToDisplayAreaAsync(pageIndex, leftUi, topUi, widthUi, heightUi);
            }
            else
            {
                ClearZoomAreaMode();
                _status.Text = PdfZoomAreaStatus.CancelledTooSmall;
            }

            if (_pageOverlays.TryGetValue(pageIndex, out var zoomOverlay))
            {
                zoomOverlay.Children.Clear();
            }

            e.Handled = true;
            return;
        }

        var pdfX = point.Position.X / _scale;
        var pdfY = page.HeightPoints - (point.Position.Y / _scale);

        if (!_pageLinks.ContainsKey(pageIndex))
        {
            _pageLinks[pageIndex] = await _linkService.GetPageLinksAsync(_document, pageIndex);
        }

        if (!wasDragging || dragDistance < 4)
        {
            var link = _pageLinks[pageIndex].FirstOrDefault(l => l.Bounds.ContainsPoint(pdfX, pdfY));
            if (link is not null
                && PdfLinkAction.Resolve(link.DestinationPageIndex, link.Uri) == PdfLinkAction.Kind.GoToPage
                && link.DestinationPageIndex is int dest)
            {
                await GoToPageAsync(dest, recordHistory: true);
                _status.Text = PdfFindStatus.FormatFollowedLink(dest + 1);
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

            var selection = PdfPageCoordinates.FromDisplayRect(
                leftUi,
                topUi,
                rightUi,
                bottomUi,
                page.HeightPoints,
                _scale);

            var startPdf = PdfPageCoordinates.FromDisplayPoint(
                dragStart.X,
                dragStart.Y,
                page.HeightPoints,
                _scale);
            var endPdf = PdfPageCoordinates.FromDisplayPoint(
                point.Position.X,
                point.Position.Y,
                page.HeightPoints,
                _scale);
            var startPdfX = startPdf.X;
            var startPdfY = startPdf.Y;
            var endPdfX = endPdf.X;
            var endPdfY = endPdf.Y;
            var rectW = selection.Width;
            var rectH = selection.Height;
            // Alt or a wide short-tall drag prefers column/region geometry; otherwise stream across lines.
            var columnMode = PdfTextSelection.PreferColumnMode(
                e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu),
                rectW,
                rectH);

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
                ? AnnotationMutationStatus.RegionSelectedCopyHint
                : AnnotationMutationStatus.FormatSelectedText(TrimForStatus(_selectedText));
            return;
        }

        // Click selects nearest character word-ish: expand to nearby chars on the same line.
        if (!PdfTextSelection.TryExpandWordAt(chars, pdfX, pdfY, out var start, out var end))
        {
            return;
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
            ? AnnotationMutationStatus.FormatPageIndex(pageIndex + 1)
            : AnnotationMutationStatus.FormatSelectedText(TrimForStatus(_selectedText));

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
                    AddHighlightRect(overlay, page.HeightPoints, chars[i].Bounds,
                        Windows.UI.Color.FromArgb(
                            PdfSearchHighlightStyle.Alpha,
                            PdfSearchHighlightStyle.Red,
                            PdfSearchHighlightStyle.Green,
                            PdfSearchHighlightStyle.Blue));
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

    private static string TrimForStatus(string text) => PdfAnnotationListLabel.TrimPreview(text);

    private sealed record OutlineItem(string Title, int? PageIndex)
    {
        public override string ToString() => Title;
    }

    private async void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!WheelInputPolicy.PreferZoomOverScroll(e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)))
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

        await SetScaleAsync(PdfZoomCalculator.ApplyManipulationScale(_scale, e.Delta.Scale));
        e.Handled = true;
    }

    private void CancelOcr()
    {
        if (_ocrCts is null)
        {
            return;
        }

        _ocrCts.Cancel();
        _status.Text = OcrResultDialog.Cancelling;
    }

    private void BeginOcrJob()
    {
        _ocrCts?.Cancel();
        _ocrCts?.Dispose();
        _ocrCts = new CancellationTokenSource();
        _ocrCancelButton.Visibility = Visibility.Visible;
        ShowJobProgress(0, determinate: true);
    }

    private void EndOcrJob()
    {
        _ocrCancelButton.Visibility = Visibility.Collapsed;
        _ocrCts?.Dispose();
        _ocrCts = null;
        HideJobProgress();
    }

    private void ShowJobProgress(double percent, bool determinate = true)
    {
        _jobProgress.IsIndeterminate = !determinate;
        if (determinate)
        {
            _jobProgress.Value = Math.Clamp(percent, 0, 100);
        }

        _jobProgress.Visibility = Visibility.Visible;
    }

    private void HideJobProgress()
    {
        _jobProgress.IsIndeterminate = false;
        _jobProgress.Value = 0;
        _jobProgress.Visibility = Visibility.Collapsed;
    }

    private async Task OnOcrButtonClickAsync()
    {
        if (_ocr is null)
        {
            _status.Text = OcrResultDialog.EngineUnavailable;
            return;
        }

        var selected = SelectedOrCurrentPages();
        var chooser = new ContentDialog
        {
            Title = PdfDialogTitles.Ocr,
            Content = new TextBlock
            {
                Text =
                    $"Recognize text offline.\n\n• {OcrPageRangeChooser.SelectedLabel(selected)}\n• {OcrPageRangeChooser.EntireDocumentLabel(_document.PageCount)}",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = OcrPageRangeChooser.PrimaryButton(selected.Count),
            SecondaryButtonText = OcrPageRangeChooser.SecondaryButton,
            CloseButtonText = DialogButtons.Cancel,
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
            await RunOcrPagesAsync(OcrPageRangeChooser.EntireDocumentPages(_document.PageCount));
        }
    }

    private async Task RunOcrPagesAsync(IReadOnlyList<int> pages)
    {
        if (_ocr is null)
        {
            _status.Text = OcrResultDialog.EngineUnavailable;
            return;
        }

        if (pages.Count == 0)
        {
            _status.Text = OcrResultDialog.NoPagesSelected;
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
                ShowJobProgress(pages.Count <= 1 ? 5 : (100.0 * i / pages.Count));
                _status.Text = OcrPageRangeChooser.ProgressStatus(pageIndex, i + 1, pages.Count);

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
                    new OcrRequest(
                        rendered.Width,
                        rendered.Height,
                        pixels,
                        LanguageTag: TryGetSettings()?.OcrLanguageTag),
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
                    : OcrResultDialog.PageSectionHeader(pageIndex, body));
                ShowJobProgress(100.0 * (i + 1) / pages.Count);
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
            var copy = new Button { Content = OcrResultDialog.CopyButton, Margin = new Thickness(0, 8, 0, 0) };
            copy.Click += (_, _) =>
            {
                var package = new DataPackage();
                package.SetText(combined);
                Clipboard.SetContent(package);
                _status.Text = OcrResultDialog.TextCopied;
            };

            var summary = OcrResultDialog.Summary(pages.Count, pages[0], totalLines, totalWords);

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
                Title = OcrResultDialog.Title(pages.Count),
                Content = panel,
                CloseButtonText = DialogButtons.Close,
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
            _status.Text = OcrResultDialog.CompletionStatus(pages.Count, pages[0], totalLines);
        }
        catch (OperationCanceledException)
        {
            _status.Text = OcrResultDialog.Cancelled;
        }
        catch (Exception ex)
        {
            _status.Text = OcrResultDialog.Failed(ex.Message);
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
        if (OcrWordSelectionPolicy.ShouldClearBeforeToggle(ctrl))
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
                ? OcrWordSelectionPolicy.Cleared
                : OcrWordSelectionPolicy.SelectedWord(visuals[wordIndex].Word.Text);
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
            _status.Text = OcrResultDialog.SearchableExportNeedsOcr;
            return;
        }

        try
        {
            _status.Text = OcrResultDialog.BuildingSearchablePdf;
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
                ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = DocumentExportFormats.SuggestedOcrSearchable;
            picker.FileTypeChoices.Add("PDF", [".pdf"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = OcrResultDialog.SearchablePdfCancelled;
                return;
            }

            await FileIO.WriteBytesAsync(file, pdfBytes);
            _status.Text = OcrResultDialog.SavedSearchablePdf(pages.Count, file.Name);
        }
        catch (Exception ex)
        {
            _status.Text = OcrResultDialog.SearchablePdfFailed(ex.Message);
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
            _status.Text = OcrWordSelectionPolicy.NothingToCopy;
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        _status.Text = OcrWordSelectionPolicy.CopiedWords(_selectedOcrIndices.Count);
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
        _status.Text = OcrResultDialog.OverlaysCleared;
    }

    private async Task ShowOcrEntitiesAsync()
    {
        var text = string.Join("\n", _ocrPageTexts.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        var entities = OcrEntityDetector.Detect(text);
        if (entities.Count == 0)
        {
            _status.Text = PdfFindStatus.NoEntitiesDetected;
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 440,
            MaxHeight = 320,
            ItemsSource = entities.Select(e => $"{e.Kind}: {e.Value}").ToList(),
        };
        var copy = new Button { Content = PdfViewerChromeLabels.CopyValue, Margin = new Thickness(0, 8, 8, 0) };
        var open = new Button { Content = PdfViewerChromeLabels.OpenOrAct, Margin = new Thickness(0, 8, 8, 0) };
        var searchWeb = new Button { Content = PdfViewerChromeLabels.SearchWeb, Margin = new Thickness(0, 8, 0, 0) };
        copy.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= entities.Count)
            {
                return;
            }

            var package = new DataPackage();
            package.SetText(entities[list.SelectedIndex].Value);
            Clipboard.SetContent(package);
            _status.Text = PdfFindStatus.FormatCopiedEntity(entities[list.SelectedIndex].Kind.ToString());
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
            Title = PdfDialogTitles.OcrEntities,
            Content = panel,
            CloseButtonText = DialogButtons.Close,
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
                        await Launcher.LaunchUriAsync(new Uri(OcrEntityActionUris.NormalizeUrl(entity.Value)));
                        _status.Text = OcrEntityActionUris.OpenedUrl;
                        break;
                    }
                case OcrEntityKind.Email:
                    await Launcher.LaunchUriAsync(new Uri(OcrEntityActionUris.Mailto(entity.Value)));
                    _status.Text = OcrEntityActionUris.OpenedMail;
                    break;
                case OcrEntityKind.Address:
                    {
                        await Launcher.LaunchUriAsync(new Uri(OcrEntityActionUris.BingMaps(entity.Value)));
                        _status.Text = OcrEntityActionUris.OpenedMaps;
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
                        _status.Text = OcrEntityActionUris.CopiedKind(entity.Kind.ToString());
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            _status.Text = OcrEntityActionUris.ActionFailed(ex.Message);
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
            _status.Text = PdfFindStatus.FormatCopiedEntityUnparsed(entity.Kind.ToString());
            return;
        }

        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "glyph-ocr-" + Guid.NewGuid().ToString("N") + ".ics");
        await System.IO.File.WriteAllTextAsync(path, ics);
        var file = await StorageFile.GetFileFromPathAsync(path);
        await Launcher.LaunchFileAsync(file);
        _status.Text = PdfFindStatus.OpenedCalendarInvite;
    }

    private async Task SearchWebAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _status.Text = OcrEntityActionUris.NothingToSearch;
            return;
        }

        try
        {
            await Launcher.LaunchUriAsync(new Uri(OcrEntityActionUris.BingWebSearch(query)));
            _status.Text = OcrEntityActionUris.OpenedWebSearch;
        }
        catch (Exception ex)
        {
            _status.Text = OcrEntityActionUris.SearchFailed(ex.Message);
        }
    }

    private async Task CopyRegionAsBitmapAsync()
    {
        if (!PdfRegionCopyPolicy.HasValidRegion(
                _regionCopyPageIndex,
                _regionCopyDisplayRect.Width,
                _regionCopyDisplayRect.Height))
        {
            _status.Text = PdfRegionCopyPolicy.DragRegionFirst;
            return;
        }

        try
        {
            _status.Text = PdfRegionCopyPolicy.Copying;
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
                System.Buffer.BlockCopy(full, srcOffset, cropped, dstOffset, srcW * 4);
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
            _status.Text = PdfRegionCopyPolicy.FormatCopied(srcW, srcH);
        }
        catch (Exception ex)
        {
            _status.Text = PdfRegionCopyPolicy.FormatFailed(ex.Message);
        }
    }

    private async Task SearchSelectedTextAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            _status.Text = AnnotationSelectionStatus.SelectTextFirst;
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
            ClearSearchResults(PdfTextInteractionUi.PathUnavailableForSearch);
            return;
        }

        var query = _searchBox.Text ?? string.Empty;
        _status.Text = PdfFindStatus.Searching;
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

        ShowSidebarMode(SidebarModeCombo.SearchIndex); // Search results panel

        var ocrHits = PdfPageTextSearch.Find(_ocrPageTexts, query, _searchCaseSensitive);
        var merged = PdfPageTextSearch.Merge(result.Hits, ocrHits);
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
            if (_ocr is not null
                && !string.IsNullOrWhiteSpace(query)
                && FindOcrFallbackPolicy.ShouldOfferOcr(message))
            {
                var offer = new ContentDialog
                {
                    Title = FindOcrFallbackPolicy.DialogTitle,
                    Content = FindOcrFallbackPolicy.DialogMessage,
                    PrimaryButtonText = FindOcrFallbackPolicy.PrimaryButton,
                    CloseButtonText = DialogButtons.NotNow,
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
            message = PdfTextInteractionUi.NoMatchesInTextOrOcr;
        }

        _hits = ApplySearchHitSort(merged, _searchQuery, _searchCaseSensitive);
        _activeHitIndex = _hits.Count > 0 ? 0 : -1;
        if (status is PdfSearchStatus.EmptyQuery or PdfSearchStatus.NoMatches or PdfSearchStatus.NoExtractableText)
        {
            _searchQuery = string.Empty;
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Clear();
            }
        }

        BindSearchResultsList(ocrHits);
        _status.Text = PdfSearchStatusText.Format(status, _hits.Count, message);

        if (_activeHitIndex >= 0)
        {
            _searchResults.SelectedIndex = _activeHitIndex;
            await GoToPageAsync(_hits[_activeHitIndex].PageIndex, recordHistory: true);
        }

        await RefreshSearchHighlightsAsync();
    }

    private PdfSearchHitOrder.SortMode CurrentSearchSortMode =>
        _searchSortBox.SelectedIndex == 1
            ? PdfSearchHitOrder.SortMode.Relevance
            : PdfSearchHitOrder.SortMode.PageOrder;

    private IReadOnlyList<PdfSearchHit> ApplySearchHitSort(
        IEnumerable<PdfSearchHit> hits,
        string query,
        bool caseSensitive) =>
        PdfSearchHitOrder.Apply(
            CurrentSearchSortMode,
            hits,
            query,
            h => h.Snippet,
            h => h.MatchStart,
            h => h.MatchLength,
            h => h.PageIndex,
            caseSensitive: caseSensitive);

    private void BindSearchResultsList(IReadOnlyList<PdfSearchHit> ocrHits)
    {
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
    }

    private async Task ResortSearchHitsAsync()
    {
        if (_hits.Count == 0 || string.IsNullOrWhiteSpace(_searchQuery))
        {
            return;
        }

        var previous = _activeHitIndex >= 0 && _activeHitIndex < _hits.Count
            ? _hits[_activeHitIndex]
            : null;
        _hits = ApplySearchHitSort(_hits, _searchQuery, _searchCaseSensitive);
        if (previous is not null)
        {
            var idx = _hits.ToList().FindIndex(h =>
                h.PageIndex == previous.PageIndex
                && h.MatchStart == previous.MatchStart
                && h.MatchLength == previous.MatchLength);
            _activeHitIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            _activeHitIndex = _hits.Count > 0 ? 0 : -1;
        }

        // OCR tags unknown on re-sort — show page + snippet only.
        BindSearchResultsList([]);
        if (_activeHitIndex >= 0)
        {
            _searchResults.SelectedIndex = _activeHitIndex;
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
        ClearSearchResults(PdfSearchHighlightStyle.ClearedStatus);
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

        var wrapped = PdfSearchHitNav.WrapIndex(hitIndex, _hits.Count);
        if (wrapped < 0)
        {
            return;
        }

        _activeHitIndex = wrapped;
        _searchResults.SelectedIndex = wrapped;
        await GoToPageAsync(_hits[wrapped].PageIndex, recordHistory: true);
        _status.Text = PdfSearchHitNav.FormatStatus(wrapped, _hits.Count, _hits[wrapped].PageIndex);
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

    private void ToggleZoomAreaMode()
    {
        if (_zoomAreaMode)
        {
            ClearZoomAreaMode();
            return;
        }

        _zoomAreaMode = true;
        if (_zoomAreaButton is not null)
        {
            _zoomAreaButton.Background = new SolidColorBrush(Colors.DodgerBlue);
        }

        _status.Text = PdfZoomAreaStatus.Prompt;
    }

    private void ClearZoomAreaMode()
    {
        _zoomAreaMode = false;
        if (_zoomAreaButton is not null)
        {
            _zoomAreaButton.Background = null;
        }
    }

    private void ToggleViewLoupeMode()
    {
        if (_viewLoupeMode)
        {
            ClearViewLoupeMode();
            return;
        }

        if (_presentationMode)
        {
            _ = ExitPresentationModeAsync();
        }

        ClearZoomAreaMode();
        _viewLoupeMode = true;
        if (_viewLoupeButton is not null)
        {
            _viewLoupeButton.Background = new SolidColorBrush(Colors.DodgerBlue);
        }

        EnsureViewLoupePopup();
        _status.Text = AnnotationToolModeStatus.MagnifierOn;
    }

    private void ClearViewLoupeMode()
    {
        _viewLoupeMode = false;
        if (_viewLoupeButton is not null)
        {
            _viewLoupeButton.Background = null;
        }

        if (_viewLoupePopup is not null)
        {
            _viewLoupePopup.Visibility = Visibility.Collapsed;
        }

        _status.Text = AnnotationToolModeStatus.MagnifierOff;
    }

    private void EnsureViewLoupePopup()
    {
        if (_viewLoupeHost is null)
        {
            return;
        }

        if (_viewLoupePopup is not null)
        {
            return;
        }

        var outSize = (int)PdfLoupeMagnifier.PopupSizeDip;
        _viewLoupePopup = new Border
        {
            Width = outSize + 4,
            Height = outSize + 4,
            CornerRadius = new CornerRadius((outSize + 4) / 2.0),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 100, 180)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Colors.White),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = new Image
            {
                Width = outSize,
                Height = outSize,
                Stretch = Stretch.UniformToFill,
            },
        };
        _viewLoupeHost.Children.Add(_viewLoupePopup);
    }

    private void UpdateViewLoupe(Border pageBorder, int pageIndex, Windows.Foundation.Point pagePoint)
    {
        EnsureViewLoupePopup();
        if (_viewLoupePopup is null || _viewLoupeHost is null || _bodyGrid is null)
        {
            return;
        }

        if (!_pageImages.TryGetValue(pageIndex, out var pageImage)
            || pageImage.Source is not WriteableBitmap bitmap)
        {
            _viewLoupePopup.Visibility = Visibility.Collapsed;
            return;
        }

        var page = _document.GetPage(pageIndex);
        var halfPts = 36 / Math.Max(_scale, 0.25); // ~36 DIP sample radius in page space
        var pdfX = pagePoint.X / _scale;
        var pdfYFromBottom = page.HeightPoints - (pagePoint.Y / _scale);
        var magnified = PdfLoupeMagnifier.CropAndScale(
            bitmap,
            page.WidthPoints,
            page.HeightPoints,
            pdfX - halfPts,
            pdfYFromBottom - halfPts,
            pdfX + halfPts,
            pdfYFromBottom + halfPts,
            PdfLoupeMagnifier.DefaultZoom,
            (int)PdfLoupeMagnifier.PopupSizeDip);
        if (magnified is null)
        {
            _viewLoupePopup.Visibility = Visibility.Collapsed;
            return;
        }

        if (_viewLoupePopup.Child is Image img)
        {
            img.Source = magnified;
        }

        // Position popup near the pointer in the body overlay.
        var inBody = pageBorder.TransformToVisual(_bodyGrid).TransformPoint(pagePoint);
        var size = PdfLoupeMagnifier.PopupSizeDip + 4;
        var left = inBody.X + 24;
        var top = inBody.Y - size / 2;
        if (left + size > _bodyGrid.ActualWidth)
        {
            left = inBody.X - size - 24;
        }

        left = Math.Max(0, left);
        top = Math.Clamp(top, 0, Math.Max(0, _bodyGrid.ActualHeight - size));
        Canvas.SetLeft(_viewLoupePopup, left);
        Canvas.SetTop(_viewLoupePopup, top);
        _viewLoupePopup.Visibility = Visibility.Visible;
    }

    private async Task TogglePresentationModeAsync()
    {
        if (_presentationMode)
        {
            await ExitPresentationModeAsync();
            return;
        }

        ClearViewLoupeMode();
        ClearZoomAreaMode();
        _presentationMode = true;
        _layoutBeforePresentation = _layoutMode;
        _scaleBeforePresentation = _scale;
        _toolbarWasVisible = IsToolbarVisible;
        if (_toolbar is not null && _toolbar.Visibility == Visibility.Visible)
        {
            _toolbar.Visibility = Visibility.Collapsed;
        }

        if (_sidePanel is not null)
        {
            _sidePanel.Visibility = Visibility.Collapsed;
        }

        if (_bodyGrid is not null && _bodyGrid.ColumnDefinitions.Count > 0)
        {
            _bodyGrid.ColumnDefinitions[0].Width = new GridLength(0);
        }

        await SetLayoutModeAsync(PageLayoutMode.SinglePage);
        await FitPageAsync();
        if (App.CurrentApp.MainWindowInstance is MainWindow mw
            && mw.AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
        {
            mw.ToggleFullscreen();
        }

        if (_presentButton is not null)
        {
            _presentButton.Background = new SolidColorBrush(Colors.DodgerBlue);
            _presentButton.Content = PdfViewerChromeLabels.ExitPresent;
        }

        _presentationTimer?.Stop();
        _presentationTimer = new DispatcherTimer { Interval = PresentationModeDefaults.AutoAdvanceInterval };
        _presentationTimer.Tick += async (_, _) =>
        {
            if (!_presentationMode || _document.PageCount == 0)
            {
                return;
            }

            var next = PageLayoutCalculator.NextPageIndex(_layoutMode, CurrentPageIndex, _document.PageCount);
            if (next == CurrentPageIndex)
            {
                next = 0;
            }

            await GoToPageAsync(next, recordHistory: true);
        };
        _presentationTimer.Start();
        _status.Text = PresentationModeDefaults.StatusMessage;
    }

    private async Task ExitPresentationModeAsync()
    {
        if (!_presentationMode)
        {
            return;
        }

        _presentationMode = false;
        _presentationTimer?.Stop();
        _presentationTimer = null;
        if (_presentButton is not null)
        {
            _presentButton.Background = null;
            _presentButton.Content = PdfViewerChromeLabels.Present;
        }

        if (_sidePanel is not null)
        {
            _sidePanel.Visibility = Visibility.Visible;
        }

        if (_bodyGrid is not null && _bodyGrid.ColumnDefinitions.Count > 0)
        {
            _bodyGrid.ColumnDefinitions[0].Width = new GridLength(170);
        }

        if (_toolbar is not null && _toolbarWasVisible)
        {
            _toolbar.Visibility = Visibility.Visible;
        }

        if (App.CurrentApp.MainWindowInstance is MainWindow mw
            && mw.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            mw.ToggleFullscreen();
        }

        await SetLayoutModeAsync(_layoutBeforePresentation);
        await SetScaleAsync(_scaleBeforePresentation);
        _status.Text = AnnotationToolModeStatus.ExitedPresentation;
    }

    private async Task ZoomToDisplayAreaAsync(int pageIndex, double leftUi, double topUi, double widthUi, double heightUi)
    {
        var pdfLeft = leftUi / _scale;
        var pdfTopFromTop = topUi / _scale;
        var pdfWidth = widthUi / _scale;
        var pdfHeight = heightUi / _scale;

        var newScale = PdfZoomCalculator.ZoomToArea(
            _scale,
            widthUi,
            heightUi,
            _scrollViewer.ViewportWidth,
            _scrollViewer.ViewportHeight);

        ClearZoomAreaMode();
        await SetScaleAsync(newScale);
        await GoToPageAsync(pageIndex, recordHistory: true);

        // After rebuild, scroll so the selected region is centered in the viewport.
        await Task.Yield();
        var pageEl = _continuousHost.Children.OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is int tag && tag == pageIndex)
            ?? _spreadHost.Children.OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is int tag && tag == pageIndex);
        if (pageEl is null || _scrollViewer.Content is not UIElement scrollContent)
        {
            _status.Text = PdfZoomAreaStatus.FormatZoomed(newScale);
            return;
        }

        try
        {
            var origin = pageEl.TransformToVisual(scrollContent).TransformPoint(new Windows.Foundation.Point(0, 0));
            var targetLeft = origin.X + pdfLeft * newScale;
            var targetTop = origin.Y + pdfTopFromTop * newScale;
            var centerX = targetLeft + (pdfWidth * newScale) / 2 - _scrollViewer.ViewportWidth / 2;
            var centerY = targetTop + (pdfHeight * newScale) / 2 - _scrollViewer.ViewportHeight / 2;
            _scrollViewer.ChangeView(Math.Max(0, centerX), Math.Max(0, centerY), null, disableAnimation: false);
            _status.Text = PdfZoomAreaStatus.FormatZoomedToArea(newScale);
        }
        catch
        {
            _status.Text = PdfZoomAreaStatus.FormatZoomed(newScale);
        }
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

        _status.Text = PageEditStatus.Rotating;
        await RunPageEditAsync(() => _pageEditor.RotatePagesAsync(_document, indexes, deltaDegrees));
        await ReloadAfterPageEditAsync();
        _status.Text = PageEditStatus.FormatRotated(indexes.Count);
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
            _status.Text = PageEditStatus.CannotDeleteEveryPage;
            return;
        }

        _status.Text = PageEditStatus.Deleting;
        await RunPageEditAsync(() => _pageEditor.DeletePagesAsync(_document, indexes));
        await ReloadAfterPageEditAsync();
        _status.Text = PageEditStatus.FormatDeleted(indexes.Count);
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

        _status.Text = PageEditStatus.Reordering;
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

        _status.Text = PageEditStatus.PagesReordered;
    }

    private List<int> SelectedOrCurrentPages() =>
        _pageSelection.SelectedOrFallback(CurrentPageIndex).ToList();

    private async Task InsertBlankAfterSelectionAsync()
    {
        var insertAt = SelectedOrCurrentPages().DefaultIfEmpty(CurrentPageIndex).Max() + 1;
        var template = _document.GetPage(Math.Clamp(CurrentPageIndex, 0, _document.PageCount - 1));
        _status.Text = PageEditStatus.InsertingBlankPage;
        await RunPageEditAsync(() => _pageEditor.InsertBlankPageAsync(
            _document,
            insertAt,
            template.WidthPoints,
            template.HeightPoints));
        _pageSelection.SelectOnly(insertAt);
        await ReloadAfterPageEditAsync();
        await GoToPageAsync(insertAt, recordHistory: true);
        _status.Text = PageEditStatus.InsertedBlankPage;
    }

    private async Task DuplicateSelectedAsync()
    {
        var indexes = SelectedOrCurrentPages();
        if (indexes.Count == 0)
        {
            return;
        }

        _status.Text = PageEditStatus.Duplicating;
        await RunPageEditAsync(() => _pageEditor.DuplicatePagesAsync(_document, indexes));
        await ReloadAfterPageEditAsync();
        _status.Text = PageEditStatus.FormatDuplicated(indexes.Count);
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
            _status.Text = AnnotationToolModeStatus.HighlightOff;
            return;
        }

        var picked = await PickHighlightColorAsync();
        if (picked is null)
        {
            _status.Text = AnnotationToolModeStatus.HighlightCancelled;
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

        _highlightMode = PersistentHighlightMode.Toggle(false);
        _highlightModeColor = picked.Value;
        RefreshToolButtonChrome();
        _status.Text = AnnotationToolModeStatus.HighlightOn;

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
                ? AnnotationToolModeStatus.HighlightOnShort
                : AnnotationToolModeStatus.SelectTextThenMarkup;
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
                    _status.Text = AnnotationToolModeStatus.HighlightMarkCancelled;
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
                PdfTextMarkupKind.Highlight => AnnotationToolModeStatus.Highlighting,
                PdfTextMarkupKind.Underline => AnnotationToolModeStatus.Underlining,
                _ => AnnotationToolModeStatus.StrikingThrough,
            };

            var created = await _annotations.AddTextMarkupAsync(
                _document,
                _selectionPageIndex,
                kind,
                _selectionQuads,
                color);
            RememberAnnotationForUndo(created);

            NotifyEdited();

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = kind switch
            {
                PdfTextMarkupKind.Highlight => _highlightMode
                    ? AnnotationToolModeStatus.HighlightAddedContinue
                    : AnnotationToolModeStatus.HighlightAdded,
                PdfTextMarkupKind.Underline => AnnotationToolModeStatus.UnderlineAdded,
                _ => AnnotationToolModeStatus.StrikethroughAdded,
            };
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Markup, ex.Message);
        }
    }

    private async Task<PdfAnnotationColor?> PickHighlightColorAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.ColorDialog);

        var list = new ListView
        {
            Height = 220,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.HighlightPresets.Select(p => p.Name).ToList(),
        };
        list.SelectedIndex = 0;
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.HighlightColor,
            Content = list,
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
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

    private void BeginCalloutTipEdit()
    {
        if (!TryGetSelectedAnnotation(out var item) || !item.IsCallout)
        {
            _status.Text = AnnotationSelectionStatus.CalloutTip;
            return;
        }

        ClearShapeMode();
        ClearInkMode();
        ClearFreeformMode();
        ClearPolygonMode();
        ClearSignatureMode();
        ClearHighlightMode();
        ClearCalloutMode();
        ClearEraserMode();
        _calloutTipEditMode = true;
        _calloutTipTarget = item;
        _status.Text = AnnotationToolModeStatus.CalloutTipPrompt;
    }

    private async Task FinishCalloutTipEditAsync(Border border, int pageIndex, PointerRoutedEventArgs e)
    {
        var target = _calloutTipTarget;
        ClearCalloutTipEditMode();
        if (target is null || pageIndex != target.PageIndex)
        {
            _status.Text = AnnotationToolModeStatus.CalloutTipWrongPage;
            return;
        }

        var page = _document.GetPage(pageIndex);
        var pt = e.GetCurrentPoint(border).Position;
        var tip = PdfPageCoordinates.FromDisplayPoint(pt.X, pt.Y, page.HeightPoints, _scale);

        try
        {
            _status.Text = AnnotationPersistStatus.MovingCalloutTip;
            await _annotations.SetCalloutTipAsync(
                _document,
                pageIndex,
                target.AnnotIndex,
                tip,
                pointerWidthPoints: Math.Max(1f, _drawStrokeWidth));
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            var refreshed = _annotationItems.FirstOrDefault(a => a.IsCallout && a.PageIndex == pageIndex)
                ?? target;
            _selectedAnnot = refreshed;
            SyncSidebarSelection(refreshed);
            DrawAnnotSelection(refreshed);
            _status.Text = AnnotationToolModeStatus.CalloutTipMoved;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.CalloutTip, ex.Message);
        }
    }

    private async Task SetSelectedAnnotationFillAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.FillDialog);

        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = AnnotationSelectionStatus.ShapeOrTextBoxFill;
            return;
        }

        if (!(item.IsTextBox || item.ShapeKind is PdfShapeKind.Rectangle
                or PdfShapeKind.RoundedRectangle
                or PdfShapeKind.HighlightRectangle
                or PdfShapeKind.Ellipse
                or PdfShapeKind.Loupe))
        {
            _status.Text = AnnotationPersistStatus.FillAppliesHint;
            return;
        }

        var fills = PdfAnnotationColorPresets.FillChoices.ToArray();
        var list = new ListView
        {
            Height = 220,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = fills.Select(f => f.Name).ToList(),
            SelectedIndex = 1,
        };
        var dialog = new ContentDialog
        {
            Title = PdfAnnotationColorPresets.FormatFillTitle(PdfAnnotationListLabel.Format(item)),
            Content = list,
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var fill = fills[Math.Clamp(list.SelectedIndex, 0, fills.Length - 1)].Color;
        try
        {
            await _annotations.SetFillColorAsync(_document, item.PageIndex, item.AnnotIndex, fill);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = fill is null ? AnnotationMutationStatus.FillCleared : AnnotationMutationStatus.FillColorUpdated;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Fill, ex.Message);
        }
    }

    private async Task SetSelectedAnnotationColorAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.ColorDialog);

        var index = _annotationList.SelectedIndex;
        PdfAnnotationInfo? item = index >= 0 && index < _annotationItems.Count
            ? _annotationItems[index]
            : _selectedAnnot;
        if (item is null)
        {
            _status.Text = AnnotationSelectionStatus.AnnotationColor;
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
                Title = $"Note color — {PdfAnnotationListLabel.Format(item)}",
                Content = list,
                PrimaryButtonText = DialogButtons.Apply,
                CloseButtonText = DialogButtons.Cancel,
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
            var presets = PdfAnnotationColorPresets.StrokePresets.ToArray();
            var list = new ListView
            {
                Height = 220,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = presets.Select(p => p.Name).ToList(),
            };
            list.SelectedIndex = 0;
            var dialog = new ContentDialog
            {
                Title = $"Color — {PdfAnnotationListLabel.Format(item)}",
                Content = list,
                PrimaryButtonText = DialogButtons.Apply,
                CloseButtonText = DialogButtons.Cancel,
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
            _status.Text = AnnotationMutationStatus.ColorUpdated;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Color, ex.Message);
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
                    && !(a.IsInk && (a.Contents == "CalloutPointer"
                        || (a.Contents?.StartsWith("CalloutPointer:", StringComparison.Ordinal) == true)
                        || a.Contents == "GlyphTextUnderline"
                        || (a.Contents?.StartsWith("GlyphTextUnderline:", StringComparison.Ordinal) == true))))
                .OrderBy(a => a.PageIndex)
                .ThenBy(a => a.AnnotIndex)
                .ToList();

            _suppressAnnotationNav = true;
            _annotationList.ItemsSource = _annotationItems
                .Select(PdfAnnotationListLabel.Format)
                .ToList();
            _suppressAnnotationNav = false;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.AnnotationList, ex.Message);
        }
    }

    /// <summary>
    /// When a note/textbox dialog opens empty, seed from clipboard text (F40-09).
    /// Native TextBox Ctrl+V also works once the field is focused.
    /// </summary>
    private static async Task SeedTextBoxFromClipboardAsync(TextBox box)
    {
        if (box is null || !ClipboardTextSeedPolicy.ShouldSeed(box.Text))
        {
            return;
        }

        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var text = await content.GetTextAsync();
            var seeded = ClipboardTextSeedPolicy.ApplyClipboardText(box.Text, text);
            if (!ReferenceEquals(seeded, box.Text) && seeded is not null)
            {
                box.Text = seeded;
            }
        }
        catch
        {
            // Clipboard may be locked by another app; leave the box empty.
        }
    }


    private void ToggleCalloutMode()
    {
        ClearRedactionMode();
        ClearCalloutTipEditMode();
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
            _status.Text = AnnotationToolModeStatus.CalloutOff;
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
        _status.Text = AnnotationToolModeStatus.CalloutOn;
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
        var tip = PdfPageCoordinates.FromDisplayPoint(start.X, start.Y, page.HeightPoints, _scale);
        var boxCenter = PdfPageCoordinates.FromDisplayPoint(end.X, end.Y, page.HeightPoints, _scale);
        var boxWidth = Math.Min(180, page.WidthPoints * 0.4);
        var boxHeight = 56;
        var left = Math.Clamp(boxCenter.X - (boxWidth / 2), 8, Math.Max(8, page.WidthPoints - boxWidth - 8));
        var bottom = Math.Clamp(boxCenter.Y - (boxHeight / 2), 8, Math.Max(8, page.HeightPoints - boxHeight - 8));
        var textBounds = new PdfRect(left, bottom, left + boxWidth, bottom + boxHeight);

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.CalloutDialog);
        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 100,
            PlaceholderText = PdfDialogPlaceholders.CalloutText,
        };
        await SeedTextBoxFromClipboardAsync(box);
        var fontSizeBox = new NumberBox
        {
            Header = PdfDialogHeaders.FontSizePt,
            Value = 12,
            Minimum = 6,
            Maximum = 72,
            SmallChange = 1,
            LargeChange = 2,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var fontFamilyBox = new ComboBox
        {
            Header = PdfDialogHeaders.Font,
            ItemsSource = PdfDialogOptions.StandardFonts.ToList(),
            SelectedIndex = 0,
            Width = 220,
        };
        var boldCheck = new CheckBox { Content = PdfViewerChromeLabels.Bold, IsChecked = false };
        var italicCheck = new CheckBox { Content = PdfViewerChromeLabels.Italic, IsChecked = false };
        var underlineCheck = new CheckBox { Content = PdfViewerChromeLabels.Underline, IsChecked = false };
        var alignBox = new ComboBox
        {
            Header = PdfDialogHeaders.Align,
            ItemsSource = PdfDialogOptions.HorizontalAlignments.ToList(),
            SelectedIndex = 0,
            Width = 220,
        };
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
                alignBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { boldCheck, italicCheck, underlineCheck },
                },
                new TextBlock { Text = PdfViewerTextLabels.TextColor, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                textColorList,
            },
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.Callout,
            Content = panel,
            PrimaryButtonText = DialogButtons.Add,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = AnnotationToolModeStatus.CalloutCancelled;
            return;
        }

        var textColor = PdfAnnotationColor.StrokePresets[
            Math.Clamp(textColorList.SelectedIndex, 0, PdfAnnotationColor.StrokePresets.Count - 1)].Color;
        var fontSize = (float)(double.IsNaN(fontSizeBox.Value) ? 12 : Math.Clamp(fontSizeBox.Value, 6, 72));
        var fontResource = PdfFreeTextFont.ResolveResourceName(
            (PdfFreeTextFontFamily)Math.Clamp(fontFamilyBox.SelectedIndex, 0, 2),
            boldCheck.IsChecked == true,
            italicCheck.IsChecked == true);
        var quadding = (PdfTextQuadding)Math.Clamp(alignBox.SelectedIndex, 0, 2);

        try
        {
            _status.Text = AnnotationGroupStatus.AddingCallout;
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
                pointerWidthPoints: _drawStrokeWidth,
                underline: underlineCheck.IsChecked == true,
                quadding: quadding);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = AnnotationToolModeStatus.CalloutAdded;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Callout, ex.Message);
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
            _status.Text = AnnotationToolModeStatus.InkOff;
            RefreshToolButtonChrome();
            return;
        }

        var picked = await PickStrokeStyleAsync("Ink stroke");
        if (picked is null)
        {
            _status.Text = AnnotationToolModeStatus.InkCancelled;
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
        _status.Text = AnnotationToolModeStatus.InkOn;
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
            _status.Text = AnnotationToolModeStatus.EraserOff;
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
        _selectedAnnots.Clear();
        RefreshToolButtonChrome();
        _status.Text = AnnotationToolModeStatus.EraserOn;
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
        var hit = PdfAnnotationHitTest.HitTestWithPad(onPage.Where(a => a.IsInk), pdfX, pdfY, pad)
            ?? PdfAnnotationHitTest.HitTestWithPad(onPage, pdfX, pdfY, pad);

        if (hit is null)
        {
            _status.Text = AnnotationToolModeStatus.EraserMiss;
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
            _selectedAnnots.Clear();
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = AnnotationMutationStatus.FormatErased(PdfAnnotationListLabel.Format(hit));
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Eraser, ex.Message);
        }
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
            _status.Text = AnnotationToolModeStatus.FreeformOff;
            RefreshToolButtonChrome();
            return;
        }

        var picked = await PickStrokeStyleAsync("Freeform stroke");
        if (picked is null)
        {
            _status.Text = AnnotationToolModeStatus.FreeformCancelled;
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
        _status.Text = AnnotationToolModeStatus.FreeformOn;
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
            _status.Text = AnnotationToolModeStatus.PolygonOff;
            RefreshToolButtonChrome();
            return;
        }

        var picked = await PickStrokeStyleAsync("Polygon stroke");
        if (picked is null)
        {
            _status.Text = AnnotationToolModeStatus.PolygonModeCancelled;
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
        _status.Text = AnnotationToolModeStatus.PolygonOn;
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
        var pdf = PdfPageCoordinates.FromDisplayPoint(ui.X, ui.Y, page.HeightPoints, _scale);

        if (_polygonPageIndex >= 0 && _polygonPageIndex != pageIndex)
        {
            _status.Text = AnnotationGroupStatus.FinishPolygonFirst;
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
        _status.Text = AnnotationToolModeStatus.FormatPolygonVertex(_polygonVertices.Count);
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
            _status.Text = AnnotationToolModeStatus.PolygonNeedsVertices;
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
            _status.Text = AnnotationPersistStatus.SavingPolygon;
            await _annotations.AddPolygonAsync(_document, pageIndex, vertices, color, width);
            // Re-list to capture the created annot for stroke undo.
            var listed = await _annotations.ListAsync(_document, pageIndex);
            var created = listed.LastOrDefault(a => a.ShapeKind == PdfShapeKind.Polygon && a.IsInk);
            if (created is not null)
            {
                _strokeUndoStack.Push(created);
                NotifyEdited();
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = AnnotationToolModeStatus.PolygonAdded;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Polygon, ex.Message);
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
            _status.Text = AnnotationToolModeStatus.ShapeOff;
            RefreshToolButtonChrome();
            return;
        }

        if (kind == PdfShapeKind.HighlightRectangle)
        {
            var hiColor = await PickHighlightColorAsync();
            if (hiColor is null)
            {
                _status.Text = AnnotationGroupStatus.AreaHighlightCancelled;
                return;
            }

            _drawStrokeColor = hiColor.Value;
            _drawStrokeWidth = 0.5f;
        }
        else if (kind == PdfShapeKind.Loupe)
        {
            // Fixed lens styling — skip stroke dialog for a one-click loupe tool.
            _drawStrokeColor = new PdfAnnotationColor(30, 100, 180);
            _drawStrokeWidth = 2f;
        }
        else
        {
            var picked = await PickStrokeStyleAsync(
                "Shape stroke",
                includeInkLineStyle: kind is PdfShapeKind.Line or PdfShapeKind.Arrow,
                includeArrowheadStyle: kind == PdfShapeKind.Arrow);
            if (picked is null)
            {
                _status.Text = AnnotationToolModeStatus.ShapeCancelled;
                return;
            }

            _drawStrokeColor = picked.Value.Color;
            _drawStrokeWidth = picked.Value.WidthPoints;
            _drawInkLineStyle = picked.Value.InkLineStyle;
            _drawArrowheadStyle = picked.Value.ArrowheadStyle;
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
            PdfShapeKind.Rectangle => AnnotationToolModeStatus.RectangleMode,
            PdfShapeKind.RoundedRectangle => AnnotationToolModeStatus.RoundedRectangleMode,
            PdfShapeKind.HighlightRectangle => AnnotationToolModeStatus.AreaHighlightMode,
            PdfShapeKind.Ellipse => AnnotationToolModeStatus.EllipseMode,
            PdfShapeKind.Arrow => AnnotationToolModeStatus.ArrowMode,
            PdfShapeKind.Star => AnnotationToolModeStatus.StarMode,
            PdfShapeKind.SpeechBubble => AnnotationToolModeStatus.BubbleMode,
            PdfShapeKind.Loupe => AnnotationToolModeStatus.LoupeMode,
            _ => AnnotationToolModeStatus.LineMode,
        };
    }

    private async Task<(PdfAnnotationColor Color, float WidthPoints, PdfInkLineStyle InkLineStyle, PdfArrowheadStyle ArrowheadStyle)?> PickStrokeStyleAsync(
        string title,
        bool includeInkLineStyle = false,
        bool includeArrowheadStyle = false)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.StrokeStyleDialog);

        float[] widths = PdfStrokeWidthPresets.Points.ToArray();
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
            ItemsSource = widths.Select(PdfStrokeWidthPresets.FormatLabel).ToList(),
        };
        widthList.SelectedIndex = PdfStrokeWidthPresets.DefaultSelectedIndex(_drawStrokeWidth);

        ComboBox? lineStyleBox = null;
        if (includeInkLineStyle)
        {
            lineStyleBox = new ComboBox
            {
                Header = PdfDialogHeaders.LineStyle,
                ItemsSource = PdfDialogOptions.LineStyles.ToList(),
                SelectedIndex = (int)_drawInkLineStyle,
                Width = 220,
            };
        }

        ComboBox? arrowheadBox = null;
        if (includeArrowheadStyle)
        {
            arrowheadBox = new ComboBox
            {
                Header = PdfDialogHeaders.Arrowhead,
                ItemsSource = PdfDialogOptions.Arrowheads.ToList(),
                SelectedIndex = (int)_drawArrowheadStyle,
                Width = 220,
            };
        }

        var panel = new StackPanel { Spacing = 8, Children = { } };
        panel.Children.Add(new TextBlock { Text = PdfViewerTextLabels.Color, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(colorList);
        panel.Children.Add(new TextBlock { Text = PdfViewerTextLabels.Width, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(widthList);
        if (lineStyleBox is not null)
        {
            panel.Children.Add(lineStyleBox);
        }

        if (arrowheadBox is not null)
        {
            panel.Children.Add(arrowheadBox);
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = DialogButtons.Use,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var colorIndex = Math.Clamp(colorList.SelectedIndex, 0, PdfAnnotationColor.StrokePresets.Count - 1);
        var widthIndex = Math.Clamp(widthList.SelectedIndex, 0, widths.Length - 1);
        var lineStyle = lineStyleBox is null
            ? _drawInkLineStyle
            : (PdfInkLineStyle)Math.Clamp(lineStyleBox.SelectedIndex, 0, 2);
        var arrowhead = arrowheadBox is null
            ? _drawArrowheadStyle
            : (PdfArrowheadStyle)Math.Clamp(arrowheadBox.SelectedIndex, 0, 2);
        return (PdfAnnotationColor.StrokePresets[colorIndex].Color, widths[widthIndex], lineStyle, arrowhead);
    }

    private void ClearShapeMode()
    {
        CancelShapeDrag();
        _shapeMode = null;
    }

    private void ClearInkMode()
    {
        if (!_inkMode)
        {
            return;
        }

        _inkMode = false;
        CancelInkStroke();
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

    private void ClearCalloutTipEditMode()
    {
        _calloutTipEditMode = false;
        _calloutTipTarget = null;
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

        if (_bubbleButton is not null)
        {
            _bubbleButton.Background = _shapeMode == PdfShapeKind.SpeechBubble ? active : null;
        }

        if (_loupeButton is not null)
        {
            _loupeButton.Background = _shapeMode == PdfShapeKind.Loupe ? active : null;
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
        if (_shapeMode == PdfShapeKind.Loupe)
        {
            // Force a circle: diameter from the larger axis, anchored at drag start.
            var diameter = Math.Max(width, height);
            width = diameter;
            height = diameter;
            left = current.X >= _shapeStart.X ? _shapeStart.X : _shapeStart.X - diameter;
            top = current.Y >= _shapeStart.Y ? _shapeStart.Y : _shapeStart.Y - diameter;
        }

        var stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(
            _drawStrokeColor.A,
            _drawStrokeColor.R,
            _drawStrokeColor.G,
            _drawStrokeColor.B));
        var fillAlpha = (byte)(_shapeMode == PdfShapeKind.HighlightRectangle
            ? 70
            : _shapeMode == PdfShapeKind.Loupe ? 28 : 40);
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
                strokeThickness,
                _drawInkLineStyle,
                PdfArrowheadStyle.Open);
        }
        else
        {
            preview = _shapeMode switch
            {
                PdfShapeKind.Ellipse or PdfShapeKind.Loupe => new Microsoft.UI.Xaml.Shapes.Ellipse
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
                    strokeThickness,
                    _drawInkLineStyle,
                    _drawArrowheadStyle),
                PdfShapeKind.Star => CreateStarPreview(
                    left,
                    top,
                    width,
                    height,
                    stroke,
                    strokeThickness),
                PdfShapeKind.SpeechBubble => CreateSpeechBubblePreview(
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

        PdfRect bounds;
        if (kind is PdfShapeKind.Line or PdfShapeKind.Arrow)
        {
            var startPdf = PdfPageCoordinates.FromDisplayPoint(start.X, start.Y, page.HeightPoints, _scale);
            var endPdf = PdfPageCoordinates.FromDisplayPoint(end.X, end.Y, page.HeightPoints, _scale);
            // Line/Arrow store endpoints in the rect corners (not necessarily normalized).
            bounds = new PdfRect(startPdf.X, startPdf.Y, endPdf.X, endPdf.Y);
        }
        else
        {
            var uiLeft = Math.Min(start.X, end.X);
            var uiTop = Math.Min(start.Y, end.Y);
            var uiWidth = Math.Abs(end.X - start.X);
            var uiHeight = Math.Abs(end.Y - start.Y);
            if (kind == PdfShapeKind.Loupe)
            {
                var diameter = Math.Max(uiWidth, uiHeight);
                uiWidth = diameter;
                uiHeight = diameter;
                uiLeft = end.X >= start.X ? start.X : start.X - diameter;
                uiTop = end.Y >= start.Y ? start.Y : start.Y - diameter;
            }

            bounds = PdfPageCoordinates.FromDisplayRectXywh(
                uiLeft,
                uiTop,
                uiWidth,
                uiHeight,
                page.HeightPoints,
                _scale);
        }

        try
        {
            _status.Text = AnnotationPersistStatus.SavingShape;
            await _annotations.AddShapeAsync(
                _document,
                pageIndex,
                kind.Value,
                bounds,
                _drawStrokeColor,
                fillColor: PdfShapeFillPolicy.FromStroke(kind.Value, _drawStrokeColor),
                borderWidthPoints: _drawStrokeWidth,
                inkLineStyle: kind is PdfShapeKind.Line or PdfShapeKind.Arrow
                    ? _drawInkLineStyle
                    : PdfInkLineStyle.Solid,
                arrowheadStyle: kind == PdfShapeKind.Arrow
                    ? _drawArrowheadStyle
                    : PdfArrowheadStyle.Open);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = kind switch
            {
                PdfShapeKind.Rectangle => AnnotationToolModeStatus.RectangleAdded,
                PdfShapeKind.RoundedRectangle => AnnotationToolModeStatus.RoundedRectangleAdded,
                PdfShapeKind.HighlightRectangle => AnnotationToolModeStatus.AreaHighlightAdded,
                PdfShapeKind.Ellipse => AnnotationToolModeStatus.EllipseAdded,
                PdfShapeKind.Arrow => AnnotationToolModeStatus.ArrowAdded,
                PdfShapeKind.Star => AnnotationToolModeStatus.StarAdded,
                PdfShapeKind.SpeechBubble => AnnotationToolModeStatus.SpeechBubbleAdded,
                PdfShapeKind.Loupe => AnnotationToolModeStatus.LoupeAdded,
                _ => AnnotationToolModeStatus.LineAdded,
            };
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Shape, ex.Message);
        }
    }

    private static FrameworkElement CreateSpeechBubblePreview(
        double left,
        double top,
        double width,
        double height,
        SolidColorBrush stroke,
        double strokeThickness)
    {
        // Map PDF bubble geometry (Y-up) into UI space (Y-down) within the drag rect.
        var pdfBounds = new PdfRect(0, 0, Math.Max(width, 1), Math.Max(height, 1));
        var pdfPoints = PdfSpeechBubbleGeometry.BuildPoints(pdfBounds);
        var points = new PointCollection();
        foreach (var p in pdfPoints)
        {
            points.Add(new Windows.Foundation.Point(left + p.X, top + (height - p.Y)));
        }

        return new Microsoft.UI.Xaml.Shapes.Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            Fill = null,
        };
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

    private static DoubleCollection? InkLineDashArray(PdfInkLineStyle style)
    {
        var pattern = PdfInkLineStyleDashPattern.ForPreview(style);
        if (pattern is null)
        {
            return null;
        }

        var dashes = new DoubleCollection();
        foreach (var value in pattern)
        {
            dashes.Add(value);
        }

        return dashes;
    }

    private static FrameworkElement CreateLineOrArrowPreview(
        PdfShapeKind kind,
        Windows.Foundation.Point start,
        Windows.Foundation.Point end,
        SolidColorBrush stroke,
        double strokeThickness = 2,
        PdfInkLineStyle inkLineStyle = PdfInkLineStyle.Solid,
        PdfArrowheadStyle arrowheadStyle = PdfArrowheadStyle.Open)
    {
        var dash = InkLineDashArray(inkLineStyle);
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
                StrokeDashArray = dash,
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

        var pdfStart = new PdfPagePoint(start.X, start.Y);
        var pdfEnd = new PdfPagePoint(end.X, end.Y);
        var headLen = PdfArrowGeometry.ComputeHeadLength(length);
        var (shaftEndPdf, headStrokes) = PdfArrowGeometry.Build(pdfStart, pdfEnd, arrowheadStyle, headLen);
        var shaftEnd = new Windows.Foundation.Point(shaftEndPdf.X, shaftEndPdf.Y);

        var points = new PointCollection { start, shaftEnd };
        foreach (var headStroke in headStrokes)
        {
            foreach (var p in headStroke)
            {
                points.Add(new Windows.Foundation.Point(p.X, p.Y));
            }
        }

        return new Microsoft.UI.Xaml.Shapes.Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            StrokeDashArray = dash,
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
        var pdf = PdfPageCoordinates.FromDisplayPoint(uiPoint.X, uiPoint.Y, page.HeightPoints, _scale);
        _inkPoints.Add(pdf);

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
            _status.Text = _freeformMode ? AnnotationToolModeStatus.FreeformNeedsPoints : AnnotationToolModeStatus.InkStrokeTooShort;
            return;
        }

        try
        {
            PdfAnnotationInfo created;
            if (_freeformMode)
            {
                _status.Text = AnnotationPersistStatus.SavingFreeform;
                created = await _annotations.AddFreeformAsync(
                    _document,
                    pageIndex,
                    points,
                    _drawStrokeColor,
                    borderWidthPoints: _drawStrokeWidth);
                _status.Text = AnnotationPersistStatus.FreeformShapeAdded;
            }
            else
            {
                _status.Text = AnnotationPersistStatus.SavingInk;
                created = await _annotations.AddInkAsync(
                    _document,
                    pageIndex,
                    points,
                    _drawStrokeColor,
                    borderWidthPoints: _drawStrokeWidth);
                _status.Text = AnnotationGroupStatus.InkStrokeAdded;
            }

            _strokeUndoStack.Push(created);
            NotifyEdited();
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();

            // Optional smart drawing: offer cleaned shape for freehand strokes.
            await MaybeOfferStrokeCleanupAsync(pageIndex, points, created);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.FormatFreeformOrInk(_freeformMode, ex.Message);
        }
    }

    private async Task MaybeOfferStrokeCleanupAsync(
        int pageIndex,
        IReadOnlyList<PdfPagePoint> points,
        PdfAnnotationInfo original)
    {
        var recognized = PdfStrokeShapeRecognizer.Recognize(points);
        if (recognized.Shape == PdfRecognizedStrokeShape.None)
        {
            return;
        }

        var window = _ownerWindow ?? App.CurrentApp.MainWindowInstance;
        if (window?.Content?.XamlRoot is null)
        {
            return;
        }

        var label = recognized.Shape switch
        {
            PdfRecognizedStrokeShape.Line => "line",
            PdfRecognizedStrokeShape.Rectangle => "rectangle",
            PdfRecognizedStrokeShape.Ellipse => "ellipse",
            PdfRecognizedStrokeShape.Triangle => "triangle",
            _ => "shape",
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.SmartDrawing,
            Content = $"That looked like a {label}. Use a cleaned-up shape, or keep the original stroke?",
            PrimaryButtonText = DialogButtons.UseCleaned,
            SecondaryButtonText = DialogButtons.KeepOriginal,
            CloseButtonText = DialogButtons.KeepOriginal,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _annotations.RemoveAsync(_document, original.PageIndex, original.AnnotIndex);
            _strokeUndoStack.TryPopIfMatches(original.PageIndex, original.AnnotIndex);

            PdfAnnotationInfo cleaned;
            switch (recognized.Shape)
            {
                case PdfRecognizedStrokeShape.Line:
                    cleaned = await _annotations.AddShapeAsync(
                        _document,
                        pageIndex,
                        PdfShapeKind.Line,
                        recognized.Bounds,
                        _drawStrokeColor,
                        borderWidthPoints: _drawStrokeWidth);
                    break;
                case PdfRecognizedStrokeShape.Rectangle:
                    cleaned = await _annotations.AddShapeAsync(
                        _document,
                        pageIndex,
                        PdfShapeKind.Rectangle,
                        recognized.Bounds,
                        _drawStrokeColor,
                        fillColor: PdfShapeFillPolicy.FromStroke(PdfShapeKind.Rectangle, _drawStrokeColor),
                        borderWidthPoints: _drawStrokeWidth);
                    break;
                case PdfRecognizedStrokeShape.Ellipse:
                    cleaned = await _annotations.AddShapeAsync(
                        _document,
                        pageIndex,
                        PdfShapeKind.Ellipse,
                        recognized.Bounds,
                        _drawStrokeColor,
                        fillColor: PdfShapeFillPolicy.FromStroke(PdfShapeKind.Ellipse, _drawStrokeColor),
                        borderWidthPoints: _drawStrokeWidth);
                    break;
                case PdfRecognizedStrokeShape.Triangle:
                    cleaned = await _annotations.AddPolygonAsync(
                        _document,
                        pageIndex,
                        recognized.Vertices ?? [],
                        _drawStrokeColor,
                        borderWidthPoints: _drawStrokeWidth);
                    break;
                default:
                    return;
            }

            _strokeUndoStack.Push(cleaned);
            NotifyEdited();
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = AnnotationGroupStatus.FormatReplacedStroke(label);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Cleanup, ex.Message);
        }
    }

    private async Task UndoLastStrokeAsync()
    {
        if (!_strokeUndoStack.TryPop(out var stroke))
        {
            _status.Text = DocumentUndoRedoStatus.NothingToUndo;
            return;
        }

        try
        {
            // Prefer the live sidebar entry in case indices shifted after other edits.
            var live = _annotationItems.FirstOrDefault(a =>
                a.PageIndex == stroke.PageIndex
                && a.AnnotIndex == stroke.AnnotIndex);
            var target = live ?? stroke;
            await _annotations.RemoveAsync(_document, target.PageIndex, target.AnnotIndex);
            if (_selectedAnnot is not null
                && _selectedAnnot.PageIndex == target.PageIndex
                && _selectedAnnot.AnnotIndex == target.AnnotIndex)
            {
                ClearAnnotSelectionVisual();
                _selectedAnnot = null;
                _selectedAnnots.Clear();
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = AnnotationMutationStatus.FormatUndid(PdfAnnotationListLabel.Format(target));
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.UndoAnnotation, ex.Message);
        }
    }

    private void RememberAnnotationForUndo(PdfAnnotationInfo created) => _strokeUndoStack.Push(created);

    private void RememberFormValueForUndo(PdfFormFieldInfo field) =>
        _formUndoStack.Push(field);

    private async Task UndoLastFormFillAsync()
    {
        if (!_formUndoStack.TryPop(out var entry))
        {
            _status.Text = FormOverlayModePolicy.NoChangeToUndo;
            return;
        }

        try
        {
            switch (entry.Kind)
            {
                case PdfFormFieldKind.CheckBox:
                    {
                        var wasOn = !string.Equals(entry.PreviousValue, "Off", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(entry.PreviousValue);
                        await _forms.SetCheckBoxAsync(
                            _document,
                            entry.PageIndex,
                            entry.AnnotIndex,
                            isChecked: wasOn);
                        break;
                    }
                case PdfFormFieldKind.RadioButton:
                    // Best-effort: re-select if previous was on; otherwise leave as-is after annot undo path.
                    if (!string.Equals(entry.PreviousValue, "Off", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(entry.PreviousValue))
                    {
                        await _forms.SetRadioButtonAsync(
                            _document,
                            entry.PageIndex,
                            entry.AnnotIndex);
                    }
                    else
                    {
                        // Cannot cleanly turn off a radio without a sibling; restore text state if API allows.
                        await _forms.SetTextValueAsync(
                            _document,
                            entry.PageIndex,
                            entry.AnnotIndex,
                            "Off",
                            autoFontSize: false);
                    }

                    break;
                default:
                    await _forms.SetTextValueAsync(
                        _document,
                        entry.PageIndex,
                        entry.AnnotIndex,
                        entry.PreviousValue);
                    break;
            }

            NotifyEdited();
            _cache.ClearDocument(_documentKey);
            await RenderVisibleAsync();
            if (_formOverlayMode)
            {
                _formOverlayFields = await _forms.ListFieldsAsync(_document);
                DrawFormOverlays();
            }

            _status.Text = FormOverlayModePolicy.UndidFieldChange;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.UndoFormFill, ex.Message);
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
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.SignatureDialog);

        if (_signatureMode)
        {
            ClearSignatureMode();
            RefreshToolButtonChrome();
            _status.Text = SignatureLibraryUi.DrawCancelled;
            return;
        }

        var library = (await _signatures.ListAsync()).ToList();
        if (library.Count > 0)
        {
            await ShowSignatureLibraryDialogAsync(window, library);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.Signature,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.Content.XamlRoot,
        };

        var drawBtn = new Button { Content = PdfViewerChromeLabels.DrawWithMouse, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4, 0, 0) };
        var importBtn = new Button { Content = PdfViewerChromeLabels.ImportImageEllipsis, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4, 0, 0) };
        var webcamBtn = new Button { Content = PdfViewerChromeLabels.WebcamEllipsis, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4, 0, 0) };
        ToolTipService.SetToolTip(webcamBtn, PdfViewerTooltips.PhotographASignatureOnPaperWith);
        var picked = 0; // 1=draw, 2=import, 3=webcam
        drawBtn.Click += (_, _) => { picked = 1; dialog.Hide(); };
        importBtn.Click += (_, _) => { picked = 2; dialog.Hide(); };
        webcamBtn.Click += (_, _) => { picked = 3; dialog.Hide(); };
        dialog.Content = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.SignatureDrawHint,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8),
                },
                drawBtn,
                importBtn,
                webcamBtn,
            },
        };

        await dialog.ShowAsync();
        if (picked == 1)
        {
            StartSignatureDrawMode();
            return;
        }

        if (picked == 2)
        {
            await ImportSignatureImageAsync(window);
            return;
        }

        if (picked == 3)
        {
            await CaptureWebcamSignatureAsync(window);
            return;
        }

        _status.Text = SignatureLibraryUi.Cancelled;
    }

    private async Task ShowSignatureLibraryDialogAsync(Window window, List<SignatureEntry> library)
    {
        var list = new ListView
        {
            Height = 200,
            SelectionMode = ListViewSelectionMode.Single,
            SelectedIndex = 0,
        };
        var descBox = new TextBox
        {
            Header = PdfDialogHeaders.AccessibilityDescriptionF5610,
            PlaceholderText = PdfDialogPlaceholders.SignatureOfExample,
            Width = 360,
        };
        var up = new Button { Content = PdfViewerChromeLabels.MoveUp, Padding = new Thickness(10, 4, 10, 4) };
        var down = new Button { Content = PdfViewerChromeLabels.MoveDown, Padding = new Thickness(10, 4, 10, 4) };
        var del = new Button { Content = PdfViewerChromeLabels.Delete, Padding = new Thickness(10, 4, 10, 4) };
        var saveDesc = new Button { Content = PdfViewerChromeLabels.SaveDescription, Padding = new Thickness(10, 4, 10, 4) };
        ToolTipService.SetToolTip(up, PdfViewerTooltips.MoveSelectedSignatureEarlierInThe);
        ToolTipService.SetToolTip(down, PdfViewerTooltips.MoveSelectedSignatureLaterInThe);
        ToolTipService.SetToolTip(del, PdfViewerTooltips.DeleteSelectedSignatureFromTheLibrary);
        ToolTipService.SetToolTip(saveDesc, PdfViewerTooltips.SaveAccessibilityDescriptionForTheSelected);
        AutomationProperties.SetName(up, PdfViewerAutomationNames.MoveSignatureUp);
        AutomationProperties.SetName(down, PdfViewerAutomationNames.MoveSignatureDown);
        AutomationProperties.SetName(del, PdfViewerAutomationNames.DeleteSignature);
        AutomationProperties.SetName(saveDesc, PdfViewerAutomationNames.SaveSignatureDescription);
        AutomationProperties.SetName(descBox, PdfViewerAutomationNames.SignatureAccessibilityDescription);
        AutomationProperties.SetName(list, PdfViewerAutomationNames.SavedSignatures);

        void RefreshList(int selectIndex)
        {
            list.ItemsSource = library.Select(SignatureDisplayText.ListLabel).ToList();
            list.SelectedIndex = library.Count == 0
                ? -1
                : Math.Clamp(selectIndex, 0, library.Count - 1);
            if (list.SelectedIndex >= 0 && list.SelectedIndex < library.Count)
            {
                descBox.Text = library[list.SelectedIndex].Description;
            }
            else
            {
                descBox.Text = string.Empty;
            }
        }

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex >= 0 && list.SelectedIndex < library.Count)
            {
                descBox.Text = library[list.SelectedIndex].Description;
            }
        };
        RefreshList(0);

        up.Click += async (_, _) =>
        {
            var i = list.SelectedIndex;
            if (i <= 0 || i >= library.Count)
            {
                return;
            }

            (library[i - 1], library[i]) = (library[i], library[i - 1]);
            try
            {
                await _signatures.ReorderAsync(library.Select(e => e.Id).ToList());
                RefreshList(i - 1);
                _status.Text = SignatureLibraryUi.OrderUpdated;
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Reorder, ex.Message);
            }
        };
        down.Click += async (_, _) =>
        {
            var i = list.SelectedIndex;
            if (i < 0 || i >= library.Count - 1)
            {
                return;
            }

            (library[i + 1], library[i]) = (library[i], library[i + 1]);
            try
            {
                await _signatures.ReorderAsync(library.Select(e => e.Id).ToList());
                RefreshList(i + 1);
                _status.Text = SignatureLibraryUi.OrderUpdated;
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Reorder, ex.Message);
            }
        };
        del.Click += async (_, _) =>
        {
            var i = list.SelectedIndex;
            if (i < 0 || i >= library.Count)
            {
                return;
            }

            var entry = library[i];
            try
            {
                await _signatures.DeleteAsync(entry.Id);
                library.RemoveAt(i);
                RefreshList(i);
                _status.Text = SignatureLibraryUi.FormatDeleted(entry.Name);
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Delete, ex.Message);
            }
        };
        saveDesc.Click += async (_, _) =>
        {
            var i = list.SelectedIndex;
            if (i < 0 || i >= library.Count)
            {
                return;
            }

            try
            {
                var text = descBox.Text?.Trim() ?? string.Empty;
                await _signatures.UpdateDescriptionAsync(library[i].Id, text);
                library[i] = library[i] with { Description = text };
                RefreshList(i);
                _status.Text = SignatureLibraryUi.DescriptionSaved;
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.DescriptionSave, ex.Message);
            }
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = PdfViewerTextLabels.SavedSignaturesHint },
                list,
                descBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { up, down, del, saveDesc },
                },
            },
        };

        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.Signatures,
            Content = panel,
            PrimaryButtonText = DialogButtons.Insert,
            SecondaryButtonText = DialogButtons.DrawNew,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var importBtn = new Button { Content = PdfViewerChromeLabels.ImportImageEllipsis, HorizontalAlignment = HorizontalAlignment.Left };
        var webcamBtn = new Button { Content = PdfViewerChromeLabels.WebcamEllipsis, HorizontalAlignment = HorizontalAlignment.Left };
        ToolTipService.SetToolTip(webcamBtn, PdfViewerTooltips.PhotographASignatureOnPaperWith);
        var importRequested = false;
        var webcamRequested = false;
        importBtn.Click += (_, _) =>
        {
            importRequested = true;
            dialog.Hide();
        };
        webcamBtn.Click += (_, _) =>
        {
            webcamRequested = true;
            dialog.Hide();
        };
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { importBtn, webcamBtn },
        });

        var result = await dialog.ShowAsync();
        if (importRequested)
        {
            await ImportSignatureImageAsync(window);
            return;
        }

        if (webcamRequested)
        {
            await CaptureWebcamSignatureAsync(window);
            return;
        }

        if (result == ContentDialogResult.Primary)
        {
            if (library.Count == 0 || list.SelectedIndex < 0 || list.SelectedIndex >= library.Count)
            {
                _status.Text = SignatureLibraryUi.SelectToInsert;
                return;
            }

            await InsertLibrarySignatureAsync(library[list.SelectedIndex]);
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            StartSignatureDrawMode();
            return;
        }

        _status.Text = SignatureLibraryUi.Cancelled;
    }

    private async Task InsertLibrarySignatureAsync(SignatureEntry entry)
    {
        try
        {
            _status.Text = SignatureLibraryUi.Inserting;
            await using var stream = await _signatures.OpenImageAsync(entry.Id);
            using var mem = new MemoryStream();
            await stream.CopyToAsync(mem);
            mem.Position = 0;
            using var rasStream = mem.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(rasStream);
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
                _status.Text = SignatureLibraryUi.ImageEmpty;
                return;
            }

            var page = _document.GetPage(CurrentPageIndex);
            var targetWidth = Math.Min(180, page.WidthPoints * 0.35);
            var aspect = height / (double)width;
            var targetHeight = Math.Clamp(targetWidth * aspect, 24, page.HeightPoints * 0.25);
            var left = Math.Max(36, page.WidthPoints - targetWidth - 48);
            var bottom = Math.Max(36, 48.0);
            var bounds = new PdfRect(left, bottom, left + targetWidth, bottom + targetHeight);

            var stamp = await _annotations.AddStampAsync(
                _document,
                CurrentPageIndex,
                bounds,
                pixels,
                width,
                height);
            RememberAnnotationForUndo(stamp);
            await ApplySignatureContentsAsync(CurrentPageIndex, SignatureDisplayText.Contents(entry));

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = SignatureLibraryUi.FormatInserted(entry.Name);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.InsertSignature, ex.Message);
        }
    }


    private async Task ApplySignatureContentsAsync(int pageIndex, string contents)
    {
        try
        {
            var annots = await _annotations.ListAsync(_document, pageIndex);
            var stamp = annots.LastOrDefault(a => a.IsStamp);
            if (stamp is not null)
            {
                await _annotations.SetContentsAsync(_document, stamp.PageIndex, stamp.AnnotIndex, contents);
            }
        }
        catch
        {
            // Contents is accessibility metadata; insertion already succeeded.
        }
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
        _status.Text = SignatureLibraryUi.DrawPrompt;
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
            _status.Text = SignatureLibraryUi.StrokeTooShort;
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.SignatureName);

        var nameBox = new TextBox
        {
            Text = PdfViewerTextLabels.Signature,
            PlaceholderText = PdfDialogPlaceholders.SignatureName,
            Header = PdfDialogHeaders.Name,
        };
        var descBox = new TextBox
        {
            PlaceholderText = PdfDialogPlaceholders.SignatureOfExample,
            Header = PdfDialogHeaders.AccessibilityDescription,
        };
        AutomationProperties.SetName(nameBox, PdfViewerAutomationNames.SignatureName);
        AutomationProperties.SetName(descBox, PdfViewerAutomationNames.SignatureAccessibilityDescription);
        var nameDialog = new ContentDialog
        {
            Title = PdfDialogTitles.SaveSignature,
            Content = new StackPanel { Spacing = 8, Children = { nameBox, descBox } },
            PrimaryButtonText = DialogButtons.Insert,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await nameDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = SignatureLibraryUi.Cancelled;
            return;
        }

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Signature" : nameBox.Text.Trim();
        var description = descBox.Text?.Trim() ?? string.Empty;

        try
        {
            _status.Text = SignatureLibraryUi.Saving;
            var stroke = points.Select(p => (p.X, p.Y)).ToList();
            var raster = SignatureStrokeRasterizer.Rasterize(stroke);
            var png = SignaturePngEncoder.EncodeBgra(raster.BgraPixels, raster.PixelWidth, raster.PixelHeight);
            await using (var pngStream = new MemoryStream(png))
            {
                await _signatures.SaveAsync(name, pngStream, description);
            }

            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            var pad = raster.PaddingPoints;
            var bounds = new PdfRect(minX - pad, minY - pad, maxX + pad, maxY + pad);

            var stamp = await _annotations.AddStampAsync(
                _document,
                pageIndex,
                bounds,
                raster.BgraPixels,
                raster.PixelWidth,
                raster.PixelHeight);
            RememberAnnotationForUndo(stamp);
            await ApplySignatureContentsAsync(pageIndex, SignatureDisplayText.Contents(name, description));

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = SignatureLibraryUi.Inserted;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Signature, ex.Message);
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
            _status.Text = SignatureLibraryUi.Cancelled;
            return;
        }

        try
        {
            _status.Text = SignatureLibraryUi.Loading;
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
                _status.Text = SignatureLibraryUi.ImageEmpty;
                return;
            }

            // Persist a copy into the local signature library (PNG preferred).
            try
            {
                await using var copy = await file.OpenStreamForReadAsync();
                await _signatures.SaveAsync(
                    System.IO.Path.GetFileNameWithoutExtension(file.Name),
                    copy,
                    description: SignatureLibraryUi.FormatImageContents(file.Name));
            }
            catch
            {
                // Library save is best-effort; insertion can still proceed.
            }

            await InsertSignaturePixelsAsync(
                pixels,
                width,
                height,
                statusOnSuccess: SignatureLibraryUi.Inserted,
                contents: SignatureLibraryUi.FormatImageContents(file.Name));
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Signature, ex.Message);
        }
    }

    private async Task CaptureCameraIntoDocumentAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.Camera);
        try
        {
            _status.Text = WebcamCaptureUi.StartingCamera;
            var captured = await WebcamCaptureHelper.CaptureAsync(
                window.Content.XamlRoot,
                title: "Capture into PDF",
                hint: "Frame the page or photo, then Capture. It will be stamped on the current PDF page.");
            if (captured is null)
            {
                _status.Text = WebcamCaptureUi.CaptureCancelledOrUnavailable;
                return;
            }

            await InsertSignaturePixelsAsync(
                captured.BgraPixels,
                captured.Width,
                captured.Height,
                statusOnSuccess: "Camera capture inserted on page.");
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.CameraInsert, ex.Message);
        }
    }

    private async Task CaptureWebcamSignatureAsync(Window window)
    {
        try
        {
            _status.Text = WebcamCaptureUi.StartingWebcam;
            var captured = await WebcamCaptureHelper.CaptureAsync(
                window.Content.XamlRoot,
                title: SignatureLibraryUi.PhotographSignatureTitle,
                hint: SignatureLibraryUi.PhotographSignatureHint);
            if (captured is null)
            {
                var fallback = new ContentDialog
                {
                    Title = PdfDialogTitles.WebcamUnavailable,
                    Content = PdfDialogBodies.WebcamUnavailableImport,
                    PrimaryButtonText = DialogButtons.ImportImageEllipsis,
                    CloseButtonText = DialogButtons.Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = window.Content.XamlRoot,
                };
                if (await fallback.ShowAsync() == ContentDialogResult.Primary)
                {
                    await ImportSignatureImageAsync(window);
                }
                else
                {
                    _status.Text = SignatureLibraryUi.WebcamCancelled;
                }

                return;
            }

            var pixels = captured.BgraPixels;
            var width = captured.Width;
            var height = captured.Height;
            SignaturePaperKeying.KeyOutNearWhite(pixels, width, height);

            try
            {
                var png = SignaturePngEncoder.EncodeBgra(pixels, width, height);
                await using var pngStream = new MemoryStream(png);
                await _signatures.SaveAsync(
                    SignatureLibraryUi.FormatWebcamName(DateTime.Now),
                    pngStream,
                    description: SignatureLibraryUi.WebcamPaperSignature);
            }
            catch
            {
                // Library save is best-effort.
            }

            await InsertSignaturePixelsAsync(
                pixels,
                width,
                height,
                statusOnSuccess: SignatureLibraryUi.WebcamInserted,
                contents: SignatureLibraryUi.WebcamPaperSignature);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.WebcamSignature, ex.Message);
        }
    }

    private async Task InsertSignaturePixelsAsync(
        byte[] pixels,
        int width,
        int height,
        string statusOnSuccess,
        string? contents = null)
    {
        var page = _document.GetPage(CurrentPageIndex);
        var targetWidth = Math.Min(180, page.WidthPoints * 0.35);
        var aspect = height / (double)width;
        var targetHeight = Math.Clamp(targetWidth * aspect, 24, page.HeightPoints * 0.25);
        var left = Math.Max(36, page.WidthPoints - targetWidth - 48);
        var bottom = Math.Max(36, 48.0);
        var bounds = new PdfRect(left, bottom, left + targetWidth, bottom + targetHeight);

        var stamp = await _annotations.AddStampAsync(
            _document,
            CurrentPageIndex,
            bounds,
            pixels,
            width,
            height);
        RememberAnnotationForUndo(stamp);
        await ApplySignatureContentsAsync(
            CurrentPageIndex,
            string.IsNullOrWhiteSpace(contents) ? "Signature" : contents);

        _cache.ClearDocument(_documentKey);
        _cache.ClearDocument(_thumbnailKey);
        await RenderVisibleAsync();
        await RenderThumbnailsAsync();
        await RefreshAnnotationSidebarAsync();
        _status.Text = statusOnSuccess;
    }

    private async Task OnFormButtonClickAsync()
    {
        if (_formOverlayMode)
        {
            ClearFormOverlayMode();
            RefreshToolButtonChrome();
            _status.Text = FormOverlayModePolicy.Exited;
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.FormDialog);

        if (!await _forms.HasFormAsync(_document))
        {
            _status.Text = FormOverlayModePolicy.NoAcroFormFields;
            return;
        }

        var options = new ListView
        {
            Height = 180,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfDialogOptions.FormOverlayModes.ToList(),
            SelectedIndex = 0,
        };
        var chooser = new ContentDialog
        {
            Title = PdfDialogTitles.FormFill,
            Content = options,
            PrimaryButtonText = DialogButtons.Go,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await chooser.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        switch (options.SelectedIndex)
        {
            case 0:
                await BeginFormOverlayModeAsync();
                break;
            case 1:
                await EditFormFieldsAsync();
                break;
            case 2:
                await AutoFillFromProfileAsync();
                break;
            case 3:
                await EditFormAutofillProfileAsync();
                break;
        }
    }

    private async Task EditFormAutofillProfileAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.ProfileDialog);

        var current = _formProfile.Current;
        var nameBox = new TextBox { Header = PdfDialogHeaders.Name, Text = current.Name, Width = 320 };
        var addressBox = new TextBox
        {
            Header = PdfDialogHeaders.Address,
            Text = current.Address,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 72,
            Width = 320,
        };
        var emailBox = new TextBox { Header = PdfDialogHeaders.Email, Text = current.Email, Width = 320 };
        var phoneBox = new TextBox { Header = PdfDialogHeaders.Phone, Text = current.Phone, Width = 320 };
        var panel = new StackPanel
        {
            Spacing = 8,
            Children = { nameBox, addressBox, emailBox, phoneBox },
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.AutoFillProfile,
            Content = panel,
            PrimaryButtonText = DialogButtons.Save,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = FormFieldActionStatus.ProfileEditCancelled;
            return;
        }

        var profile = new FormAutofillProfile(
            nameBox.Text?.Trim() ?? string.Empty,
            addressBox.Text?.Trim() ?? string.Empty,
            emailBox.Text?.Trim() ?? string.Empty,
            phoneBox.Text?.Trim() ?? string.Empty);
        await _formProfile.SaveAsync(profile);
        _status.Text = profile.HasAnyValue
            ? AnnotationMutationStatus.AutoFillProfileSaved
            : AnnotationMutationStatus.AutoFillProfileCleared;
    }

    private async Task AutoFillFromProfileAsync()
    {
        var profile = _formProfile.Current;
        if (!profile.HasAnyValue)
        {
            _status.Text = FormFieldActionStatus.AutoFillEmpty;
            await EditFormAutofillProfileAsync();
            profile = _formProfile.Current;
            if (!profile.HasAnyValue)
            {
                return;
            }
        }

        var fields = await _forms.ListFieldsAsync(_document);
        var filled = 0;
        foreach (var field in fields)
        {
            if (field.Kind is not (PdfFormFieldKind.TextField
                or PdfFormFieldKind.ComboBox
                or PdfFormFieldKind.ListBox))
            {
                continue;
            }

            var value = FormAutofillMatcher.ResolveValue(profile, field.Name);
            if (value is null)
            {
                continue;
            }

            // Skip fields that already have a non-empty value.
            if (!string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            try
            {
                RememberFormValueForUndo(field);
                await _forms.SetTextValueAsync(_document, field.PageIndex, field.AnnotIndex, value);
                await _formValueHistory.RememberAsync(field.Name, value);
                filled++;
            }
            catch
            {
                // Continue filling other fields.
            }
        }

        if (filled > 0)
        {
            _cache.ClearDocument(_documentKey);
            await RenderVisibleAsync();
            if (_formOverlayMode)
            {
                _formOverlayFields = await _forms.ListFieldsAsync(_document);
                DrawFormOverlays();
            }
        }

        _status.Text = filled == 0
            ? AnnotationMutationStatus.AutoFillNoEmptyFields
            : AnnotationMutationStatus.FormatAutoFillUpdated(filled);
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
            _status.Text = FormOverlayModePolicy.NoFields;
            return;
        }

        _formOverlayMode = true;
        _formOverlayFields = fields;
        _formOverlayFocusIndex = 0;
        RefreshToolButtonChrome();
        DrawFormOverlays();
        await GoToPageAsync(fields[0].PageIndex, recordHistory: true);
        _status.Text = FormOverlayModePolicy.Started;
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
        _status.Text = FormFieldActionStatus.FormatFocused(next.Name, next.Kind.ToString());
    }

    private async Task EditFocusedFormOverlayFieldAsync()
    {
        if (_formOverlayFocusIndex < 0 || _formOverlayFocusIndex >= _formOverlayFields.Count)
        {
            _status.Text = FormOverlayModePolicy.NoFieldFocused;
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
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.FormDialog);

        if (!await _forms.HasFormAsync(_document))
        {
            _status.Text = FormOverlayModePolicy.NoAcroFormFields;
            return;
        }

        var fields = await _forms.ListFieldsAsync(_document);
        if (fields.Count == 0)
        {
            _status.Text = FormOverlayModePolicy.NoFields;
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
            Title = PdfDialogTitles.FormFields,
            Content = list,
            PrimaryButtonText = DialogButtons.Edit,
            SecondaryButtonText = DialogButtons.NextTab,
            CloseButtonText = DialogButtons.Close,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                _status.Text = FormOverlayModePolicy.EditorClosed;
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
    /// Activates an AcroForm push button (URI launch or in-doc GoTo when resolvable).
    /// Does not modify the document.
    /// </summary>
    private async Task<bool> ActivatePushButtonAsync(PdfFormFieldInfo field)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.ButtonDialog);

        var action = field.ButtonAction;
        var caption = !string.IsNullOrWhiteSpace(action?.Caption)
            ? action!.Caption!
            : (!string.IsNullOrWhiteSpace(field.Value) ? field.Value : field.Name);

        if (action?.Kind == PdfFormButtonActionKind.Uri && !string.IsNullOrWhiteSpace(action.Uri))
        {
            var confirm = new ContentDialog
            {
                Title = caption,
                Content = $"Open link?\n{action.Uri}",
                PrimaryButtonText = DialogButtons.Open,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            try
            {
                var raw = ExternalLaunchPolicy.LooksLikeHttpUrl(action.Uri)
                    ? action.Uri!
                    : ExternalLaunchPolicy.NormalizeHttpUrl(action.Uri!);
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps
                        && uri.Scheme != Uri.UriSchemeMailto))
                {
                    _status.Text = FormFieldActionStatus.UnsupportedButtonScheme;
                    return false;
                }

                await Launcher.LaunchUriAsync(uri);
                _status.Text = FormFieldActionStatus.FormatOpened(caption);
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.ButtonLink, ex.Message);
            }

            return false;
        }

        if (action?.Kind == PdfFormButtonActionKind.GoTo && action.DestPageIndex is int dest
            && dest >= 0 && dest < _document.PageCount)
        {
            await GoToPageAsync(dest, recordHistory: true);
            _status.Text = FormFieldActionStatus.FormatButtonToPage(caption, dest + 1);
            return false;
        }

        var info = action?.Kind switch
        {
            PdfFormButtonActionKind.GoTo =>
                "This button jumps elsewhere in the document, but the destination page could not be resolved.",
            PdfFormButtonActionKind.Other =>
                "This button uses an action type Glyph does not activate yet (submit/reset/JS/etc.).",
            _ => "This button has no resolvable action.",
        };
        var dlg = new ContentDialog
        {
            Title = caption,
            Content = info,
            CloseButtonText = DialogButtons.Close,
            XamlRoot = window.Content.XamlRoot,
        };
        await dlg.ShowAsync();
        _status.Text = FormFieldActionStatus.FormatButtonNoAction(caption);
        return false;
    }

    /// <summary>
    /// Places a library/drawn/imported signature stamp into an AcroForm signature field rect.
    /// Cryptographic PKCS#7 signing is out of scope; this is visual fill "where supported".
    /// </summary>
    private async Task<bool> FillFormSignatureFieldAsync(PdfFormFieldInfo field)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.SignatureDialog);

        IReadOnlyList<SignatureEntry> library;
        try
        {
            library = await _signatures.ListAsync();
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.CouldNotOpenSignatureLibrary, ex.Message);
            return false;
        }

        ContentDialog pick;
        ListView? list = null;
        if (library.Count > 0)
        {
            list = new ListView
            {
                Height = 200,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = library.Select(e => e.Name).ToList(),
                SelectedIndex = 0,
            };
            pick = new ContentDialog
            {
                Title = $"Sign {field.Name}",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = PdfViewerTextLabels.FormSignatureHint,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        list,
                    },
                },
                PrimaryButtonText = DialogButtons.Insert,
                SecondaryButtonText = DialogButtons.DrawNew,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };
        }
        else
        {
            pick = new ContentDialog
            {
                Title = $"Sign {field.Name}",
                Content = PdfViewerChromeLabels.NoSavedSignaturesHint,
                PrimaryButtonText = DialogButtons.Draw,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };
        }

        var result = await pick.ShowAsync();
        if (result == ContentDialogResult.None)
        {
            return false;
        }

        if (library.Count == 0 || result == ContentDialogResult.Secondary)
        {
            // Fall back to normal signature draw; user can then re-open the field.
            StartSignatureDrawMode();
            _status.Text = SignatureLibraryUi.DrawThenChooseFormField;
            return false;
        }

        if (list is null || list.SelectedIndex < 0 || list.SelectedIndex >= library.Count)
        {
            return false;
        }

        try
        {
            await InsertSignatureIntoFieldAsync(library[list.SelectedIndex], field);
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.FormSignature, ex.Message);
            return false;
        }
    }

    private async Task InsertSignatureIntoFieldAsync(SignatureEntry entry, PdfFormFieldInfo field)
    {
        _status.Text = SignatureLibraryUi.SigningFormField;
        await using var stream = await _signatures.OpenImageAsync(entry.Id);
        using var mem = new MemoryStream();
        await stream.CopyToAsync(mem);
        mem.Position = 0;
        using var rasStream = mem.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(rasStream);
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
            throw new InvalidOperationException(MainWindowRequiredMessages.SignatureImageEmpty);
        }

        var fieldW = Math.Max(1, field.Bounds.Width);
        var fieldH = Math.Max(1, field.Bounds.Height);
        var aspect = height / (double)width;
        var targetWidth = fieldW;
        var targetHeight = targetWidth * aspect;
        if (targetHeight > fieldH)
        {
            targetHeight = fieldH;
            targetWidth = targetHeight / aspect;
        }

        var left = field.Bounds.Left + ((fieldW - targetWidth) / 2);
        var bottom = field.Bounds.Bottom + ((fieldH - targetHeight) / 2);
        var bounds = new PdfRect(left, bottom, left + targetWidth, bottom + targetHeight);

        var stamp = await _annotations.AddStampAsync(
            _document,
            field.PageIndex,
            bounds,
            pixels,
            width,
            height);
        RememberAnnotationForUndo(stamp);
        await ApplySignatureContentsAsync(field.PageIndex, SignatureDisplayText.Contents(entry));

        _cache.ClearDocument(_documentKey);
        _cache.ClearDocument(_thumbnailKey);
        await RenderVisibleAsync();
        await RenderThumbnailsAsync();
        await RefreshAnnotationSidebarAsync();
        _status.Text = FormOverlayModePolicy.FormatSignedField(field.Name, entry.Name);
    }

    /// <summary>
    /// Opens the appropriate edit UI for one field. Returns true when the document was modified.
    /// </summary>
    private async Task<bool> TryEditFormFieldAsync(PdfFormFieldInfo field)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.FormDialog);

        if (field.Kind == PdfFormFieldKind.PushButton)
        {
            return await ActivatePushButtonAsync(field);
        }

        if (field.Kind == PdfFormFieldKind.Signature
            || SignatureFormPlacementPolicy.IsSignatureWidget(field.Kind.ToString()))
        {
            return await FillFormSignatureFieldAsync(field);
        }

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
                RememberFormValueForUndo(field);
                await _forms.SetRadioButtonAsync(
                    _document,
                    field.PageIndex,
                    field.AnnotIndex);
                NotifyEdited();
                _status.Text = FormFieldActionStatus.FormatSelectedRadio(field.Name);
                return true;
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.FormFill, ex.Message);
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
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot,
            };

            if (await toggle.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            try
            {
                RememberFormValueForUndo(field);
                await _forms.SetCheckBoxAsync(
                    _document,
                    field.PageIndex,
                    field.AnnotIndex,
                    isChecked: !currentlyOn);
                NotifyEdited();
                _status.Text = AnnotationMutationStatus.FormatUpdated(field.Name);
                return true;
            }
            catch (Exception ex)
            {
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.FormFill, ex.Message);
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
                    PrimaryButtonText = DialogButtons.Apply,
                    CloseButtonText = DialogButtons.Cancel,
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
                    RememberFormValueForUndo(field);
                    await _forms.SetTextValueAsync(
                        _document,
                        field.PageIndex,
                        field.AnnotIndex,
                        choice);
                    await _formValueHistory.RememberAsync(field.Name, choice);
                    NotifyEdited();
                    _status.Text = AnnotationMutationStatus.FormatUpdated(field.Name);
                    return true;
                }
                catch (Exception ex)
                {
                    _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.FormFill, ex.Message);
                    return false;
                }
            }
        }

        if (field.Kind is not (PdfFormFieldKind.TextField
            or PdfFormFieldKind.ComboBox
            or PdfFormFieldKind.ListBox))
        {
            _status.Text = FormFieldActionStatus.FormatUnsupportedEdit(field.Kind.ToString());
            return false;
        }

        var box = new TextBox
        {
            Text = field.Value,
            AcceptsReturn = FormMultilinePolicy.TextFieldAcceptsReturn
                && field.Kind == PdfFormFieldKind.TextField,
            TextWrapping = TextWrapping.Wrap,
            Height = 100,
            PlaceholderText = field.Name,
        };
        await SeedTextBoxFromClipboardAsync(box);
        var suggestions = _formValueHistory.GetSuggestions(field.Name);
        UIElement content = box;
        if (suggestions.Count > 0)
        {
            var recentList = new ListView
            {
                Height = 120,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = suggestions.ToList(),
            };
            recentList.SelectionChanged += (_, _) =>
            {
                if (recentList.SelectedItem is string picked)
                {
                    box.Text = picked;
                }
            };
            content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    box,
                    new TextBlock
                    {
                        Text = PdfViewerTextLabels.RecentValues,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    },
                    recentList,
                },
            };
        }

        var edit = new ContentDialog
        {
            Title = $"Edit {field.Name}",
            Content = content,
            PrimaryButtonText = DialogButtons.Save,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await edit.ShowAsync() != ContentDialogResult.Primary)
        {
            return false;
        }

        try
        {
            var value = box.Text ?? string.Empty;
            RememberFormValueForUndo(field);
            await _forms.SetTextValueAsync(
                _document,
                field.PageIndex,
                field.AnnotIndex,
                value);
            await _formValueHistory.RememberAsync(field.Name, value);
            NotifyEdited();
            _status.Text = AnnotationMutationStatus.FormatUpdated(field.Name);
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.FormFill, ex.Message);
            return false;
        }
    }

    private async Task FlattenAnnotationsAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.FlattenDialog);

        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.FlattenAnnotations,
            Content = PdfDialogBodies.FlattenAnnotationsConfirm,
            PrimaryButtonText = DialogButtons.Flatten,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _status.Text = FormOverlayModePolicy.FlattenCancelled;
            return;
        }

        try
        {
            _status.Text = FormOverlayModePolicy.Flattening;
            var outcome = await _annotations.FlattenAsync(_document);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();

            if (outcome.PagesFailed > 0)
            {
                _status.Text =
                    AnnotationMutationStatus.FormatFlattenedWithFailures(outcome.PagesChanged, outcome.PagesFailed);
            }
            else if (outcome.PagesChanged == 0)
            {
                _status.Text = FormOverlayModePolicy.NothingToFlatten;
            }
            else
            {
                _status.Text = AnnotationMutationStatus.FormatFlattenedAnnotations(outcome.PagesChanged);
            }
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Flatten, ex.Message);
        }
    }

    private async Task OnRedactButtonClickAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.RedactionDialog);

        var pending = _redaction.GetPending(_document);
        var hasSelection = !string.IsNullOrWhiteSpace(_selectedText)
            && _selectionPageIndex >= 0
            && _selectionQuads.Count > 0;
        var hasRegion = PdfRegionCopyPolicy.HasValidRegion(
            _regionCopyPageIndex,
            _regionCopyDisplayRect.Width,
            _regionCopyDisplayRect.Height);
        var hasFindMatches = !string.IsNullOrWhiteSpace(_searchQuery) && _hits.Count > 0;

        if (_redactionMode)
        {
            ClearRedactionMode();
            RefreshToolButtonChrome();
            _status.Text = PdfRedactionUiCopy.ModeOffWithPending(pending.Count);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = PdfRedactionUiCopy.DialogTitle,
            Content = pending.Count == 0
                ? PdfRedactionUiCopy.EmptyIntro
                : PdfRedactionUiCopy.FormatPendingStatus(pending.Count),
            XamlRoot = window.Content.XamlRoot,
        };

        if (pending.Count > 0)
        {
            dialog.PrimaryButtonText = PdfRedactionUiCopy.ApplyButton;
            dialog.SecondaryButtonText = PdfRedactionUiCopy.DrawMarksButton;
            dialog.CloseButtonText = DialogButtons.Cancel;
            dialog.DefaultButton = ContentDialogButton.Close;
        }
        else
        {
            dialog.PrimaryButtonText = PdfRedactionUiCopy.DrawMarksButton;
            dialog.CloseButtonText = DialogButtons.Cancel;
            dialog.DefaultButton = ContentDialogButton.Primary;
            if (hasFindMatches)
            {
                dialog.SecondaryButtonText = PdfRedactionUiCopy.MarkFindMatchesButton(_hits.Count);
            }
            else if (hasSelection)
            {
                dialog.SecondaryButtonText = PdfRedactionUiCopy.MarkSelectionButton;
            }
            else if (hasRegion)
            {
                dialog.SecondaryButtonText = PdfRedactionUiCopy.MarkRegionButton;
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

            _status.Text = PdfRedactionUiCopy.CancelledStatus;
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
        _status.Text = PdfRedactionUiCopy.ModeEnter;
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
            _status.Text = PdfRedactionUiCopy.MarkTooSmallStatus;
            return;
        }

        var page = _document.GetPage(pageIndex);
        var bounds = PdfPageCoordinates.FromDisplayRect(
            start.X,
            start.Y,
            end.X,
            end.Y,
            page.HeightPoints,
            _scale);

        try
        {
            _redaction.MarkRectangle(_document, pageIndex, bounds);
            RefreshPendingRedactionOverlay(pageIndex);
            var count = _redaction.GetPending(_document).Count;
            _status.Text = PdfRedactionUiCopy.FormatMarkedStatus(count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfRedactionUiCopy.FormatMarkFailed(ex.Message);
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
            _status.Text = PdfRedactionUiCopy.SelectTextPrompt;
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
            _status.Text = PdfRedactionUiCopy.FormatMarkedTextStatus(count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfRedactionUiCopy.FormatMarkFailed(ex.Message);
        }
    }

    private async Task MarkFindMatchesForRedactionAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery) || _hits.Count == 0)
        {
            _status.Text = PdfRedactionUiCopy.SelectTextPrompt;
            return;
        }

        try
        {
            var marked = 0;
            var pages = _hits.Select(h => h.PageIndex).Distinct().OrderBy(i => i).ToList();
            foreach (var pageIndex in pages)
            {
                var chars = await _textExtractor.GetCharsAsync(_document, pageIndex);
                if (chars.Count == 0)
                {
                    continue;
                }

                var pageText = string.Concat(chars.Select(c => c.Value));
                var from = 0;
                while (true)
                {
                    var found = pageText.IndexOf(_searchQuery, from, StringComparison.OrdinalIgnoreCase);
                    if (found < 0)
                    {
                        break;
                    }

                    var end = Math.Min(chars.Count - 1, found + _searchQuery.Length - 1);
                    var union = chars[found].Bounds;
                    for (var i = found; i <= end; i++)
                    {
                        var b = chars[i].Bounds;
                        union = new PdfRect(
                            Math.Min(union.Left, b.Left),
                            Math.Min(union.Bottom, b.Bottom),
                            Math.Max(union.Right, b.Right),
                            Math.Max(union.Top, b.Top));
                    }

                    _redaction.MarkTextRegion(
                        _document,
                        pageIndex,
                        new PdfRect(union.Left - 1, union.Bottom - 1, union.Right + 1, union.Top + 1),
                        TrimForStatus(_searchQuery));
                    marked++;
                    from = found + Math.Max(1, _searchQuery.Length);
                }

                RefreshPendingRedactionOverlay(pageIndex);
            }

            var count = _redaction.GetPending(_document).Count;
            _status.Text = marked > 0
                ? PdfRedactionUiCopy.FormatMarkedTextStatus(count)
                : PdfRedactionUiCopy.SelectTextPrompt;
        }
        catch (Exception ex)
        {
            _status.Text = PdfRedactionUiCopy.FormatMarkFailed(ex.Message);
        }
    }

    private void MarkRegionForRedaction()
    {
        if (!PdfRegionCopyPolicy.HasValidRegion(
                _regionCopyPageIndex,
                _regionCopyDisplayRect.Width,
                _regionCopyDisplayRect.Height))
        {
            _status.Text = PdfRedactionUiCopy.DragRegionPrompt;
            return;
        }

        try
        {
            var page = _document.GetPage(_regionCopyPageIndex);
            var bounds = PdfPageCoordinates.FromDisplayRectXywh(
                _regionCopyDisplayRect.X,
                _regionCopyDisplayRect.Y,
                _regionCopyDisplayRect.Width,
                _regionCopyDisplayRect.Height,
                page.HeightPoints,
                _scale);
            _redaction.MarkRectangle(_document, _regionCopyPageIndex, bounds);
            RefreshPendingRedactionOverlay(_regionCopyPageIndex);
            var count = _redaction.GetPending(_document).Count;
            _status.Text = PdfRedactionUiCopy.FormatMarkedRegionStatus(count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfRedactionUiCopy.FormatMarkFailed(ex.Message);
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
        _status.Text = PdfRedactionUiCopy.FormatRemovedStatus(remaining);
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
            if (!PdfRedactionOverlayLayout.TryMapToCanvas(
                    mark.Bounds,
                    page.HeightPoints,
                    _scale,
                    out var mapped))
            {
                continue;
            }

            var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = mapped.Width,
                Height = mapped.Height,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 0, 0, 0)),
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 200, 40, 40)),
                StrokeThickness = 1.5,
                Tag = mark.Id,
            };
            ToolTipService.SetToolTip(rect, PdfRedactionUiCopy.PendingOverlayTooltip);
            rect.PointerPressed += PendingRedactionRect_PointerPressed;
            Canvas.SetLeft(rect, mapped.Left);
            Canvas.SetTop(rect, mapped.Top);
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
                Canvas.SetLeft(label, mapped.Left + 4);
                Canvas.SetTop(label, mapped.Top + 2);
                overlay.Children.Add(label);
            }
        }
    }

    private async Task ApplyPendingRedactionsAsync()
    {
        var pending = _redaction.GetPending(_document);
        if (pending.Count == 0)
        {
            _status.Text = PdfRedactionUiCopy.FormatPendingStatus(0);
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.RedactionApply);

        var removeAnnotations = new CheckBox
        {
            Content = PdfViewerChromeLabels.RemoveIntersectingAnnotations,
            IsChecked = true,
        };
        var removeAttachments = new CheckBox
        {
            Content = PdfViewerChromeLabels.RemoveEmbeddedAttachments,
            IsChecked = true,
        };
        var removeMetadata = new CheckBox
        {
            Content = PdfViewerChromeLabels.ClearInfoMetadata,
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
                        PdfRedactionUiCopy.FormatApplyBody(pending.Count),
                },
                removeAnnotations,
                removeAttachments,
                removeMetadata,
            },
        };

        var dialog = new ContentDialog
        {
            Title = PdfRedactionUiCopy.ApplyDialogTitle,
            Content = panel,
            PrimaryButtonText = DialogButtons.Apply,
            SecondaryButtonText = DialogButtons.ClearMarks,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = window.Content.XamlRoot,
        };

        var applyChoice = await dialog.ShowAsync();
        if (applyChoice == ContentDialogResult.Secondary)
        {
            _redaction.ClearPending(_document);
            RefreshAllPendingRedactionOverlays();
            _status.Text = PdfRedactionUiCopy.ClearedPendingStatus;
            return;
        }

        if (applyChoice != ContentDialogResult.Primary)
        {
            _status.Text = FormFieldActionStatus.ApplyCancelled;
            return;
        }

        try
        {
            _status.Text = PdfRedactionUiCopy.ApplyingStatus;
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
                _status.Text = PdfRedactionUiCopy.NothingToApplyStatus;
            }
            else
            {
                _status.Text = PdfRedactionUiCopy.FormatAppliedStatus(
                    result.MarksApplied,
                    result.PagesChanged,
                    result.TextObjectsRemoved,
                    result.ImageObjectsRemoved,
                    result.AnnotationsRemoved,
                    result.AttachmentsRemoved,
                    result.MetadataCleared);
            }
        }
        catch (Exception ex)
        {
            _status.Text = PdfRedactionUiCopy.FormatApplyFailed(ex.Message);
        }
    }

    private async Task AddStickyNoteAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.NoteDialog);

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            PlaceholderText = PdfDialogPlaceholders.NoteText,
        };
        await SeedTextBoxFromClipboardAsync(box);
        var authorBox = new TextBox
        {
            Text = _annotationAuthor,
            PlaceholderText = PdfDialogPlaceholders.Author,
            Width = 220,
        };
        var colorList = new ListView
        {
            Height = 140,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = PdfAnnotationColor.StickyNotePresets.Select(p => p.Name).ToList(),
            SelectedIndex = Math.Max(0, PdfAnnotationColor.StickyNotePresets
                .ToList()
                .FindIndex(p => string.Equals(p.Name, ResolveDefaultStickyNoteColorName(), StringComparison.OrdinalIgnoreCase))),
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                box,
                new TextBlock { Text = PdfViewerTextLabels.Author, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                authorBox,
                new TextBlock { Text = PdfViewerTextLabels.Color, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                colorList,
            },
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.StickyNote,
            Content = panel,
            PrimaryButtonText = DialogButtons.Add,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _status.Text = StickyNoteExpandPolicy.NoteCancelled;
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
            _status.Text = StickyNoteExpandPolicy.Adding;
            var created = await _annotations.AddStickyNoteAsync(
                _document,
                CurrentPageIndex,
                x,
                y,
                box.Text ?? string.Empty,
                color,
                author: _annotationAuthor);
            RememberAnnotationForUndo(created);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = StickyNoteExpandPolicy.Added;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Note, ex.Message);
        }
    }

    private async Task AddTextBoxAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.TextBoxDialog);

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            PlaceholderText = PdfDialogPlaceholders.TextBoxContents,
        };
        await SeedTextBoxFromClipboardAsync(box);
        var fontSizeBox = new NumberBox
        {
            Header = PdfDialogHeaders.FontSizePt,
            Value = 12,
            Minimum = 6,
            Maximum = 72,
            SmallChange = 1,
            LargeChange = 2,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var fontFamilyBox = new ComboBox
        {
            Header = PdfDialogHeaders.Font,
            ItemsSource = PdfDialogOptions.StandardFonts.ToList(),
            SelectedIndex = 0,
            Width = 220,
        };
        var boldCheck = new CheckBox { Content = PdfViewerChromeLabels.Bold, IsChecked = false };
        var italicCheck = new CheckBox { Content = PdfViewerChromeLabels.Italic, IsChecked = false };
        var underlineCheck = new CheckBox { Content = PdfViewerChromeLabels.Underline, IsChecked = false };
        var alignBox = new ComboBox
        {
            Header = PdfDialogHeaders.Align,
            ItemsSource = PdfDialogOptions.HorizontalAlignments.ToList(),
            SelectedIndex = 0,
            Width = 220,
        };
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
            ItemsSource = PdfDialogOptions.NoteColors.ToList(),
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
                alignBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { boldCheck, italicCheck, underlineCheck },
                },
                new TextBlock { Text = PdfViewerTextLabels.TextColor, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                textColorList,
                new TextBlock { Text = PdfViewerTextLabels.Fill, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                fillList,
                new TextBlock { Text = PdfViewerTextLabels.Border, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                borderList,
            },
        };
        var dialog = new ContentDialog
        {
            Title = PdfTextBoxDialogStatus.Title,
            Content = panel,
            PrimaryButtonText = DialogButtons.Add,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _status.Text = PdfTextBoxDialogStatus.Cancelled;
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
        var quadding = (PdfTextQuadding)Math.Clamp(alignBox.SelectedIndex, 0, 2);

        var page = _document.GetPage(CurrentPageIndex);
        var width = Math.Min(240, page.WidthPoints * 0.45);
        var height = 72;
        var left = Math.Max(24, (page.WidthPoints - width) / 2);
        var bottom = Math.Max(24, (page.HeightPoints - height) / 2);
        var bounds = new PdfRect(left, bottom, left + width, bottom + height);

        try
        {
            _status.Text = PdfTextBoxDialogStatus.Adding;
            var created = await _annotations.AddTextBoxAsync(
                _document,
                CurrentPageIndex,
                bounds,
                box.Text ?? string.Empty,
                textColor,
                borderColor: border,
                fillColor: fill,
                fontSizePoints: fontSize,
                fontResourceName: fontResource,
                underline: underlineCheck.IsChecked == true,
                quadding: quadding);
            RememberAnnotationForUndo(created);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = PdfTextBoxDialogStatus.Added;
        }
        catch (Exception ex)
        {
            _status.Text = PdfTextBoxDialogStatus.Failed(ex.Message);
        }
    }

    private async void AnnotationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressAnnotationNav)
        {
            return;
        }

        if (_annotationList.SelectedItems.Count == 0)
        {
            return;
        }

        _selectedAnnots.Clear();
        foreach (var selected in _annotationList.SelectedItems)
        {
            if (selected is not string label)
            {
                continue;
            }

            var index = _annotationItems.ToList().FindIndex(a => PdfAnnotationListLabel.Format(a) == label);
            if (index >= 0)
            {
                _selectedAnnots.Add(_annotationItems[index]);
            }
        }

        if (_selectedAnnots.Count == 0)
        {
            return;
        }

        // Prefer the last SelectedIndex as primary when available.
        var primaryIndex = _annotationList.SelectedIndex;
        _selectedAnnot = primaryIndex >= 0 && primaryIndex < _annotationItems.Count
            ? _annotationItems[primaryIndex]
            : _selectedAnnots[^1];
        DrawAnnotSelection(_selectedAnnot);
        if (_selectedAnnot.IsStickyNote)
        {
            ExpandStickyNote(_selectedAnnot);
        }

        await GoToPageAsync(_selectedAnnot.PageIndex, recordHistory: true);
        _status.Text = _selectedAnnots.Count == 1
            ? AnnotationJumpStatus.JumpedTo(PdfAnnotationListLabel.Format(_selectedAnnot))
            : AnnotationMultiSelectPolicy.StatusAfterSelect(_selectedAnnots.Count);
    }


    private void ToggleAnnotInSelection(PdfAnnotationInfo hit)
    {
        var existing = _selectedAnnots.FindIndex(a => PdfAnnotationHitTest.SameIdentity(a, hit));
        if (existing >= 0)
        {
            _selectedAnnots.RemoveAt(existing);
            if (_selectedAnnot is not null && PdfAnnotationHitTest.SameIdentity(_selectedAnnot, hit))
            {
                _selectedAnnot = _selectedAnnots.Count > 0 ? _selectedAnnots[^1] : null;
            }
        }
        else
        {
            _selectedAnnots.Add(hit);
            _selectedAnnot = hit;
        }

        SyncSidebarSelectionMulti();
        if (_selectedAnnot is not null)
        {
            DrawAnnotSelection(_selectedAnnot);
            if (_selectedAnnot.IsStickyNote)
            {
                ExpandStickyNote(_selectedAnnot);
            }

            _status.Text = _selectedAnnots.Count == 1
                ? AnnotationMutationStatus.FormatSelectedAnnotation(PdfAnnotationListLabel.Format(_selectedAnnot))
                : AnnotationMultiSelectPolicy.StatusAfterToggle(_selectedAnnots.Count);
        }
        else
        {
            ClearAnnotSelectionVisual();
            _status.Text = AnnotationMultiSelectPolicy.StatusAfterToggle(0);
        }
    }

    private void ReplaceAnnotSelection(PdfAnnotationInfo hit)
    {
        _selectedAnnots.Clear();
        _selectedAnnots.Add(hit);
        _selectedAnnot = hit;
    }

    private void BeginAnnotDrag(
        Border border,
        PdfAnnotationInfo hit,
        Windows.Foundation.Point uiPoint,
        PointerRoutedEventArgs e)
    {
        var alreadyInSelection = _selectedAnnots.Exists(a => PdfAnnotationHitTest.SameIdentity(a, hit));
        if (!alreadyInSelection)
        {
            // Selecting one member of a group selects the whole group on that page.
            if (!string.IsNullOrEmpty(hit.GroupId))
            {
                var members = _annotationItems
                    .Where(a => a.PageIndex == hit.PageIndex
                        && string.Equals(a.GroupId, hit.GroupId, StringComparison.Ordinal))
                    .ToList();
                _selectedAnnots.Clear();
                _selectedAnnots.AddRange(members.Count > 0 ? members : [hit]);
                _selectedAnnot = hit;
            }
            else
            {
                ReplaceAnnotSelection(hit);
            }
        }
        else
        {
            _selectedAnnot = hit;
        }

        _annotDragging = true;
        _annotResizeHandle = null;
        _annotDragOriginBounds = hit.Bounds;
        _annotDragOriginUi = uiPoint;
        _multiDragOrigins.Clear();
        foreach (var a in _selectedAnnots)
        {
            _multiDragOrigins.Add((a, a.Bounds));
        }

        border.CapturePointer(e.Pointer);
        SyncSidebarSelectionMulti();
        DrawAnnotSelection(hit);
        if (hit.IsStickyNote)
        {
            ExpandStickyNote(hit);
        }

        _status.Text = _selectedAnnots.Count > 1
            ? AnnotationMutationStatus.FormatSelectedAnnotationsMoveTogether(_selectedAnnots.Count)
            : AnnotationMutationStatus.FormatSelectedAnnotationDrag(PdfAnnotationListLabel.Format(hit));
    }

    private async Task GroupSelectedAnnotationsAsync()
    {
        var members = _selectedAnnots.Count > 0
            ? _selectedAnnots.ToList()
            : TryGetSelectedAnnotation(out var one) ? [one] : [];
        if (members.Count < 2)
        {
            _status.Text = AnnotationSelectionStatus.GroupAtLeastTwo;
            return;
        }

        if (members.Select(m => m.PageIndex).Distinct().Count() > 1)
        {
            _status.Text = AnnotationGroupStatus.SamePageOnly;
            return;
        }

        var groupId = Guid.NewGuid().ToString("N");
        try
        {
            await _annotations.SetGroupAsync(
                _document,
                members.Select(m => (m.PageIndex, m.AnnotIndex)).ToList(),
                groupId);
            await RefreshAnnotationSidebarAsync();
            RestoreSelectionAfterRefresh(members[0].PageIndex, members[0].AnnotIndex);
            _status.Text = AnnotationGroupStatus.FormatGrouped(members.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Group, ex.Message);
        }
    }

    private async Task UngroupSelectedAnnotationsAsync()
    {
        var members = _selectedAnnots.Count > 0
            ? _selectedAnnots.ToList()
            : TryGetSelectedAnnotation(out var one) ? [one] : [];
        if (members.Count == 0)
        {
            _status.Text = AnnotationSelectionStatus.Ungroup;
            return;
        }

        // Expand to full group if any member has a group id.
        var groupIds = members
            .Where(m => !string.IsNullOrEmpty(m.GroupId))
            .Select(m => m.GroupId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (groupIds.Count == 0)
        {
            _status.Text = AnnotationGroupStatus.SelectionNotGrouped;
            return;
        }

        var toClear = _annotationItems
            .Where(a => a.GroupId is not null && groupIds.Contains(a.GroupId))
            .Select(a => (a.PageIndex, a.AnnotIndex))
            .ToList();
        if (toClear.Count == 0)
        {
            toClear = members.Select(m => (m.PageIndex, m.AnnotIndex)).ToList();
        }

        try
        {
            await _annotations.SetGroupAsync(_document, toClear, groupId: null);
            await RefreshAnnotationSidebarAsync();
            if (members.Count > 0)
            {
                RestoreSelectionAfterRefresh(members[0].PageIndex, members[0].AnnotIndex);
            }

            _status.Text = AnnotationGroupStatus.FormatUngrouped(toClear.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Ungroup, ex.Message);
        }
    }

    private void BeginAnnotResize(
        Border border,
        PdfAnnotationInfo hit,
        string handle,
        Windows.Foundation.Point uiPoint,
        PointerRoutedEventArgs e)
    {
        if (!_selectedAnnots.Exists(a => PdfAnnotationHitTest.SameIdentity(a, hit)))
        {
            ReplaceAnnotSelection(hit);
        }
        else
        {
            _selectedAnnot = hit;
        }

        _annotDragging = true;
        _annotResizeHandle = handle;
        _annotDragOriginBounds = hit.Bounds;
        _annotDragOriginEndpointA = hit.EndpointA ?? new PdfPagePoint(hit.Bounds.Left, hit.Bounds.Bottom);
        _annotDragOriginEndpointB = hit.EndpointB ?? new PdfPagePoint(hit.Bounds.Right, hit.Bounds.Top);
        _annotDragOriginUi = uiPoint;
        _multiDragOrigins.Clear();
        border.CapturePointer(e.Pointer);
        SyncSidebarSelectionMulti();
        DrawAnnotSelection(hit);
        _status.Text = PdfAnnotationResize.IsEndpointHandle(handle)
            ? AnnotationMutationStatus.FormatAdjustingEndpoint(PdfAnnotationListLabel.Format(hit))
            : AnnotationMutationStatus.FormatResizing(PdfAnnotationListLabel.Format(hit));
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
        if (_multiDragOrigins.Count > 1)
        {
            DrawMultiAnnotSelectionPreview(dx, dy);
            return;
        }

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
        if (PdfAnnotationResize.IsEndpointHandle(_annotResizeHandle)
            && _selectedAnnot.UsesEndpointHandles)
        {
            var (a, b) = PdfAnnotationResize.ComputeEndpoints(
                _annotDragOriginEndpointA,
                _annotDragOriginEndpointB,
                _annotResizeHandle,
                dx,
                dy);
            var previewBounds = new PdfRect(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y));
            DrawAnnotSelection(_selectedAnnot with
            {
                Bounds = previewBounds,
                EndpointA = a,
                EndpointB = b,
            });
            return;
        }

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
            if (_multiDragOrigins.Count > 1)
            {
                _status.Text = AnnotationPersistStatus.FormatMovingAnnotations(_multiDragOrigins.Count);
                foreach (var (info, origin) in _multiDragOrigins
                             .Where(o => o.Info.PageIndex == _selectedAnnot.PageIndex)
                             .OrderByDescending(o => o.Info.AnnotIndex))
                {
                    var target = new PdfRect(
                        origin.Left + dx,
                        origin.Bottom + dy,
                        origin.Right + dx,
                        origin.Top + dy);
                    await _annotations.MoveAsync(_document, info.PageIndex, info.AnnotIndex, target);
                }
            }
            else
            {
                _status.Text = AnnotationPersistStatus.MovingAnnotation;
                await _annotations.MoveAsync(
                    _document,
                    _selectedAnnot.PageIndex,
                    _selectedAnnot.AnnotIndex,
                    moved);
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            RestoreSelectionAfterRefresh(_selectedAnnot.PageIndex, _selectedAnnot.AnnotIndex, moved);
            _status.Text = _selectedAnnots.Count > 1
                ? AnnotationMutationStatus.FormatMovedAnnotations(_selectedAnnots.Count)
                : AnnotationMutationStatus.AnnotationMoved;
        }
        catch (Exception ex)
        {
            DrawAnnotSelection(_selectedAnnot);
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Move, ex.Message);
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
        if (PdfAnnotationResize.IsEndpointHandle(handle) && _selectedAnnot.UsesEndpointHandles)
        {
            var (a, b) = PdfAnnotationResize.ComputeEndpoints(
                _annotDragOriginEndpointA,
                _annotDragOriginEndpointB,
                handle,
                dx,
                dy);
            try
            {
                _status.Text = AnnotationGroupStatus.UpdatingLineEndpoints;
                var updated = await _annotations.SetLineEndpointsAsync(
                    _document,
                    _selectedAnnot.PageIndex,
                    _selectedAnnot.AnnotIndex,
                    a,
                    b);
                _cache.ClearDocument(_documentKey);
                _cache.ClearDocument(_thumbnailKey);
                await RenderVisibleAsync();
                await RenderThumbnailsAsync();
                await RefreshAnnotationSidebarAsync();
                RestoreSelectionAfterRefresh(updated.PageIndex, updated.AnnotIndex, updated.Bounds);
                _status.Text = AnnotationGroupStatus.LineEndpointsUpdated;
            }
            catch (Exception ex)
            {
                DrawAnnotSelection(_selectedAnnot);
                _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.EndpointAdjust, ex.Message);
            }

            return;
        }

        var resized = PdfAnnotationResize.ComputeBounds(_annotDragOriginBounds, handle, dx, dy);

        try
        {
            _status.Text = AnnotationGroupStatus.Resizing;
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
            RestoreSelectionAfterRefresh(_selectedAnnot.PageIndex, _selectedAnnot.AnnotIndex, resized);
            _status.Text = AnnotationMutationStatus.Resized;
        }
        catch (Exception ex)
        {
            DrawAnnotSelection(_selectedAnnot);
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Resize, ex.Message);
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
        if (info.UsesEndpointHandles
            && info.EndpointA is { } a
            && info.EndpointB is { } b)
        {
            yield return ("p0", a.X * _scale, (page.HeightPoints - a.Y) * _scale);
            yield return ("p1", b.X * _scale, (page.HeightPoints - b.Y) * _scale);
            yield break;
        }

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

    private void RestoreSelectionAfterRefresh(int primaryPage, int primaryIndex, PdfRect? primaryBounds = null)
    {
        var keys = _selectedAnnots
            .Select(a => (a.PageIndex, a.AnnotIndex))
            .ToList();
        if (keys.Count == 0)
        {
            keys.Add((primaryPage, primaryIndex));
        }

        _selectedAnnots.Clear();
        foreach (var (page, index) in keys)
        {
            var live = _annotationItems.FirstOrDefault(a => a.PageIndex == page && a.AnnotIndex == index);
            if (live is not null)
            {
                _selectedAnnots.Add(live);
            }
        }

        _selectedAnnot = _selectedAnnots.FirstOrDefault(a => a.PageIndex == primaryPage && a.AnnotIndex == primaryIndex)
            ?? (_selectedAnnots.Count > 0 ? _selectedAnnots[^1] : null);
        if (_selectedAnnot is null && primaryBounds is { } bounds)
        {
            _selectedAnnot = new PdfAnnotationInfo(primaryPage, primaryIndex, null, bounds, null);
            _selectedAnnots.Add(_selectedAnnot);
        }

        if (_selectedAnnot is not null)
        {
            SyncSidebarSelectionMulti();
            DrawAnnotSelection(_selectedAnnot);
        }
        else
        {
            ClearAnnotSelectionVisual();
        }
    }

    private void SyncSidebarSelection(PdfAnnotationInfo info)
    {
        ReplaceAnnotSelection(info);
        SyncSidebarSelectionMulti();
    }

    private void SyncSidebarSelectionMulti()
    {
        _suppressAnnotationNav = true;
        try
        {
            _annotationList.SelectedItems.Clear();
            foreach (var a in _selectedAnnots)
            {
                var index = _annotationItems.ToList().FindIndex(x => PdfAnnotationHitTest.SameIdentity(x, a));
                if (index >= 0 && index < _annotationList.Items.Count)
                {
                    _annotationList.SelectedItems.Add(_annotationList.Items[index]);
                }
            }

            if (_selectedAnnot is not null)
            {
                var primary = _annotationItems.ToList().FindIndex(x => PdfAnnotationHitTest.SameIdentity(x, _selectedAnnot));
                if (primary >= 0)
                {
                    _annotationList.SelectedIndex = primary;
                }
            }
        }
        finally
        {
            _suppressAnnotationNav = false;
        }
    }

    private void DrawMultiAnnotSelectionPreview(double dxPoints, double dyPoints)
    {
        ClearAnnotSelectionVisual();
        foreach (var (info, origin) in _multiDragOrigins)
        {
            var preview = info with
            {
                Bounds = new PdfRect(
                    origin.Left + dxPoints,
                    origin.Bottom + dyPoints,
                    origin.Right + dxPoints,
                    origin.Top + dyPoints),
            };
            AddAnnotSelectionChrome(preview, primary: _selectedAnnot is not null && PdfAnnotationHitTest.SameIdentity(info, _selectedAnnot));
        }
    }

    private void DrawAnnotSelection(PdfAnnotationInfo info)
    {
        ClearAnnotSelectionVisual();
        if (_selectedAnnots.Count == 0)
        {
            ReplaceAnnotSelection(info);
        }

        var drawn = false;
        foreach (var a in _selectedAnnots)
        {
            var boundsInfo = PdfAnnotationHitTest.SameIdentity(a, info) ? info : a;
            AddAnnotSelectionChrome(boundsInfo, primary: _selectedAnnot is not null && PdfAnnotationHitTest.SameIdentity(a, _selectedAnnot));
            drawn = true;
        }

        if (!drawn)
        {
            AddAnnotSelectionChrome(info, primary: true);
        }

        if (info.ShapeKind == PdfShapeKind.Loupe)
        {
            ShowLoupeMagnifier(info);
        }
    }

    private void AddAnnotSelectionChrome(PdfAnnotationInfo info, bool primary)
    {
        if (!_pageOverlays.TryGetValue(info.PageIndex, out var overlay))
        {
            return;
        }

        var page = _document.GetPage(info.PageIndex);
        var left = info.Bounds.Left * _scale;
        var top = (page.HeightPoints - info.Bounds.Top) * _scale;
        var width = Math.Max(4, info.Bounds.Width * _scale);
        var height = Math.Max(4, info.Bounds.Height * _scale);
        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Stroke = new SolidColorBrush(primary ? Colors.DodgerBlue : Colors.CornflowerBlue),
            StrokeThickness = primary ? 2 : 1.5,
            StrokeDashArray = primary ? null : new DoubleCollection { 3, 2 },
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(primary ? (byte)40 : (byte)24, 30, 144, 255)),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        overlay.Children.Add(rect);
        _annotSelectionVisuals.Add(rect);
        if (primary)
        {
            _annotSelectionRect = rect;
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
    }

    private void ClearAnnotSelectionVisual()
    {
        ClearLoupeMagnifier();
        foreach (var visual in _annotSelectionVisuals)
        {
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Remove(visual);
            }
        }

        foreach (var handle in _annotResizeHandleVisuals)
        {
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Remove(handle);
            }
        }

        _annotSelectionVisuals.Clear();
        _annotSelectionRect = null;
        _annotResizeHandleVisuals.Clear();
    }

    private void ClearLoupeMagnifier()
    {
        foreach (var visual in _loupePopupVisuals)
        {
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Remove(visual);
            }
        }

        _loupePopupVisuals.Clear();
    }

    private void ShowLoupeMagnifier(PdfAnnotationInfo loupe)
    {
        ClearLoupeMagnifier();
        if (loupe.ShapeKind != PdfShapeKind.Loupe)
        {
            return;
        }

        if (!_pageOverlays.TryGetValue(loupe.PageIndex, out var overlay)
            || !_pageImages.TryGetValue(loupe.PageIndex, out var pageImage)
            || pageImage.Source is not WriteableBitmap bitmap)
        {
            return;
        }

        var page = _document.GetPage(loupe.PageIndex);
        var outSize = (int)PdfLoupeMagnifier.PopupSizeDip;
        var magnified = PdfLoupeMagnifier.CropAndScale(
            bitmap,
            page.WidthPoints,
            page.HeightPoints,
            loupe.Bounds.Left,
            loupe.Bounds.Bottom,
            loupe.Bounds.Right,
            loupe.Bounds.Top,
            PdfLoupeMagnifier.DefaultZoom,
            outSize);
        if (magnified is null)
        {
            return;
        }

        var ring = new Border
        {
            Width = outSize + 4,
            Height = outSize + 4,
            CornerRadius = new CornerRadius((outSize + 4) / 2.0),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 100, 180)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Colors.White),
            IsHitTestVisible = false,
            Child = new Image
            {
                Source = magnified,
                Width = outSize,
                Height = outSize,
                Stretch = Stretch.UniformToFill,
            },
        };

        // Place popup to the right of the loupe circle (or left if near page edge).
        var loupeRight = loupe.Bounds.Right * _scale;
        var loupeTop = (page.HeightPoints - loupe.Bounds.Top) * _scale;
        var pageDisplayWidth = page.WidthPoints * _scale;
        var popupLeft = loupeRight + 12;
        if (popupLeft + outSize + 4 > pageDisplayWidth)
        {
            popupLeft = loupe.Bounds.Left * _scale - outSize - 16;
        }

        popupLeft = Math.Max(0, popupLeft);
        var popupTop = Math.Max(0, loupeTop - 8);
        Canvas.SetLeft(ring, popupLeft);
        Canvas.SetTop(ring, popupTop);
        overlay.Children.Add(ring);
        _loupePopupVisuals.Add(ring);
    }

    private void ExpandSelectedStickyNote()
    {
        if (!TryGetSelectedAnnotation(out var item) || !item.IsStickyNote)
        {
            _status.Text = StickyNoteExpandPolicy.SelectToExpand;
            return;
        }

        ExpandStickyNote(item);
        _status.Text = StickyNoteExpandPolicy.Expanded(PdfAnnotationListLabel.Format(item));
    }

    private void CollapseSelectedStickyNote()
    {
        var hasSelectedSticky = TryGetSelectedAnnotation(out var item) && item.IsStickyNote;
        if (!hasSelectedSticky)
        {
            if (!StickyNoteExpandPolicy.ShouldCollapseAll(false, _expandedStickyNotes.Count))
            {
                _status.Text = StickyNoteExpandPolicy.NoExpandedNotes;
                return;
            }

            _expandedStickyNotes.Clear();
            RedrawStickyNotePopups();
            _status.Text = StickyNoteExpandPolicy.CollapsedAll;
            return;
        }

        var key = (item.PageIndex, item.AnnotIndex);
        if (!_expandedStickyNotes.Remove(key))
        {
            _status.Text = StickyNoteExpandPolicy.AlreadyCollapsed;
            return;
        }

        RedrawStickyNotePopups();
        _status.Text = StickyNoteExpandPolicy.Collapsed(PdfAnnotationListLabel.Format(item));
    }

    private async Task ExportNotesAsync()
    {
        IReadOnlyList<PdfAnnotationInfo> annotations;
        try
        {
            annotations = await _annotations.ListAsync(_document);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.CouldNotListNotes, ex.Message);
            return;
        }

        var noteCount = annotations.Count(a => a.IsStickyNote);
        if (noteCount == 0)
        {
            _status.Text = StickyNoteExpandPolicy.NoNotesToExport;
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
        var baseName = _document.Path is null
            ? "Glyph"
            : System.IO.Path.GetFileNameWithoutExtension(_document.Path);
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = $"{baseName}-notes";
        picker.FileTypeChoices.Add("Text", [".txt"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            _status.Text = StickyNoteExpandPolicy.ExportCancelled;
            return;
        }

        var title = _document.Path is null
            ? baseName
            : System.IO.Path.GetFileName(_document.Path);
        var text = PdfNotesExport.Format(annotations, documentTitle: title);
        await Windows.Storage.FileIO.WriteTextAsync(file, text);
        _status.Text = StickyNoteExpandPolicy.FormatExported(noteCount, file.Name);
    }

    private async Task RotateSelectedAnnotationAsync()
    {
        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = AnnotationSelectionStatus.Rotate;
            return;
        }

        if (item.IsStickyNote || item.TextMarkupKind is not null)
        {
            _status.Text = StickyNoteExpandPolicy.CannotRotateMarkup;
            return;
        }

        try
        {
            _status.Text = AnnotationGroupStatus.Rotating;
            var updated = await _annotations.RotateAsync(
                _document,
                item.PageIndex,
                item.AnnotIndex,
                degreesClockwise: 90);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            RestoreSelectionAfterRefresh(updated.PageIndex, updated.AnnotIndex, updated.Bounds);
            _status.Text = AnnotationMutationStatus.Rotated90;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Rotate, ex.Message);
        }
    }

    private async Task ToggleSelectedTextUnderlineAsync()
    {
        if (!TryGetSelectedAnnotation(out var item) || !item.IsTextBox)
        {
            _status.Text = AnnotationSelectionStatus.UnderlineTextBoxOrCallout;
            return;
        }

        try
        {
            var updated = await _annotations.SetUnderlineAsync(
                _document,
                item.PageIndex,
                item.AnnotIndex,
                underline: !item.IsUnderlined);
            _selectedAnnot = updated;
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = updated.IsUnderlined ? AnnotationMutationStatus.UnderlineOn : AnnotationMutationStatus.UnderlineOff;
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Underline, ex.Message);
        }
    }

    private async Task SetSelectedTextQuaddingAsync()
    {
        if (!TryGetSelectedAnnotation(out var item) || !item.IsTextBox)
        {
            _status.Text = AnnotationSelectionStatus.AlignmentTextBoxOrCallout;
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.AlignmentDialog);

        var alignBox = new ComboBox
        {
            Header = PdfDialogHeaders.Align,
            ItemsSource = PdfDialogOptions.HorizontalAlignments.ToList(),
            SelectedIndex = Math.Clamp((int)(item.TextQuadding ?? PdfTextQuadding.Left), 0, 2),
            Width = 220,
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.TextAlignment,
            Content = alignBox,
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var quadding = (PdfTextQuadding)Math.Clamp(alignBox.SelectedIndex, 0, 2);
        try
        {
            var updated = await _annotations.SetTextQuaddingAsync(
                _document,
                item.PageIndex,
                item.AnnotIndex,
                quadding);
            _selectedAnnot = updated;
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            RestoreSelectionAfterRefresh(updated.PageIndex, updated.AnnotIndex, updated.Bounds);
            _status.Text = AnnotationGroupStatus.FormatAlignment(quadding.ToString());
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Alignment, ex.Message);
        }
    }

    private void ExpandStickyNote(PdfAnnotationInfo note)
    {
        if (!note.IsStickyNote)
        {
            return;
        }

        _expandedStickyNotes.Add((note.PageIndex, note.AnnotIndex));
        RedrawStickyNotePopups();
    }

    private void RedrawStickyNotePopups()
    {
        foreach (var visual in _stickyNotePopupVisuals)
        {
            foreach (var overlay in _pageOverlays.Values)
            {
                overlay.Children.Remove(visual);
            }
        }

        _stickyNotePopupVisuals.Clear();
        if (_expandedStickyNotes.Count == 0)
        {
            return;
        }

        foreach (var key in _expandedStickyNotes.ToList())
        {
            var note = _annotationItems.FirstOrDefault(a =>
                a.IsStickyNote && a.PageIndex == key.PageIndex && a.AnnotIndex == key.AnnotIndex);
            if (note is null)
            {
                _expandedStickyNotes.Remove(key);
                continue;
            }

            if (!_pageOverlays.TryGetValue(note.PageIndex, out var overlay))
            {
                continue;
            }

            var page = _document.GetPage(note.PageIndex);
            var left = note.Bounds.Right * _scale + 8;
            var top = (page.HeightPoints - note.Bounds.Top) * _scale;
            var author = string.IsNullOrWhiteSpace(note.Author) ? null : note.Author;
            var body = string.IsNullOrWhiteSpace(note.Contents) ? "(empty note)" : note.Contents!;
            var fill = note.Color is { } c
                ? Windows.UI.Color.FromArgb(230, c.R, c.G, c.B)
                : Windows.UI.Color.FromArgb(230, 255, 240, 150);

            var panel = new Border
            {
                Width = Math.Clamp(180 * _scale / 1.25, 140, 280),
                Background = new SolidColorBrush(fill),
                BorderBrush = new SolidColorBrush(Colors.DimGray),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = author ?? "Note",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = body,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            MaxHeight = 160,
                        },
                    },
                },
            };
            Canvas.SetLeft(panel, left);
            Canvas.SetTop(panel, top);
            overlay.Children.Add(panel);
            _stickyNotePopupVisuals.Add(panel);
        }
    }

    private async Task ConfigureAnnotationAuthorAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.AuthorDialog);

        var box = new TextBox
        {
            Text = _annotationAuthor,
            PlaceholderText = PdfDialogPlaceholders.AuthorName,
            Width = 260,
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.AnnotationAuthor,
            Content = box,
            PrimaryButtonText = DialogButtons.Save,
            CloseButtonText = DialogButtons.Cancel,
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
        try
        {
            var store = App.Services.GetService<ISettingsStore>();
            if (store is not null)
            {
                var settings = store.Current;
                settings.AnnotationAuthor = _annotationAuthor;
                await store.SaveAsync(settings);
            }
        }
        catch
        {
            // Preference persist is best-effort.
        }

        _status.Text = AnnotationMutationStatus.FormatAuthorSet(_annotationAuthor);
    }

    private static AppSettings? TryGetSettings()
    {
        try
        {
            return App.Services.GetService<ISettingsStore>()?.Current;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyAnnotationDefaults(AppSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        var highlight = PdfAnnotationColor.HighlightPresets
            .FirstOrDefault(p => string.Equals(p.Name, settings.DefaultHighlightColor, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(highlight.Name))
        {
            _highlightModeColor = highlight.Color;
        }

        var stroke = PdfAnnotationColor.StrokePresets
            .FirstOrDefault(p => string.Equals(p.Name, settings.DefaultStrokeColor, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(stroke.Name))
        {
            _drawStrokeColor = stroke.Color;
        }

        _drawStrokeWidth = (float)Math.Clamp(settings.DefaultStrokeWidthPoints, 0.5, 12);
    }

    private UIElement BuildPagesHeader()
    {
        var small = new Button { Content = PdfViewerChromeLabels.SizeSmall, Width = 28, Padding = new Thickness(0), Tag = ThumbnailWidthConstraints.Min };
        var medium = new Button { Content = PdfViewerChromeLabels.SizeMedium, Width = 28, Padding = new Thickness(0), Tag = ThumbnailWidthConstraints.Default };
        var large = new Button { Content = PdfViewerChromeLabels.SizeLarge, Width = 28, Padding = new Thickness(0), Tag = 156.0 };
        ToolTipService.SetToolTip(small, PdfViewerTooltips.SmallPageThumbnails);
        ToolTipService.SetToolTip(medium, PdfViewerTooltips.MediumPageThumbnails);
        ToolTipService.SetToolTip(large, PdfViewerTooltips.LargePageThumbnails);
        AutomationProperties.SetName(small, PdfViewerAutomationNames.SmallPageThumbnails);
        AutomationProperties.SetName(medium, PdfViewerAutomationNames.MediumPageThumbnails);
        AutomationProperties.SetName(large, PdfViewerAutomationNames.LargePageThumbnails);

        async void OnSizeClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: double width })
            {
                await SetThumbnailWidthAsync(width);
            }
        }

        small.Click += OnSizeClick;
        medium.Click += OnSizeClick;
        large.Click += OnSizeClick;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 8, 8, 4),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.Pages,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                small,
                medium,
                large,
            },
        };
    }

    private void ApplySidebarMode()
    {
        var selected = SidebarModeCombo.ClampIndex(_sidebarModeBox?.SelectedIndex ?? 0);
        _viewState.SidebarMode = SidebarModeCombo.ToSidebarMode(selected);
        for (var i = 0; i < _sidebarSections.Count; i++)
        {
            _sidebarSections[i].Visibility = i == selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ShowSidebarMode(int modeIndex)
    {
        if (_sidebarModeBox is null || !SidebarModeCombo.IsValidIndex(modeIndex))
        {
            return;
        }

        if (_sidebarModeBox.SelectedIndex != modeIndex)
        {
            _sidebarModeBox.SelectedIndex = modeIndex;
        }
        else
        {
            ApplySidebarMode();
        }
    }

    private async Task SetThumbnailWidthAsync(double width)
    {
        var clamped = ThumbnailWidthConstraints.Clamp(width);
        if (Math.Abs(clamped - _thumbnailWidth) < 0.5)
        {
            return;
        }

        _thumbnailWidth = clamped;
        try
        {
            var settings = TryGetSettings();
            if (settings is not null)
            {
                settings.ThumbnailWidth = clamped;
                var store = App.Services.GetService<ISettingsStore>();
                if (store is not null)
                {
                    await store.SaveAsync(settings);
                }
            }
        }
        catch
        {
            // Persistence is best-effort.
        }

        _cache.ClearDocument(_thumbnailKey);
        if (_sidePanel is not null)
        {
            _sidePanel.Width = Math.Max(180, _thumbnailWidth + 48);
        }

        BuildThumbnailPlaceholders();
        RefreshThumbnailSelectionChrome();
        _ = RenderThumbnailsAsync();
        _status.Text = AnnotationGroupStatus.FormatThumbnailSize(_thumbnailWidth);
    }

    private string ResolveDefaultStickyNoteColorName()
    {
        var settings = TryGetSettings();
        var name = settings?.DefaultStickyNoteColor;
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Yellow";
        }

        return PdfAnnotationColor.StickyNotePresets.Any(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ? name
            : "Yellow";
    }

    private async Task EditSelectedAnnotationContentsAsync()
    {
        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = StickyNoteExpandPolicy.SelectToEdit;
            return;
        }

        if (!item.IsStickyNote && !item.IsTextBox && !item.IsCallout)
        {
            _status.Text = StickyNoteExpandPolicy.EditAppliesToNotes;
            return;
        }

        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.EditDialog);

        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 140,
            Text = item.Contents ?? string.Empty,
            PlaceholderText = item.IsStickyNote ? PdfDialogPlaceholders.NoteText : PdfDialogPlaceholders.TextContents,
        };
        await SeedTextBoxFromClipboardAsync(box);
        var title = item.IsCallout ? "Edit callout" : item.IsStickyNote ? "Edit sticky note" : "Edit text box";
        var dialog = new ContentDialog
        {
            Title = title,
            Content = box,
            PrimaryButtonText = DialogButtons.Save,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = AnnotationMutationStatus.EditCancelled;
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
            _status.Text = AnnotationMutationStatus.FormatUpdated(PdfAnnotationListLabel.Format(updated));
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.EditAnnotation, ex.Message);
        }
    }

    private async Task DuplicateSelectedAnnotationAsync()
    {
        var index = _annotationList.SelectedIndex;
        if (index < 0 || index >= _annotationItems.Count)
        {
            if (_selectedAnnot is null)
            {
                _status.Text = AnnotationSelectionStatus.Duplicate;
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
            _status.Text = AnnotationMutationStatus.FormatDuplicated(PdfAnnotationListLabel.Format(item));
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.DuplicateAnnotation, ex.Message);
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
            _status.Text = AnnotationClipboardPolicy.SelectToCopy;
            return;
        }

        _annotClipboard = (item.PageIndex, item.AnnotIndex);
        _annotClipboardIsCut = false;
        _status.Text = AnnotationClipboardPolicy.Copied(PdfAnnotationListLabel.Format(item));
    }

    private void CutSelectedAnnotationToClipboard()
    {
        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = AnnotationClipboardPolicy.SelectToCut;
            return;
        }

        _annotClipboard = (item.PageIndex, item.AnnotIndex);
        _annotClipboardIsCut = true;
        _status.Text = AnnotationClipboardPolicy.Cut(PdfAnnotationListLabel.Format(item));
    }

    private async Task PasteAnnotationClipboardAsync()
    {
        if (_annotClipboard is not { } clip)
        {
            _status.Text = AnnotationClipboardPolicy.EmptyClipboard;
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
                copyIndex = AnnotationClipboardPolicy.AdjustIndexAfterCutRemove(
                    clip.PageIndex,
                    clip.AnnotIndex,
                    copyPage,
                    copyIndex);

                if (AnnotationClipboardPolicy.ClearClipboardAfterPaste(wasCut))
                {
                    _annotClipboard = null;
                    _annotClipboardIsCut = false;
                }
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
            _status.Text = AnnotationClipboardPolicy.Pasted(PdfAnnotationListLabel.Format(pasted), wasCut);
        }
        catch (Exception ex)
        {
            _status.Text = AnnotationClipboardPolicy.PasteFailed(ex.Message);
        }
    }

    private async Task SetSelectedAnnotationBorderWidthAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.WidthDialog);

        if (!TryGetSelectedAnnotation(out var item))
        {
            _status.Text = AnnotationSelectionStatus.InkOrShapeWidth;
            return;
        }

        float initial = _drawStrokeWidth;
        try
        {
            var current = await _annotations.GetBorderWidthAsync(_document, item.PageIndex, item.AnnotIndex);
            if (current is > 0)
            {
                initial = current.Value;
            }
        }
        catch
        {
            // Use default when read fails.
        }

        var widthBox = new NumberBox
        {
            Header = PdfDialogHeaders.WidthPt,
            Value = initial,
            Minimum = 0.5,
            Maximum = 24,
            SmallChange = 0.5,
            LargeChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 280,
        };
        var dialog = new ContentDialog
        {
            Title = $"Stroke width — {PdfAnnotationListLabel.Format(item)}",
            Content = widthBox,
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var width = (float)(double.IsNaN(widthBox.Value) ? initial : Math.Clamp(widthBox.Value, 0.5, 24));

        try
        {
            await _annotations.SetBorderWidthAsync(_document, item.PageIndex, item.AnnotIndex, width);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = AnnotationGroupStatus.FormatStrokeWidth(width);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Width, ex.Message);
        }
    }

    private async Task SetSelectedAnnotationOpacityAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.OpacityDialog);

        var index = _annotationList.SelectedIndex;
        PdfAnnotationInfo? item = index >= 0 && index < _annotationItems.Count
            ? _annotationItems[index]
            : _selectedAnnot;
        if (item is null)
        {
            _status.Text = AnnotationSelectionStatus.Opacity;
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
            Title = $"Opacity — {PdfAnnotationListLabel.Format(item)}",
            Content = panel,
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
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
            _status.Text = AnnotationGroupStatus.FormatOpacity((int)slider.Value);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Opacity, ex.Message);
        }
    }

    private async Task RemoveSelectedAnnotationAsync()
    {
        var toRemove = _selectedAnnots.Count > 0
            ? _selectedAnnots.ToList()
            : TryGetSelectedAnnotation(out var one) ? [one] : [];
        if (toRemove.Count == 0)
        {
            _status.Text = AnnotationSelectionStatus.Delete;
            return;
        }

        try
        {
            foreach (var item in toRemove
                         .OrderByDescending(a => a.PageIndex)
                         .ThenByDescending(a => a.AnnotIndex))
            {
                await _annotations.RemoveAsync(_document, item.PageIndex, item.AnnotIndex);
            }

            ClearAnnotSelectionVisual();
            _selectedAnnot = null;
            _selectedAnnots.Clear();
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            await RefreshAnnotationSidebarAsync();
            _status.Text = toRemove.Count == 1
                ? AnnotationMutationStatus.FormatDeletedAnnotation(PdfAnnotationListLabel.Format(toRemove[0]))
                : AnnotationMutationStatus.FormatDeletedAnnotations(toRemove.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.DeleteAnnotation, ex.Message);
        }
    }

    private async Task RunPageEditAsync(Func<Task> mutation)
    {
        await _editHistory.ExecuteAsync(_document, _pageEditor, mutation);
        NotifyEdited();
    }

    /// <summary>True when page edits or other mutations have not been saved (F50).</summary>
    public bool HasUnsavedEdits => _hasUnsavedContent || _editHistory.CanUndo;

    public void NotifyEdited()
    {
        _hasUnsavedContent = true;
        _onEdited?.Invoke();
    }

    public void ClearUnsavedEdits()
    {
        _hasUnsavedContent = false;
        _editHistory.Clear();
    }

    /// <summary>Write current in-memory PDF bytes for crash recovery (F50-02).</summary>
    public async Task WriteRecoverySnapshotAsync(
        Glyph.Infrastructure.Session.ICrashRecoveryStore store,
        string originalPath,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _pageEditor.SaveToBytesAsync(_document, cancellationToken);
        await using var stream = new MemoryStream(bytes);
        await store.SaveSnapshotAsync(originalPath, stream, ".pdf", cancellationToken);
    }

    /// <summary>Heuristic: in-memory PDF length differs from on-disk file (catches annotations).</summary>
    public async Task<bool> DiffersFromDiskAsync(CancellationToken cancellationToken = default)
    {
        if (HasUnsavedEdits)
        {
            return true;
        }

        var path = _document.Path;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var bytes = await _pageEditor.SaveToBytesAsync(_document, cancellationToken);
            return new FileInfo(path).Length != bytes.Length;
        }
        catch (Exception)
        {
            return HasUnsavedEdits;
        }
    }

    private async Task UndoMostRecentAsync()
    {
        // Prefer pending redaction undo (F49-13), then stroke, then page-edit history.
        if (_redaction.GetPending(_document).Count > 0)
        {
            var removed = _redaction.UndoLastPending(_document);
            if (removed is not null)
            {
                RefreshPendingRedactionOverlay(removed.PageIndex);
                var remaining = _redaction.GetPending(_document).Count;
                _status.Text = PdfRedactionUiCopy.FormatUndidStatus(remaining);
                return;
            }
        }

        if (_strokeUndoStack.Count > 0)
        {
            await UndoLastStrokeAsync();
            return;
        }

        if (_formUndoStack.Count > 0)
        {
            await UndoLastFormFillAsync();
            return;
        }

        if (_infoUndoStack.Count > 0)
        {
            await UndoLastInfoEditAsync();
            return;
        }

        await UndoPageEditAsync();
    }

    private async Task UndoLastInfoEditAsync()
    {
        if (!_infoUndoStack.TryPop(out var previous))
        {
            return;
        }

        try
        {
            _documentInfo.SetInfo(_document, previous);
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            RefreshPropertiesSidebar();
            NotifyEdited();
            _status.Text = PdfDocumentInfoUi.UndidStatus;
        }
        catch (Exception ex)
        {
            _infoUndoStack.Push(previous);
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.UndoInfoEdit, ex.Message);
        }
    }

    private async Task UndoPageEditAsync()
    {
        if (!_editHistory.CanUndo)
        {
            _status.Text = DocumentUndoRedoStatus.NothingToUndo;
            return;
        }

        _status.Text = DocumentUndoRedoStatus.Undoing;
        await _editHistory.UndoAsync(_document, _pageEditor);
        await ReloadAfterPageEditAsync();
        _status.Text = PageEditStatus.UndidPageEdit;
    }

    private async Task RedoPageEditAsync()
    {
        if (!_editHistory.CanRedo)
        {
            _status.Text = DocumentUndoRedoStatus.NothingToRedo;
            return;
        }

        _status.Text = DocumentUndoRedoStatus.Redoing;
        await _editHistory.RedoAsync(_document, _pageEditor);
        await ReloadAfterPageEditAsync();
        _status.Text = PageEditStatus.RedidPageEdit;
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
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = DocumentExportFormats.SuggestedExtractedPages;
        picker.FileTypeChoices.Add("PDF", [".pdf"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            _status.Text = PageClipboardStatus.ExtractCancelled;
            return;
        }

        _status.Text = PageClipboardStatus.Extracting;
        await using var extracted = await _pageEditor.ExtractPagesAsync(_document, indexes);
        await _pageEditor.SaveAsync(extracted, file.Path);
        _status.Text = PageEditStatus.FormatExtracted(indexes.Count, file.Name);
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
        _status.Text = AnnotationToolModeStatus.CropOn;
    }

    private void EnsureCropChrome()
    {
        if (_cropChrome is not null)
        {
            _cropChrome.Visibility = Visibility.Visible;
            return;
        }

        var apply = new Button { Content = PdfViewerChromeLabels.ApplyCrop };
        var cancel = new Button { Content = PdfViewerChromeLabels.Cancel };
        var numeric = new Button { Content = PdfViewerChromeLabels.NumericEllipsis };
        var exportCropped = new Button { Content = PdfViewerChromeLabels.ExportCroppedEllipsis };
        apply.Click += async (_, _) => await ApplyCropModeAsync();
        cancel.Click += (_, _) => CancelCropMode();
        numeric.Click += async (_, _) => await CropNumericDialogAsync();
        exportCropped.Click += async (_, _) => await ExportCroppedAsync();
        ToolTipService.SetToolTip(exportCropped, PdfViewerTooltips.ExportSelectedPagesWithAPermanent);
        _cropChrome = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Padding = new Thickness(8, 0, 8, 8),
            Children =
            {
                new TextBlock
                {
                    Text = PdfViewerTextLabels.CropHandles,
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

        _status.Text = PageEditStatus.CropCancelled;
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
            _status.Text = PageEditStatus.Cropping;
            await RunPageEditAsync(() => _pageEditor.CropPagesAsync(_document, indexes, margins));
            CancelCropMode();
            await ReloadAfterPageEditAsync();
            _status.Text = PageEditStatus.FormatCropped(indexes.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Crop, ex.Message);
        }
    }

    private async Task MergePdfsAsync()
    {
        var window = App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.OpenPicker);
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".pdf");

        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            _status.Text = PageEditStatus.MergeCancelled;
            return;
        }

        var insertAt = PageInsertIndex.Append(_document.PageCount);
        var opened = new List<IPdfDocument>();
        try
        {
            foreach (var file in files)
            {
                opened.Add(await _documentFactory.OpenAsync(file.Path));
            }

            var before = _document.PageCount;
            _status.Text = PageEditStatus.FormatMerging(files.Count);
            await RunPageEditAsync(() => _pageEditor.MergeDocumentsAsync(_document, opened, insertAt));
            await ReloadAfterPageEditAsync();
            var added = _document.PageCount - before;
            _status.Text = PageEditStatus.FormatMerged(added);
            if (added > 0)
            {
                await GoToPageAsync(insertAt, recordHistory: true);
            }
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Merge, ex.Message);
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
                _status.Text = PageClipboardStatus.NeedTwoPagesToSplit;
                return;
            }

            splitBefore = Enumerable.Range(1, _document.PageCount - 1).ToList();
        }

        var ranges = PdfSplitRanges.BuildRanges(_document.PageCount, splitBefore);
        if (ranges.Count < 2)
        {
            _status.Text = PageEditStatus.SplitWouldBeSingle;
            return;
        }

        var window = App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.FolderPicker);
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            _status.Text = PageEditStatus.SplitCancelled;
            return;
        }

        _status.Text = PageEditStatus.FormatSplitting(ranges.Count);
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

            _status.Text = PageEditStatus.FormatSplitDone(parts.Count, folder.Name);
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
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = DocumentExportFormats.SuggestedCroppedPages;
        picker.FileTypeChoices.Add("PDF", [".pdf"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            _status.Text = DocumentExportFormats.CancelledStatus;
            return;
        }

        try
        {
            _status.Text = DocumentExportFormats.ExportingCroppedPdf;
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
            _status.Text = DocumentExportFormats.FormatExportedCroppedPdf(file.Name);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Export, ex.Message);
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
            Header = PdfDialogHeaders.Units,
            ItemsSource = PdfDialogOptions.CropUnits.ToList(),
            SelectedIndex = (int)_cropUnit,
            Width = 220,
        };
        var leftBox = new NumberBox
        {
            Header = PdfDialogHeaders.Left,
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginLeftPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var topBox = new NumberBox
        {
            Header = PdfDialogHeaders.Top,
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginTopPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var rightBox = new NumberBox
        {
            Header = PdfDialogHeaders.Right,
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginRightPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var bottomBox = new NumberBox
        {
            Header = PdfDialogHeaders.Bottom,
            Value = PdfLengthUnits.FromPoints(_cropMode ? _cropMarginBottomPt : 36, _cropUnit),
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var allPages = new CheckBox { Content = PdfViewerChromeLabels.ApplyToAllPages, IsChecked = false };
        var note = new TextBlock
        {
            Text = PdfViewerTextLabels.NonDestructiveCropBoxInsetVisualHandlesRemainAvailableFromCrop,
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
            PrimaryButtonText = DialogButtons.Crop,
            CloseButtonText = DialogButtons.Cancel,
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
        var margins = PdfCropMarginsParser.FromDialogValues(
            leftBox.Value,
            topBox.Value,
            rightBox.Value,
            bottomBox.Value,
            _cropUnit);

        if (_cropMode)
        {
            _cropMarginLeftPt = margins.LeftPoints;
            _cropMarginTopPt = margins.TopPoints;
            _cropMarginRightPt = margins.RightPoints;
            _cropMarginBottomPt = margins.BottomPoints;
            RedrawCropOverlay();
            return;
        }

        indexes = PageSelection.ResolveTargets(
            allPages.IsChecked == true,
            _pageSelection,
            _document.PageCount,
            CurrentPageIndex).ToList();

        try
        {
            _status.Text = PageEditStatus.Cropping;
            await RunPageEditAsync(() => _pageEditor.CropPagesAsync(_document, indexes, margins));
            await ReloadAfterPageEditAsync();
            _status.Text = PageEditStatus.FormatCropped(indexes.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Crop, ex.Message);
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
                left = PdfCropMargins.ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, _cropDragStartRight, minSize);
                right = PdfCropMargins.ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                top = PdfCropMargins.ClampMargin(_cropDragStartTop + dy, page.HeightPoints, _cropDragStartBottom, minSize);
                bottom = PdfCropMargins.ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
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
                left = PdfCropMargins.ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, right, minSize);
                break;
            case "e":
                right = PdfCropMargins.ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                break;
            case "n":
                top = PdfCropMargins.ClampMargin(_cropDragStartTop + dy, page.HeightPoints, bottom, minSize);
                break;
            case "s":
                bottom = PdfCropMargins.ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                break;
            case "nw":
                left = PdfCropMargins.ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, right, minSize);
                top = PdfCropMargins.ClampMargin(_cropDragStartTop + dy, page.HeightPoints, bottom, minSize);
                break;
            case "ne":
                right = PdfCropMargins.ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                top = PdfCropMargins.ClampMargin(_cropDragStartTop + dy, page.HeightPoints, bottom, minSize);
                break;
            case "sw":
                left = PdfCropMargins.ClampMargin(_cropDragStartLeft + dx, page.WidthPoints, right, minSize);
                bottom = PdfCropMargins.ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                break;
            case "se":
                right = PdfCropMargins.ClampMargin(_cropDragStartRight - dx, page.WidthPoints, left, minSize);
                bottom = PdfCropMargins.ClampMargin(_cropDragStartBottom - dy, page.HeightPoints, top, minSize);
                break;
        }

        _cropMarginLeftPt = Math.Max(0, left);
        _cropMarginTopPt = Math.Max(0, top);
        _cropMarginRightPt = Math.Max(0, right);
        _cropMarginBottomPt = Math.Max(0, bottom);
        RedrawCropOverlay();
    }


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
        if (_layoutMode != PageLayoutMode.Continuous)
        {
            return;
        }

        // Keep page chrome in sync while flinging; throttle bitmap fills so scroll stays smooth (F57-08).
        UpdateCurrentPageFromScroll();
        if (!e.IsIntermediate)
        {
            await RenderVisibleAsync();
            return;
        }

        var now = Environment.TickCount64;
        if (!IntermediateScrollThrottle.ShouldRender(now, _lastIntermediateRenderTick))
        {
            return;
        }

        _lastIntermediateRenderTick = now;
        _ = RenderVisibleAsync();
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

            // Drop distant page bitmaps from the visual tree; LRU cache still holds recent renders (F57-11).
            foreach (var (pageIndex, image) in _pageImages)
            {
                if (pageIndex < first - 2 || pageIndex > last + 2)
                {
                    image.Source = null;
                }
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

        if (_selectedAnnot is not null)
        {
            DrawAnnotSelection(_selectedAnnot);
        }

        RedrawStickyNotePopups();
    }
    private int _thumbnailGeneration;

    private async Task RenderThumbnailsAsync()
    {
        var generation = ++_thumbnailGeneration;
        var count = _document.PageCount;
        if (count <= 0)
        {
            return;
        }

        // Prefer pages near the current view so the sidebar fills usefully first (F57-04).
        var ordered = Enumerable.Range(0, count)
            .OrderBy(i => Math.Abs(i - CurrentPageIndex))
            .ThenBy(i => i)
            .ToList();

        foreach (var i in ordered)
        {
            if (generation != _thumbnailGeneration)
            {
                return;
            }

            try
            {
                await RenderThumbnailAsync(i);
            }
            catch
            {
                // Thumbnail failures must not break viewing.
            }

            // Yield so page renders and input stay responsive on large docs.
            await Task.Yield();
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
        var thumbScale = _thumbnailWidth / Math.Max(1, page.WidthPoints);

        if (_cache.TryGet(_thumbnailKey, pageIndex, thumbScale, out var cached) && cached is not null)
        {
            image.Source = await ToWriteableBitmapAsync(cached);
            return;
        }

        var result = await _renderer.RenderPageAsync(
            _document,
            pageIndex,
            new PdfRenderRequest(thumbScale, MaxWidthPixels: (int)_thumbnailWidth));

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
        // Bookmarks list is mutated in place on _viewState.Bookmarks.
    }

    private async Task ExportBookmarksToPdfOutlineAsync()
    {
        if (_viewState.Bookmarks.Count == 0)
        {
            _status.Text = BookmarkStatus.AddBeforeExport;
            return;
        }

        try
        {
            _status.Text = BookmarkStatus.WritingOutline;
            var entries = _viewState.Bookmarks
                .Select(b => new PdfOutlineExportEntry(b.Title, b.PageIndex))
                .ToList();
            await _outlineExport.ExportAsync(_document, entries);
            NotifyEdited();
            await LoadOutlineAsync();
            _status.Text = DocumentExportFormats.FormatExportedBookmarks(_viewState.Bookmarks.Count);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.OutlineExport, ex.Message);
        }
    }

    private void RefreshBookmarkList()
    {
        var selectedId = (_bookmarkList.SelectedItem as BookmarkListItem)?.Id;
        _bookmarkList.ItemsSource = _viewState.Bookmarks
            .Select(b => new BookmarkListItem(b.Id, $"{b.Title} · p.{b.PageIndex + 1}", b.PageIndex))
            .ToList();
        if (selectedId is not null)
        {
            _bookmarkList.SelectedItem = _bookmarkList.Items
                .OfType<BookmarkListItem>()
                .FirstOrDefault(i => i.Id == selectedId);
        }
    }

    private async Task AddBookmarkAsync()
    {
        var page = CurrentPageIndex;
        var box = new TextBox
        {
            Header = PdfDialogHeaders.BookmarkTitle,
            Text = $"Page {page + 1}",
            Width = 280,
        };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.AddBookmark,
            Content = box,
            PrimaryButtonText = DialogButtons.Add,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(box.Text) ? $"Page {page + 1}" : box.Text.Trim();
        _viewState.Bookmarks.Add(new Glyph.Core.Documents.UserBookmark
        {
            Title = title,
            PageIndex = page,
        });
        RefreshBookmarkList();
        SyncViewState();
        _status.Text = BookmarkStatus.FormatBookmarked(title);
    }

    private async Task RenameSelectedBookmarkAsync()
    {
        if (_bookmarkList.SelectedItem is not BookmarkListItem item)
        {
            _status.Text = AnnotationSelectionStatus.RenameBookmark;
            return;
        }

        var bookmark = _viewState.Bookmarks.FirstOrDefault(b => b.Id == item.Id);
        if (bookmark is null)
        {
            return;
        }

        var box = new TextBox { Header = PdfDialogHeaders.Title, Text = bookmark.Title, Width = 280 };
        var dialog = new ContentDialog
        {
            Title = PdfDialogTitles.RenameBookmark,
            Content = box,
            PrimaryButtonText = DialogButtons.Save,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        bookmark.Title = string.IsNullOrWhiteSpace(box.Text) ? bookmark.Title : box.Text.Trim();
        RefreshBookmarkList();
        SyncViewState();
        _status.Text = BookmarkStatus.Renamed;
    }

    private void DeleteSelectedBookmark()
    {
        if (_bookmarkList.SelectedItem is not BookmarkListItem item)
        {
            _status.Text = AnnotationSelectionStatus.DeleteBookmark;
            return;
        }

        var removed = _viewState.Bookmarks.RemoveAll(b => b.Id == item.Id);
        if (removed > 0)
        {
            RefreshBookmarkList();
            SyncViewState();
            _status.Text = BookmarkStatus.Deleted;
        }
    }

    private void MoveSelectedBookmark(int delta)
    {
        if (_bookmarkList.SelectedItem is not BookmarkListItem item)
        {
            return;
        }

        var index = _viewState.Bookmarks.FindIndex(b => b.Id == item.Id);
        if (index < 0)
        {
            return;
        }

        var target = index + delta;
        if (target < 0 || target >= _viewState.Bookmarks.Count)
        {
            return;
        }

        var bookmark = _viewState.Bookmarks[index];
        _viewState.Bookmarks.RemoveAt(index);
        _viewState.Bookmarks.Insert(target, bookmark);
        RefreshBookmarkList();
        SyncViewState();
    }

    private sealed record BookmarkListItem(string Id, string Display, int PageIndex)
    {
        public override string ToString() => Display;
    }

    private void UpdateStatus()
    {
        _gotoBox.Text = (CurrentPageIndex + 1).ToString();
        var encrypted = PdfDocumentPermissions.StatusBarEncryptedSuffix(_document.IsEncrypted);
        _status.Text = AnnotationMutationStatus.FormatChromeStatus(
            CurrentPageIndex + 1,
            _document.PageCount,
            (int)Math.Round(_scale * 100),
            _layoutMode.ToString(),
            encrypted);
    }

    private async Task PrintDocumentAsync()
    {
        try
        {
            var info = _documentInfo.GetInfo(_document);
            if (!info.Permissions.CanPrint)
            {
                _status.Text = PrintPageScopeChooser.PrintingNotAllowed;
                return;
            }

            var scopeBox = new ComboBox
            {
                Width = 220,
                ItemsSource = PrintPageScopeChooser.Labels.ToList(),
                SelectedIndex = _pageSelection.Count > 0
                    ? (int)PrintPageScopeChooser.Scope.SelectedPages
                    : (int)PrintPageScopeChooser.Scope.CurrentPage,
            };
            var rangeBox = new TextBox
            {
                Header = PdfDialogHeaders.RangeEG135,
                Width = 220,
                Text = $"{CurrentPageIndex + 1}",
                Visibility = Visibility.Collapsed,
            };
            var scaleBox = new ComboBox
            {
                Header = PdfDialogHeaders.Scale,
                Width = 220,
                ItemsSource = PdfDialogOptions.PrintScales.ToList(),
                SelectedIndex = 0,
            };
            var nUpBox = new ComboBox
            {
                Header = PdfDialogHeaders.PagesPerSheet,
                Width = 220,
                ItemsSource = PdfDialogOptions.PagesPerSheet.ToList(),
                SelectedIndex = 0,
            };
            var grayscale = new CheckBox { Content = PdfViewerChromeLabels.Grayscale };
            var center = new CheckBox { Content = PdfViewerChromeLabels.CenterOnPage, IsChecked = true };
            var autoRotate = new CheckBox { Content = PdfViewerChromeLabels.AutoRotate, IsChecked = true };
            var includeNotes = new CheckBox { Content = PrintNotesUi.IncludeCheckbox };
            scopeBox.SelectionChanged += (_, _) =>
            {
                rangeBox.Visibility =
                    PrintPageScopeChooser.FromComboIndex(scopeBox.SelectedIndex)
                        == PrintPageScopeChooser.Scope.PageRange
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            };

            var dialog = new ContentDialog
            {
                Title = PdfDialogTitles.PrintPdf,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = PdfViewerTextLabels.AnnotationsAreIncludedInThePageRenderSystemDialogSetsPrinterCopiesCollateDuplexAndPaper,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 360,
                            Opacity = 0.8,
                        },
                        scopeBox,
                        rangeBox,
                        scaleBox,
                        nUpBox,
                        grayscale,
                        center,
                        autoRotate,
                        includeNotes,
                    },
                },
                PrimaryButtonText = DialogButtons.PrintEllipsis,
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                _status.Text = PrintPageScopeChooser.CancelledStatus;
                return;
            }

            var indexes = PrintPageScopeChooser.FromComboIndex(scopeBox.SelectedIndex) switch
            {
                PrintPageScopeChooser.Scope.SelectedPages => SelectedOrCurrentPages(),
                PrintPageScopeChooser.Scope.PageRange =>
                    PageRangeParser.Parse(rangeBox.Text, _document.PageCount).ToList(),
                PrintPageScopeChooser.Scope.AllPages =>
                    Enumerable.Range(0, _document.PageCount).ToList(),
                _ => [CurrentPageIndex],
            };
            if (indexes.Count == 0)
            {
                _status.Text = PrintPageScopeChooser.NoPagesToPrint;
                return;
            }

            var scaleMode = scaleBox.SelectedIndex switch
            {
                1 => DocumentPrintScaleMode.Fill,
                2 => DocumentPrintScaleMode.ActualSize,
                _ => DocumentPrintScaleMode.Fit,
            };
            var pagesPerSheet = nUpBox.SelectedIndex switch
            {
                1 => 2,
                2 => 4,
                _ => 1,
            };

            _status.Text = PrintPageScopeChooser.FormatPreparing(indexes.Count);
            var bitmaps = new List<WriteableBitmap>();
            const double printDpi = 150;
            var renderScale = printDpi / 72.0;
            foreach (var pageIndex in indexes)
            {
                using var rendered = await _renderer.RenderPageAsync(
                    _document,
                    pageIndex,
                    new PdfRenderRequest(renderScale));
                var pixels = rendered.Pixels.ToArray();
                if (grayscale.IsChecked == true)
                {
                    DocumentPrintHelper.ApplyGrayscale(pixels);
                }

                bitmaps.Add(await DocumentPrintHelper.ToWriteableBitmapAsync(
                    rendered.Width,
                    rendered.Height,
                    pixels));
            }

            if (includeNotes.IsChecked == true)
            {
                var notesBitmap = await RenderNotesPrintPageAsync();
                if (notesBitmap is not null)
                {
                    bitmaps.Add(notesBitmap);
                }
            }

            var window = _ownerWindow
                ?? App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException(MainWindowRequiredMessages.Print);
            using var helper = new DocumentPrintHelper(
                window,
                jobName: System.IO.Path.GetFileName(_document.Path) ?? "Glyph PDF",
                scaleMode: scaleMode,
                center: center.IsChecked == true,
                autoRotate: autoRotate.IsChecked == true,
                pagesPerSheet: pagesPerSheet);
            await helper.PrintAsync(bitmaps);
            _status.Text = PrintPageScopeChooser.FormatPrintUiShown(bitmaps.Count, pagesPerSheet);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Print, ex.Message);
        }
    }

    private static void ApplyToolbarAccessibleNames(params DependencyObject[] elements)
    {
        foreach (var element in elements)
        {
            if (ToolTipService.GetToolTip(element) is string tip && tip.Length > 0)
            {
                AutomationProperties.SetName(element, tip);
            }
        }
    }

    private async Task<WriteableBitmap?> RenderNotesPrintPageAsync()
    {
        try
        {
            var annotations = await _annotations.ListAsync(_document);
            var title = System.IO.Path.GetFileName(_document.Path) ?? "Document";
            var text = PdfNotesExport.Format(annotations, documentTitle: title);
            if (string.IsNullOrWhiteSpace(text) || !annotations.Any(a => a.IsStickyNote))
            {
                return null;
            }

            var block = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Width = 612,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)),
            };
            block.Measure(new Windows.Foundation.Size(612, 20000));
            var height = (int)Math.Ceiling(Math.Max(792, block.DesiredSize.Height + 24));
            block.Arrange(new Windows.Foundation.Rect(0, 0, 612, height));
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(block, 612, height);
            var pixels = (await rtb.GetPixelsAsync()).ToArray();
            return await DocumentPrintHelper.ToWriteableBitmapAsync(rtb.PixelWidth, rtb.PixelHeight, pixels);
        }
        catch
        {
            return null;
        }
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
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.Export);

        var formatBox = new ComboBox
        {
            Width = 180,
            SelectedIndex = 0,
        };
        foreach (var name in DocumentExportFormats.PageImageFormatNames)
        {
            formatBox.Items.Add(name);
        }

        var dpiBox = new TextBox { Width = 80, Text = DocumentExportFormats.DefaultDpi.ToString("0") };
        var qualityBox = new Slider
        {
            Minimum = DocumentExportFormats.MinQuality,
            Maximum = DocumentExportFormats.MaxQuality,
            Value = DocumentExportFormats.DefaultQuality,
            Width = 180,
            Header = DocumentExportFormats.QualityHeader,
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = DocumentExportFormats.ExportSummary(indexes.Count) },
                new TextBlock { Text = PdfViewerTextLabels.Format },
                formatBox,
                new TextBlock { Text = DocumentExportFormats.DpiHeader },
                dpiBox,
                qualityBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = DocumentExportFormats.DialogTitle,
            Content = panel,
            PrimaryButtonText = DocumentExportFormats.PrimaryButton,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = DocumentExportFormats.CancelledStatus;
            return;
        }

        var formatName = formatBox.SelectedItem as string ?? "PNG";
        var extension = DocumentExportFormats.ExtensionForDisplayName(formatName);

        var dpi = DocumentExportFormats.ParseDpi(dpiBox.Text);

        var options = new PdfPageImageExportOptions(
            Extension: extension,
            Dpi: dpi,
            Quality: DocumentExportFormats.ClampQuality(qualityBox.Value),
            EmbedSrgbProfile: true);

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

        // Transparency: formats with FlattensTransparency flatten via Magick/codec; others keep render alpha.

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
                    _status.Text = DocumentExportFormats.CancelledStatus;
                    return;
                }

                _status.Text = DocumentExportFormats.ExportingPage;
                ShowJobProgress(0, determinate: false);
                try
                {
                    await _export.ExportPageAsImageAsync(_document, indexes[0], file.Path, options);
                    _status.Text = DocumentExportFormats.FormatExportedPage(indexes[0] + 1, file.Name);
                }
                finally
                {
                    HideJobProgress();
                }

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
                _status.Text = DocumentExportFormats.CancelledStatus;
                return;
            }

            _status.Text = DocumentExportFormats.FormatExportingPages(indexes.Count);
            ShowJobProgress(0, determinate: true);
            try
            {
                var progress = new Progress<double>(p => ShowJobProgress(p));
                var written = await _export.ExportPagesAsImagesAsync(
                    _document,
                    indexes,
                    folder.Path,
                    baseName,
                    options,
                    progress);
                _status.Text = DocumentExportFormats.FormatExportedPages(written, folder.Name);
            }
            finally
            {
                HideJobProgress();
            }
        }
        catch (Exception ex)
        {
            HideJobProgress();
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Export, ex.Message);
        }
    }

    private async Task ShowProtectDialogAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.DocumentInfo);

        var body = new TextBlock
        {
            Text = _security.WriteProtectSupported
                ? "Password-protect is available."
                : PdfSecurityWriteUiCopy.DialogBody(),
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 420,
        };

        var dialog = new ContentDialog
        {
            Title = PdfSecurityWriteUiCopy.DialogTitle,
            Content = body,
            CloseButtonText = PdfSecurityWriteUiCopy.CloseButton,
            XamlRoot = window.Content.XamlRoot,
        };

        await dialog.ShowAsync();
        _status.Text = _security.WriteProtectSupported
            ? PdfSecurityWriteUiCopy.ToolbarLabel
            : PdfSecurityWriteUiCopy.StatusBlocked;
    }

    private async Task ShowOptimizeDialogAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.Optimize);

        var presetBox = new ComboBox
        {
            Width = 260,
            SelectedIndex = PdfOptimizeDialogUi.DefaultPresetIndex,
            ItemsSource = PdfOptimizeDialogUi.PresetLabels.ToList(),
        };

        var aboveDpiBox = new TextBox { Width = 80, Text = "225", IsEnabled = false };
        var targetDpiBox = new TextBox { Width = 80, Text = "150", IsEnabled = false };
        var jpegQualityBox = new NumberBox
        {
            Header = PdfOptimizeDialogUi.JpegQualityHeader,
            Width = 140,
            Value = 75,
            Minimum = 1,
            Maximum = 100,
            SmallChange = 5,
            LargeChange = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            IsEnabled = false,
        };
        var stripAttachments = new CheckBox { Content = PdfOptimizeDialogUi.StripAttachmentsLabel, IsEnabled = false };
        var preserveMono = new CheckBox { Content = PdfOptimizeDialogUi.PreserveMonoLabel, IsChecked = true, IsEnabled = false };
        var stripMetadata = new CheckBox { Content = PdfOptimizeDialogUi.StripMetadataLabel, IsEnabled = false };

        void SyncCustomEnabled()
        {
            var custom = PdfOptimizeDialogUi.IsCustomIndex(presetBox.SelectedIndex);
            aboveDpiBox.IsEnabled = custom;
            targetDpiBox.IsEnabled = custom;
            jpegQualityBox.IsEnabled = custom;
            stripAttachments.IsEnabled = custom;
            preserveMono.IsEnabled = custom;
            stripMetadata.IsEnabled = custom;
            if (!custom)
            {
                var preset = SelectedPreset();
                var opts = PdfOptimizeOptions.FromPreset(preset);
                aboveDpiBox.Text = opts.DownsampleAboveDpi.ToString("0");
                targetDpiBox.Text = opts.TargetDpi.ToString("0");
                jpegQualityBox.Value = opts.JpegQuality;
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
            Text = PdfViewerTextLabels.ChooseAPresetOrCustomThenEstimateOrApplyDownsampledImagesAreRewrittenAsJPEGWhenQualitySettingsApply,
        };

        PdfOptimizePreset SelectedPreset() => PdfOptimizeDialogUi.FromComboIndex(presetBox.SelectedIndex);

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

            var jpegQuality = double.IsNaN(jpegQualityBox.Value)
                ? 75
                : (int)Math.Clamp(jpegQualityBox.Value, 1, 100);

            return new PdfOptimizeOptions(
                Preset: PdfOptimizePreset.Custom,
                DownsampleImages: true,
                DownsampleAboveDpi: above,
                TargetDpi: target,
                JpegQuality: jpegQuality,
                PreserveMonochrome: preserveMono.IsChecked == true,
                RemoveEmbeddedAttachments: stripAttachments.IsChecked == true,
                RemoveMetadata: stripMetadata.IsChecked == true);
        }

        var estimateButton = new Button { Content = PdfViewerChromeLabels.Estimate, Margin = new Thickness(0, 8, 8, 0) };
        estimateButton.Click += (_, _) =>
        {
            try
            {
                var estimate = _optimize.Estimate(_document, BuildOptions());
                estimateText.Text =
                    $"Current: {ByteSizeFormat.Format(estimate.CurrentBytes)} · Estimated: {ByteSizeFormat.Format(estimate.EstimatedBytes)} · "
                    + $"{estimate.ImagesEligibleForDownsample} image(s) above DPI threshold · "
                    + $"{estimate.AttachmentCount} embedded file(s).";
            }
            catch (Exception ex)
            {
                estimateText.Text = PdfViewerTextLabels.EstimateFailed + ex.Message;
            }
        };

        var customRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = PdfViewerTextLabels.AboveDPI, VerticalAlignment = VerticalAlignment.Center },
                aboveDpiBox,
                new TextBlock { Text = PdfViewerTextLabels.TargetDPI, VerticalAlignment = VerticalAlignment.Center },
                targetDpiBox,
            },
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = PdfViewerTextLabels.Preset },
                presetBox,
                customRow,
                jpegQualityBox,
                stripAttachments,
                preserveMono,
                stripMetadata,
                estimateButton,
                estimateText,
            },
        };

        var dialog = new ContentDialog
        {
            Title = PdfOptimizeDialogUi.DialogTitle,
            Content = panel,
            PrimaryButtonText = PdfOptimizeDialogUi.ApplyButton,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = PdfOptimizeDialogUi.CancelledStatus;
            return;
        }

        try
        {
            _status.Text = PdfOptimizeDialogUi.OptimizingStatus;
            ShowJobProgress(0, determinate: false);
            var result = await _optimize.OptimizeAsync(_document, BuildOptions());
            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            _status.Text = PdfOptimizeDialogUi.ResultStatus(
                result,
                ByteSizeFormat.Format(result.BytesBefore),
                ByteSizeFormat.Format(result.BytesAfter));
        }
        catch (Exception ex)
        {
            _status.Text = PdfOptimizeDialogUi.FailedStatus(ex.Message);
        }
        finally
        {
            HideJobProgress();
        }
    }

    private async Task ShowDocumentInfoAsync()
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.DocumentInfo);

        PdfDocumentInfo info;
        try
        {
            info = _documentInfo.GetInfo(_document);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Info, ex.Message);
            return;
        }

        var annotationCount = 0;
        try
        {
            var annotations = await _annotations.ListAsync(_document);
            annotationCount = annotations.Count;
        }
        catch
        {
            // Annotation list optional for inspector.
        }

        static string Bytes(long? size) => ByteSizeFormat.FormatOptional(size);

        var perms = info.Permissions;
        var permissionLines = perms.FormatLines();

        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460,
            Text = PdfDocumentInfoUi.FormatDialogBody(
                DisplayValue.OrEmDash(info.Title),
                DisplayValue.OrEmDash(info.Author),
                DisplayValue.OrEmDash(info.Subject),
                DisplayValue.OrEmDash(info.Keywords),
                DisplayValue.OrEmDash(info.Creator),
                DisplayValue.OrEmDash(info.Producer),
                DisplayValue.OrEmDash(info.CreationDate),
                DisplayValue.OrEmDash(info.ModificationDate),
                info.PageCount,
                annotationCount,
                DisplayValue.OrEmDash(info.PdfVersion),
                PdfPageSizeFormat.FormatPoints(info.PageWidthPoints, info.PageHeightPoints),
                info.Fonts.Count == 0 ? DisplayValue.EmDash : string.Join(", ", info.Fonts),
                info.EmbeddedAttachmentCount,
                DisplayValue.OrEmDash(info.FilePath),
                DisplayValue.OrEmDash(info.FilePath is null ? null : System.IO.Path.GetFileName(info.FilePath)),
                Bytes(info.FileSizeBytes),
                PdfDocumentPermissions.InfoEncryptedLine(info.IsEncrypted),
                info.SecurityHandlerRevision < 0 ? "none" : info.SecurityHandlerRevision.ToString(),
                $"0x{info.PermissionFlags:X8}",
                perms.FormatSection()),
        };

        var dialog = new ContentDialog
        {
            Title = PdfDocumentInfoUi.DialogTitle,
            Content = new ScrollViewer
            {
                Content = body,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = PdfDocumentInfoUi.EditButton,
            CloseButtonText = PdfDocumentInfoUi.CloseButton,
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
            ? PdfDocumentPermissions.EncryptedAdvisoryStatus
            : AnnotationMutationStatus.DocumentInfo;
    }

    /// <summary>File → Properties entry point (F48).</summary>
    public Task ShowPropertiesAsync() => ShowDocumentInfoAsync();

    public void ToggleFullscreen()
    {
        if (App.CurrentApp.MainWindowInstance is MainWindow window)
        {
            window.ToggleFullscreen();
            _status.Text = FullscreenTogglePolicy.Toggled;
            return;
        }

        _status.Text = FullscreenTogglePolicy.Unavailable;
    }

    public void ToggleToolbarVisibility()
    {
        SetToolbarVisible(!IsToolbarVisible);
        _status.Text = IsToolbarVisible ? AnnotationMutationStatus.ToolbarShown : AnnotationMutationStatus.ToolbarHidden;
    }

    public void SetToolbarVisible(bool visible)
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Re-apply F54 hide/reorder (+ compact spacing) from Preferences without reopening.</summary>
    public void ApplyToolbarCustomization(AppSettings? settings)
    {
        ToolbarCommandApplicator.Apply(_toolbar, settings, _toolbarDefaults);
        if (settings is not null)
        {
            SetToolbarVisible(settings.ToolbarVisible);
        }
    }

    public bool IsToolbarVisible => _toolbar?.Visibility != Visibility.Collapsed;

    /// <summary>File → Save / Save As (F01-16/17).</summary>
    public async Task SaveDocumentAsync(bool saveAs)
    {
        try
        {
            var path = _document.Path;
            if (saveAs || string.IsNullOrWhiteSpace(path))
            {
                var window = _ownerWindow
                    ?? App.CurrentApp.MainWindowInstance
                    ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
                var picker = new FileSavePicker();
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("PDF", [".pdf"]);
                picker.SuggestedFileName = string.IsNullOrWhiteSpace(path)
                    ? "document.pdf"
                    : System.IO.Path.GetFileName(path);
                var file = await picker.PickSaveFileAsync();
                if (file is null)
                {
                    _status.Text = DocumentSaveStatus.Cancelled;
                    return;
                }

                path = file.Path;
            }

            await _pageEditor.SaveAsync(_document, path!);
            ClearUnsavedEdits();
            _status.Text = DocumentSaveStatus.SavedFileName(System.IO.Path.GetFileName(path));
            App.CurrentApp.MainWindowInstance?.NotifyActiveDocumentSaved(path!);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.Save, ex.Message);
        }
    }

    private async Task EditDocumentInfoAsync(PdfDocumentInfo current)
    {
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.DocumentInfoEdit);

        var titleBox = new TextBox { Text = current.Title ?? string.Empty, Width = 320 };
        var authorBox = new TextBox { Text = current.Author ?? string.Empty, Width = 320 };
        var subjectBox = new TextBox { Text = current.Subject ?? string.Empty, Width = 320 };
        var keywordsBox = new TextBox { Text = current.Keywords ?? string.Empty, Width = 320 };
        var creatorBox = new TextBox { Text = current.Creator ?? string.Empty, Width = 320 };
        var producerBox = new TextBox { Text = current.Producer ?? string.Empty, Width = 320 };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = PdfDocumentInfoUi.FieldTitle },
                titleBox,
                new TextBlock { Text = PdfDocumentInfoUi.FieldAuthor },
                authorBox,
                new TextBlock { Text = PdfDocumentInfoUi.FieldSubject },
                subjectBox,
                new TextBlock { Text = PdfDocumentInfoUi.FieldKeywords },
                keywordsBox,
                new TextBlock { Text = PdfDocumentInfoUi.FieldCreator },
                creatorBox,
                new TextBlock { Text = PdfDocumentInfoUi.FieldProducer },
                producerBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = PdfDocumentInfoUi.EditDialogTitle,
            Content = panel,
            PrimaryButtonText = PdfDocumentInfoUi.SaveButton,
            SecondaryButtonText = PdfDocumentInfoUi.ClearAllButton,
            CloseButtonText = PdfDocumentInfoUi.CloseButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
        };

        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.None)
        {
            _status.Text = PdfDocumentInfoUi.EditCancelledStatus;
            return;
        }

        try
        {
            _infoUndoStack.Push(
                new PdfDocumentInfoUpdate(
                    Title: current.Title ?? string.Empty,
                    Author: current.Author ?? string.Empty,
                    Subject: current.Subject ?? string.Empty,
                    Keywords: current.Keywords ?? string.Empty,
                    Creator: current.Creator ?? string.Empty,
                    Producer: current.Producer ?? string.Empty));

            if (choice == ContentDialogResult.Secondary)
            {
                _documentInfo.SetInfo(_document, new PdfDocumentInfoUpdate(ClearAll: true));
                _status.Text = PdfDocumentInfoUi.ClearedStatus;
            }
            else
            {
                _documentInfo.SetInfo(
                    _document,
                    new PdfDocumentInfoUpdate(
                        Title: titleBox.Text,
                        Author: authorBox.Text,
                        Subject: subjectBox.Text,
                        Keywords: keywordsBox.Text,
                        Creator: creatorBox.Text,
                        Producer: producerBox.Text));
                _status.Text = PdfDocumentInfoUi.UpdatedStatus;
            }

            _cache.ClearDocument(_documentKey);
            _cache.ClearDocument(_thumbnailKey);
            await RenderVisibleAsync();
            await RenderThumbnailsAsync();
            RefreshPropertiesSidebar();
            NotifyEdited();
        }
        catch (Exception ex)
        {
            _infoUndoStack.TryDiscardTop();
            _status.Text = PdfDocumentInfoUi.FailedStatus(ex.Message);
        }
    }

    private void RefreshPropertiesSidebar()
    {
        try
        {
            var info = _documentInfo.GetInfo(_document);
            static string Bytes(long? size) => ByteSizeFormat.FormatOptional(size);

            var pageSize = PdfPageSizeFormat.FormatPoints(
                info.PageWidthPoints,
                info.PageHeightPoints,
                separator: "×");
            var fileName = DisplayValue.OrEmDash(
                info.FilePath is null ? null : System.IO.Path.GetFileName(info.FilePath));

            _propertiesSummary.Text = PdfDocumentInfoUi.FormatSidebarSummary(
                DisplayValue.OrEmDash(info.Title),
                DisplayValue.OrEmDash(info.Author),
                DisplayValue.OrEmDash(info.Subject),
                DisplayValue.OrEmDash(info.Creator),
                DisplayValue.OrEmDash(info.Producer),
                info.PageCount,
                pageSize,
                fileName,
                Bytes(info.FileSizeBytes),
                DisplayValue.OrEmDash(info.PdfVersion),
                PdfDocumentPermissions.PropertiesEncryptedMarker(info.IsEncrypted),
                info.EmbeddedAttachmentCount);
        }
        catch (Exception ex)
        {
            _propertiesSummary.Text = PdfDocumentInfoUi.PropertiesUnavailablePrefix + ex.Message;
        }
    }

    private void RefreshAttachmentsSidebar()
    {
        try
        {
            _attachmentItems = _documentInfo.ListAttachments(_document);
            static string Bytes(long? size) => ByteSizeFormat.FormatOptional(size, "?");

            _attachmentList.ItemsSource = _attachmentItems.Count == 0
                ? new[] { PdfDialogOptions.NoneChoice }
                : _attachmentItems
                    .Select(a => $"{a.Name} · {Bytes(a.SizeBytes)}")
                    .ToList();
        }
        catch (Exception ex)
        {
            _attachmentItems = [];
            _attachmentList.ItemsSource = new[] { "Unavailable: " + ex.Message };
        }
    }

    private void AttachmentList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_attachmentItems.Count == 0
            || _attachmentList.SelectedIndex < 0
            || _attachmentList.SelectedIndex >= _attachmentItems.Count)
        {
            _status.Text = AnnotationSelectionStatus.AttachmentContextHint;
            return;
        }

        if (sender is not FrameworkElement target)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var saveItem = new MenuFlyoutItem { Text = PdfViewerTextLabels.SaveAs };
        saveItem.Click += async (_, _) => await SaveSelectedAttachmentAsync();
        flyout.Items.Add(saveItem);
        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private async Task SaveSelectedAttachmentAsync()
    {
        if (_attachmentItems.Count == 0
            || _attachmentList.SelectedIndex < 0
            || _attachmentList.SelectedIndex >= _attachmentItems.Count)
        {
            _status.Text = AnnotationSelectionStatus.SaveAttachment;
            return;
        }

        var item = _attachmentItems[_attachmentList.SelectedIndex];
        var window = _ownerWindow
            ?? App.CurrentApp.MainWindowInstance
            ?? throw new InvalidOperationException(MainWindowRequiredMessages.AttachmentSave);

        try
        {
            var bytes = _documentInfo.GetAttachmentBytes(_document, item.Index);
            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.SuggestedFileName = string.IsNullOrWhiteSpace(item.Name) ? "attachment.bin" : item.Name;
            var ext = System.IO.Path.GetExtension(item.Name);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".bin";
            }

            picker.FileTypeChoices.Add("Attachment", [ext]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = AttachmentSaveStatus.Cancelled;
                return;
            }

            await FileIO.WriteBytesAsync(file, bytes);
            _status.Text = AttachmentSaveStatus.FormatSaved(item.Name, bytes.Length);
        }
        catch (Exception ex)
        {
            _status.Text = PdfOperationFailedStatus.Format(PdfOperationFailedStatus.AttachmentSave, ex.Message);
        }
    }
}
