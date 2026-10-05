using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.App.Printing;
using Glyph.Core.Documents;
using Glyph.Core.IO;
using Glyph.Core.Signatures;
using Glyph.Imaging.Abstractions;
using Glyph.Infrastructure.Settings;
using Glyph.Ocr.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

namespace Glyph.App.Views;

/// <summary>
/// Image viewer/editor: zoom/pan, fit, rotate/flip/crop, folder navigation, and format conversion.
/// </summary>
public sealed class ImageDocumentView : UserControl
{
    private readonly IImageDocument _document;
    private readonly IImageProcessor _processor;
    private readonly IImageEncoder _encoder;
    private readonly IImageDecoder? _decoder;
    private readonly IOcrEngine? _ocr;
    private readonly ISignatureLibrary? _signatures;
    private readonly DocumentViewState _viewState;
    private readonly Func<string, Task>? _openSibling;
    private readonly ScrollViewer _scrollViewer;
    private StackPanel? _toolbar;
    private readonly Grid _imageSurface;
    private readonly Image _image;
    private readonly Canvas _cropOverlay;
    private readonly Canvas _markupOverlay;
    private readonly Rectangle _cropRect;
    private readonly Ellipse _selectionEllipse;
    private readonly ComboBox _selectionKindBox;
    private readonly TextBlock _status;
    private readonly TextBox _cropBox;
    private readonly ListView _siblingList;
    private readonly Button _prevButton;
    private readonly Button _nextButton;
    private readonly Button _slideshowButton;
    private readonly Button _animPlayButton;
    private readonly Button _animPrevButton;
    private readonly Button _animNextButton;
    private readonly Button _animRestartButton;
    private readonly Button _animExtractButton;
    private readonly CheckBox _animLoopBox;
    private readonly TextBlock _animFrameLabel;
    private readonly Button _undoButton;
    private readonly Button _interactiveCropButton;
    private readonly Button _applyCropButton;
    private readonly Button _cancelCropButton;
    private readonly ComboBox _cropAspectBox;
    private readonly Button _selectButton;
    private readonly Button _selectAllButton;
    private readonly Button _deselectButton;
    private readonly Button _invertSelButton;
    private readonly Button _copySelButton;
    private readonly Button _cutSelButton;
    private readonly Button _pasteSelButton;
    private readonly Button _deleteSelButton;
    private readonly Button _cropSelButton;
    private readonly Button _drawButton;
    private readonly Button _flattenMarkupButton;
    private ImageSelectionKind _selectionKind = ImageSelectionKind.Rectangle;
    private IReadOnlyList<string> _siblings = Array.Empty<string>();
    private DispatcherTimer? _slideshowTimer;
    private DispatcherTimer? _animationTimer;
    private bool _animationPlaying;
    private int _animationLoopsCompleted;
    private readonly List<IImageEditCheckpoint> _editUndoStack = [];
    private readonly Action? _onEdited;
    private readonly List<ImageMarkupStroke> _markupStrokes = [];
    private readonly List<ImageMarkupShape> _markupShapes = [];
    private readonly List<bool> _markupUndoWasShape = [];
    private ImageMarkupShapeKind? _markupShapeTool;
    private string? _pendingText;
    private double _pendingFontSize = 24;
    private const int MaxEditUndo = 12;
    private double _zoom = 1.0;
    private bool _loaded;
    private int _refreshGeneration;
    private bool _syncingList;
    private bool _cropMode;
    private bool _selectionMode;
    private bool _drawMode;
    private bool _cropDragging;
    private bool _selectionMoving;
    private bool _drawDragging;
    private Windows.Foundation.Point _cropStart;
    private Windows.Foundation.Point _moveStart;
    private double _moveOriginLeft;
    private double _moveOriginTop;
    private ImageRect? _moveSourcePixels;
    private bool _navDragging;
    private Windows.Foundation.Point _navStart;
    private ImageRect? _pixelSelection;
    private ImagePixelBuffer? _selectionClipboard;
    private bool _selectionInverted;
    private readonly List<ImageMarkupPoint> _lassoDocPoints = [];
    private Polyline? _lassoPolyline;
    private float[,]? _smartEdgeMap;
    private int _smartEdgeMapWidth;
    private int _smartEdgeMapHeight;
    private readonly List<Windows.Foundation.Point> _drawPoints = [];
    private Polyline? _activeDrawPolyline;
    private Shape? _activeShapePreview;
    private Windows.UI.Color _drawColor = Windows.UI.Color.FromArgb(255, 220, 20, 60);
    private double _drawWidthPixels = 3;
    private int _displayWidth;
    private int _displayHeight;

    public ImageDocumentView(
        IImageDocument document,
        IImageProcessor processor,
        IImageEncoder encoder,
        DocumentViewState? viewState = null,
        Func<string, Task>? openSibling = null,
        IOcrEngine? ocr = null,
        ISignatureLibrary? signatures = null,
        IImageDecoder? decoder = null,
        Action? onEdited = null)
    {
        _document = document;
        _processor = processor;
        _encoder = encoder;
        _decoder = decoder;
        _viewState = viewState ?? new DocumentViewState();
        _openSibling = openSibling;
        _ocr = ocr;
        _signatures = signatures;
        _onEdited = onEdited;
        _zoom = _viewState.Zoom <= 0 ? 1.0 : _viewState.Zoom;
        IsTabStop = true;

        _image = new Image
        {
            Stretch = Microsoft.UI.Xaml.Media.Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _cropRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 200, 0)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 200, 0)),
            Visibility = Visibility.Collapsed,
        };
        _selectionEllipse = new Ellipse
        {
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 144, 255)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
            Visibility = Visibility.Collapsed,
        };
        _cropOverlay = new Canvas
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
            IsHitTestVisible = false,
        };
        _cropOverlay.Children.Add(_cropRect);
        _cropOverlay.Children.Add(_selectionEllipse);
        _markupOverlay = new Canvas
        {
            IsHitTestVisible = false,
        };
        _imageSurface = new Grid();
        _imageSurface.Children.Add(_image);
        _imageSurface.Children.Add(_markupOverlay);
        _imageSurface.Children.Add(_cropOverlay);
        _image.CanDrag = true;
        _image.DragStarting += Image_DragStarting;
        _scrollViewer = new ScrollViewer
        {
            Content = _imageSurface,
            ZoomMode = ZoomMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _scrollViewer.PointerPressed += ImageSurface_PointerPressed;
        _scrollViewer.PointerMoved += ImageSurface_PointerMoved;
        _scrollViewer.PointerReleased += ImageSurface_PointerReleased;
        _scrollViewer.PointerCaptureLost += (_, _) =>
        {
            _navDragging = false;
            _selectionMoving = false;
        };
        // Precision-touchpad pinch often arrives as Ctrl+wheel; Manipulation Scale covers direct pinch (F53).
        _scrollViewer.PointerWheelChanged += ScrollViewer_PointerWheelChanged;
        _scrollViewer.ManipulationMode = ManipulationModes.Scale;
        _scrollViewer.ManipulationDelta += ScrollViewer_ManipulationDelta;
        _imageSurface.RightTapped += ImageSurface_RightTapped;
        _status = new TextBlock { Opacity = 0.75, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _cropBox = new TextBox
        {
            PlaceholderText = "Crop x,y,w,h",
            Width = 140,
        };
        _siblingList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 180,
        };
        _siblingList.SelectionChanged += SiblingList_SelectionChanged;

        var zoomOut = new Button { Content = "−", Width = 36 };
        var zoomIn = new Button { Content = "+", Width = 36 };
        var fit = new Button { Content = "Fit" };
        var actual = new Button { Content = "100%" };
        var rotateLeft = new Button { Content = "⟲" };
        var rotateRight = new Button { Content = "⟳" };
        var flipH = new Button { Content = "Flip H" };
        var flipV = new Button { Content = "Flip V" };
        var crop = new Button { Content = "Crop" };
        _interactiveCropButton = new Button { Content = "Crop…" };
        _applyCropButton = new Button { Content = "Apply crop", Visibility = Visibility.Collapsed };
        _cancelCropButton = new Button { Content = "Cancel crop", Visibility = Visibility.Collapsed };
        _cropAspectBox = new ComboBox
        {
            Width = 110,
            Visibility = Visibility.Collapsed,
            ItemsSource = new[] { "Free", "Original", "1:1", "4:3", "3:2", "16:9" },
            SelectedIndex = 0,
        };
        ToolTipService.SetToolTip(_cropAspectBox, "Crop aspect: free, original image ratio, or common presets");
        _selectButton = new Button { Content = "Select" };
        _selectionKindBox = new ComboBox
        {
            Width = 88,
            Visibility = Visibility.Collapsed,
            ItemsSource = new[] { "Rect", "Ellipse", "Lasso", "Smart" },
            SelectedIndex = 0,
        };
        _selectAllButton = new Button { Content = "All", Visibility = Visibility.Collapsed };
        _invertSelButton = new Button { Content = "Invert", Visibility = Visibility.Collapsed };
        _deselectButton = new Button { Content = "Deselect", Visibility = Visibility.Collapsed };
        _copySelButton = new Button { Content = "Copy sel", Visibility = Visibility.Collapsed };
        _cutSelButton = new Button { Content = "Cut sel", Visibility = Visibility.Collapsed };
        _pasteSelButton = new Button { Content = "Paste", Visibility = Visibility.Collapsed };
        _deleteSelButton = new Button { Content = "Del sel", Visibility = Visibility.Collapsed };
        _cropSelButton = new Button { Content = "Crop sel", Visibility = Visibility.Collapsed };
        _drawButton = new Button { Content = "Draw" };
        _flattenMarkupButton = new Button { Content = "Flatten", Visibility = Visibility.Collapsed };
        ToolTipService.SetToolTip(_selectButton, "Pixel selection (drag on image; drag inside to move; arrow keys nudge)");
        ToolTipService.SetToolTip(_selectionKindBox, "Selection shape: rectangle, ellipse, freeform lasso, or smart (edge-snapping) lasso");
        ToolTipService.SetToolTip(_selectAllButton, "Select entire image");
        ToolTipService.SetToolTip(_invertSelButton, "Invert selection (operations apply to outside)");
        ToolTipService.SetToolTip(_deselectButton, "Clear selection");
        ToolTipService.SetToolTip(_copySelButton, "Copy selection to clipboard as PNG");
        ToolTipService.SetToolTip(_cutSelButton, "Cut selection (copy + clear)");
        ToolTipService.SetToolTip(_pasteSelButton, "Paste at selection top-left (or 0,0)");
        ToolTipService.SetToolTip(_deleteSelButton, "Clear selection to transparent");
        ToolTipService.SetToolTip(_cropSelButton, "Crop image to selection");
        ToolTipService.SetToolTip(_drawButton, "Freehand markup (non-destructive overlay until Flatten/Save)");
        ToolTipService.SetToolTip(_flattenMarkupButton, "Bake markup strokes into pixels");
        var resize = new Button { Content = "Resize" };
        var adjust = new Button { Content = "Adjust" };
        var bgRemove = new Button { Content = "BG" };
        var stamp = new Button { Content = "Stamp" };
        var meta = new Button { Content = "Meta" };
        var ocrButton = new Button { Content = "OCR" };
        var rotate180 = new Button { Content = "180°" };
        var orient = new Button { Content = "Orient" };
        var straighten = new Button { Content = "Straighten" };
        var batchOrient = new Button { Content = "Batch…" };
        var fullscreen = new Button { Content = "Fullscreen" };
        var save = new Button { Content = "Save" };
        var exportPng = new Button { Content = "→PNG" };
        var exportJpeg = new Button { Content = "→JPEG" };
        var convert = new Button { Content = "Convert" };
        var printImage = new Button { Content = "Print" };
        var copyImage = new Button { Content = "Copy" };
        var pasteImage = new Button { Content = "Paste" };
        _prevButton = new Button { Content = "◀", Width = 36 };
        _nextButton = new Button { Content = "▶", Width = 36 };
        _slideshowButton = new Button { Content = "Slideshow" };
        _animPlayButton = new Button { Content = "Play", Visibility = Visibility.Collapsed };
        _animPrevButton = new Button { Content = "⟨frm", Visibility = Visibility.Collapsed };
        _animNextButton = new Button { Content = "frm⟩", Visibility = Visibility.Collapsed };
        _animRestartButton = new Button { Content = "Restart", Visibility = Visibility.Collapsed };
        _animExtractButton = new Button { Content = "Save frame", Visibility = Visibility.Collapsed };
        _animLoopBox = new CheckBox
        {
            Content = "Loop",
            IsChecked = true,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _animFrameLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Opacity = 0.8,
            Visibility = Visibility.Collapsed,
        };
        _undoButton = new Button { Content = "Undo", IsEnabled = false };

        ToolTipService.SetToolTip(crop, "Crop using x,y,w,h pixels (origin top-left)");
        ToolTipService.SetToolTip(_interactiveCropButton, "Drag a rectangle on the image to crop");
        ToolTipService.SetToolTip(_applyCropButton, "Apply the dragged crop rectangle");
        ToolTipService.SetToolTip(_cancelCropButton, "Cancel interactive crop");
        ToolTipService.SetToolTip(resize, "Resize width/height with optional aspect lock");
        ToolTipService.SetToolTip(adjust, "Brightness / contrast / saturation / levels");
        ToolTipService.SetToolTip(bgRemove, "Remove solid background / extract subject (corner flood-fill)");
        ToolTipService.SetToolTip(stamp, "Stamp a signature from the library onto the image");
        ToolTipService.SetToolTip(meta, "Image metadata, EXIF/IPTC/XMP, and GPS");
        ToolTipService.SetToolTip(ocrButton, "Run offline OCR on this image");
        ToolTipService.SetToolTip(rotate180, "Rotate 180°");
        ToolTipService.SetToolTip(orient, "Apply EXIF orientation into pixels");
        ToolTipService.SetToolTip(straighten, "Deskew / straighten scanned page (Magick)");
        ToolTipService.SetToolTip(batchOrient, "Batch folder: rotate/flip/orient, convert/export, or strip metadata");
        ToolTipService.SetToolTip(fullscreen, "Toggle window fullscreen");
        ToolTipService.SetToolTip(exportPng, "Export as PNG");
        ToolTipService.SetToolTip(exportJpeg, "Export as JPEG");
        ToolTipService.SetToolTip(convert, "Export as WebP, TIFF, BMP, GIF, AVIF, JP2, or HEIC");
        ToolTipService.SetToolTip(printImage, "Print this image (Ctrl+P)");
        ToolTipService.SetToolTip(copyImage, "Copy whole image to clipboard (Ctrl+C; selection copies when active)");
        ToolTipService.SetToolTip(pasteImage, "Paste image from clipboard (Ctrl+V)");
        ToolTipService.SetToolTip(_prevButton, "Previous image in folder");
        ToolTipService.SetToolTip(_nextButton, "Next image in folder");
        ToolTipService.SetToolTip(_slideshowButton, "Play/stop folder slideshow (3s, loops; Esc stops)");
        ToolTipService.SetToolTip(_animPlayButton, "Play/pause animated frames (GIF/WebP)");
        ToolTipService.SetToolTip(_animPrevButton, "Previous animation frame");
        ToolTipService.SetToolTip(_animNextButton, "Next animation frame");
        ToolTipService.SetToolTip(_animRestartButton, "Restart animation from first frame");
        ToolTipService.SetToolTip(_animExtractButton, "Save current frame as PNG");
        ToolTipService.SetToolTip(_animLoopBox, "Loop animation playback");
        ToolTipService.SetToolTip(_undoButton, "Undo last crop/resize/rotate/adjust (Ctrl+Z)");
        ToolTipService.SetToolTip(zoomOut, "Zoom out");
        ToolTipService.SetToolTip(zoomIn, "Zoom in");
        ToolTipService.SetToolTip(fit, "Fit image in view");
        ToolTipService.SetToolTip(actual, "Zoom to 100%");
        ToolTipService.SetToolTip(rotateLeft, "Rotate left 90°");
        ToolTipService.SetToolTip(rotateRight, "Rotate right 90°");
        ToolTipService.SetToolTip(flipH, "Flip horizontal");
        ToolTipService.SetToolTip(flipV, "Flip vertical");
        ToolTipService.SetToolTip(save, "Save image");
        ApplyToolbarAccessibleNames(
            zoomOut, zoomIn, fit, actual, rotateLeft, rotateRight, flipH, flipV, crop,
            _interactiveCropButton, _applyCropButton, _cancelCropButton, _cropAspectBox,
            _selectButton, _selectionKindBox, _selectAllButton, _invertSelButton, _deselectButton,
            _copySelButton, _cutSelButton, _pasteSelButton, _deleteSelButton, _cropSelButton,
            _drawButton, _flattenMarkupButton, resize, adjust, bgRemove, stamp, meta, ocrButton,
            rotate180, orient, straighten, batchOrient, fullscreen, save, exportPng, exportJpeg, convert,
            printImage, copyImage, pasteImage, _prevButton, _nextButton, _slideshowButton,
            _animPlayButton, _animPrevButton, _animNextButton, _animRestartButton, _animExtractButton,
            _animLoopBox, _undoButton);

        zoomOut.Click += async (_, _) => await SetZoomAsync(ImageZoomCalculator.ZoomOut(_zoom));
        zoomIn.Click += async (_, _) => await SetZoomAsync(ImageZoomCalculator.ZoomIn(_zoom));
        fit.Click += async (_, _) => await FitAsync();
        actual.Click += async (_, _) => await ZoomActualSizeAsync();
        rotateLeft.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), "Rotated left.");
        rotateRight.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), "Rotated right.");
        rotate180.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 180), "Rotated 180°.");
        orient.Click += async (_, _) => await MutateAsync(() => _processor.NormalizeOrientationAsync(_document), "Orientation normalized.");
        straighten.Click += async (_, _) => await MutateAsync(
            () => _processor.DeskewAsync(_document, thresholdPercent: 40, crop: true),
            "Straightened (deskew).");
        batchOrient.Click += async (_, _) => await BatchOrientFolderAsync();
        fullscreen.Click += (_, _) => ToggleFullscreen();
        flipH.Click += async (_, _) => await MutateAsync(() => _processor.FlipHorizontalAsync(_document), "Flipped horizontally.");
        flipV.Click += async (_, _) => await MutateAsync(() => _processor.FlipVerticalAsync(_document), "Flipped vertically.");
        crop.Click += async (_, _) => await CropAsync();
        _interactiveCropButton.Click += (_, _) => EnterCropMode();
        _applyCropButton.Click += async (_, _) => await ApplyInteractiveCropAsync();
        _cancelCropButton.Click += (_, _) => ExitCropMode();
        _selectButton.Click += (_, _) => ToggleSelectionMode();
        _selectionKindBox.SelectionChanged += (_, _) =>
        {
            _selectionKind = _selectionKindBox.SelectedIndex switch
            {
                1 => ImageSelectionKind.Ellipse,
                2 => ImageSelectionKind.Freeform,
                3 => ImageSelectionKind.Smart,
                _ => ImageSelectionKind.Rectangle,
            };
            SyncSelectionOverlayShape();
        };
        _selectAllButton.Click += (_, _) => SelectAllPixels();
        _invertSelButton.Click += (_, _) => InvertSelection();
        _deselectButton.Click += (_, _) => ClearPixelSelection();
        _copySelButton.Click += async (_, _) => await CopySelectionAsync();
        _cutSelButton.Click += async (_, _) => await CutSelectionAsync();
        _pasteSelButton.Click += async (_, _) => await PasteSelectionAsync();
        _deleteSelButton.Click += async (_, _) => await DeleteSelectionAsync();
        _cropSelButton.Click += async (_, _) => await CropToSelectionAsync();
        _drawButton.Click += async (_, _) => await ToggleDrawModeAsync();
        _flattenMarkupButton.Click += async (_, _) => await FlattenMarkupAsync();
        resize.Click += async (_, _) => await ResizeAsync();
        adjust.Click += async (_, _) => await AdjustAsync();
        bgRemove.Click += async (_, _) => await BackgroundToolsAsync();
        stamp.Click += async (_, _) => await StampSignatureAsync();
        meta.Click += async (_, _) => await ShowMetadataAsync();
        ocrButton.Click += async (_, _) => await RunOcrAsync();
        save.Click += async (_, _) => await SaveAsync();
        exportPng.Click += async (_, _) => await ExportAsync(ImageEncodeFormat.Png, ".png");
        exportJpeg.Click += async (_, _) => await ExportJpegAsync();
        convert.Click += async (_, _) => await ConvertAsync();
        printImage.Click += async (_, _) => await PrintImageAsync();
        copyImage.Click += async (_, _) => await CopyImageAsync();
        pasteImage.Click += async (_, _) => await PasteImageAsync();
        _prevButton.Click += async (_, _) => await NavigateSiblingAsync(-1);
        _nextButton.Click += async (_, _) => await NavigateSiblingAsync(1);
        _slideshowButton.Click += (_, _) => ToggleSlideshow();
        _animPlayButton.Click += (_, _) => ToggleAnimationPlayback();
        _animPrevButton.Click += async (_, _) => await StepAnimationFrameAsync(-1);
        _animNextButton.Click += async (_, _) => await StepAnimationFrameAsync(1);
        _animRestartButton.Click += async (_, _) => await RestartAnimationAsync();
        _animExtractButton.Click += async (_, _) => await SaveCurrentFrameAsync();
        _undoButton.Click += async (_, _) => await UndoEditAsync();
        KeyDown += ImageDocumentView_KeyDown;
        Unloaded += (_, _) =>
        {
            StopSlideshowTimerOnly();
            StopAnimationTimerOnly();
            ClearEditUndoStack();
        };

        _cropOverlay.PointerPressed += CropOverlay_PointerPressed;
        _cropOverlay.PointerMoved += CropOverlay_PointerMoved;
        _cropOverlay.PointerReleased += CropOverlay_PointerReleased;
        _cropOverlay.PointerCaptureLost += (_, _) =>
        {
            _cropDragging = false;
            _selectionMoving = false;
            _drawDragging = false;
        };
        _markupOverlay.PointerPressed += MarkupOverlay_PointerPressed;
        _markupOverlay.PointerMoved += MarkupOverlay_PointerMoved;
        _markupOverlay.PointerReleased += MarkupOverlay_PointerReleased;
        _markupOverlay.PointerCaptureLost += (_, _) => _drawDragging = false;

        var compact = false;
        try
        {
            compact = App.Services.GetService<ISettingsStore>()?.Current.CompactToolbar == true;
        }
        catch
        {
            // settings optional during construction
        }

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = compact ? 2 : 6,
            Padding = compact ? new Thickness(4, 2, 4, 2) : new Thickness(8),
            Children =
            {
                _prevButton, _nextButton, _slideshowButton,
                _animPlayButton, _animPrevButton, _animNextButton, _animRestartButton, _animLoopBox, _animFrameLabel, _animExtractButton,
                _undoButton, zoomOut, zoomIn, fit, actual, rotateLeft, rotateRight, rotate180, orient, straighten, batchOrient, fullscreen, flipH, flipV,
                _cropBox, crop, _interactiveCropButton, _cropAspectBox, _applyCropButton, _cancelCropButton,
                _selectButton, _selectionKindBox, _selectAllButton, _invertSelButton, _deselectButton, _copySelButton, _cutSelButton, _pasteSelButton, _deleteSelButton, _cropSelButton,
                _drawButton, _flattenMarkupButton,
                resize, adjust, bgRemove, stamp, meta, ocrButton, copyImage, pasteImage, save, exportPng, exportJpeg, convert, printImage, _status,
            },
        };
        _toolbar = toolbar;

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(180) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        var sidebar = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Padding = new Thickness(4),
        };
        sidebar.Children.Add(new TextBlock
        {
            Text = "Images",
            FontSize = 12,
            Opacity = 0.75,
            Margin = new Thickness(4, 0, 4, 4),
        });
        Grid.SetRow(_siblingList, 1);
        sidebar.Children.Add(_siblingList);
        body.Children.Add(sidebar);
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

        Loaded += ImageDocumentView_Loaded;
    }

    private async void ImageDocumentView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        RefreshSiblingList();
        RefreshAnimationChrome();
        await RefreshAsync();
        UpdateStatus();
        if (_viewState.IsSlideshowActive)
        {
            StartSlideshow(resume: true);
        }
        else if (_document.FrameCount > 1
            && App.Services.GetService<ISettingsStore>()?.Current.AnimationAutoplay == true)
        {
            StartAnimationPlayback();
            _status.Text = "Animation playing (autoplay).";
        }
    }

    private void RefreshSiblingList()
    {
        var path = _document.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            _siblings = Array.Empty<string>();
            _siblingList.ItemsSource = null;
            _prevButton.IsEnabled = false;
            _nextButton.IsEnabled = false;
            return;
        }

        _siblings = ImageFolderNavigator.ListSiblings(path);
        var names = _siblings.Select(System.IO.Path.GetFileName).ToList();
        _syncingList = true;
        try
        {
            _siblingList.ItemsSource = names;
            var index = ImageFolderNavigator.IndexOf(_siblings, path);
            if (index >= 0)
            {
                _siblingList.SelectedIndex = index;
            }
        }
        finally
        {
            _syncingList = false;
        }

        _prevButton.IsEnabled = ImageFolderNavigator.Previous(_siblings, path) is not null;
        _nextButton.IsEnabled = ImageFolderNavigator.Next(_siblings, path) is not null;
    }

    private async void SiblingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingList || _openSibling is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            return;
        }

        var index = _siblingList.SelectedIndex;
        if (index < 0 || index >= _siblings.Count)
        {
            return;
        }

        var target = _siblings[index];
        if (string.Equals(
                System.IO.Path.GetFullPath(target),
                System.IO.Path.GetFullPath(_document.Path),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _openSibling(target);
    }

    private async Task NavigateSiblingAsync(int delta)
    {
        if (_openSibling is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            _status.Text = "Folder navigation unavailable.";
            return;
        }

        var target = delta < 0
            ? ImageFolderNavigator.Previous(_siblings, _document.Path)
            : ImageFolderNavigator.Next(_siblings, _document.Path);
        if (target is null)
        {
            _status.Text = delta < 0 ? "Already at first image." : "Already at last image.";
            return;
        }

        await _openSibling(target);
    }

    private void ImageSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_cropMode || _selectionMode || _drawMode || e.GetCurrentPoint(_scrollViewer).Properties.IsRightButtonPressed)
        {
            return;
        }

        // Don't steal clicks from toolbar; only gesture when not cropping.
        _navDragging = true;
        _navStart = e.GetCurrentPoint(_scrollViewer).Position;
        _scrollViewer.CapturePointer(e.Pointer);
    }

    private void ImageSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Threshold checked on release to avoid fighting ScrollViewer pan.
    }

    private async void ImageSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_navDragging)
        {
            return;
        }

        _navDragging = false;
        try { _scrollViewer.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }

        if (_cropMode)
        {
            return;
        }

        var end = e.GetCurrentPoint(_scrollViewer).Position;
        var dx = end.X - _navStart.X;
        var dy = end.Y - _navStart.Y;
        const double minSwipe = 80;
        if (Math.Abs(dx) < minSwipe || Math.Abs(dx) < Math.Abs(dy) * 1.5)
        {
            return;
        }

        // Swipe left → next; swipe right → previous (natural photo-viewer feel).
        await NavigateSiblingAsync(dx < 0 ? 1 : -1);
    }

    private void ToggleSlideshow()
    {
        if (_viewState.IsSlideshowActive)
        {
            StopSlideshow();
            _status.Text = "Slideshow stopped.";
            return;
        }

        if (_siblings.Count < 2 || _openSibling is null)
        {
            _status.Text = "Slideshow needs at least two images in the folder.";
            return;
        }

        StartSlideshow(resume: false);
    }

    private void StartSlideshow(bool resume)
    {
        if (_siblings.Count < 2 || _openSibling is null)
        {
            _viewState.IsSlideshowActive = false;
            RefreshSlideshowChrome();
            return;
        }

        _viewState.IsSlideshowActive = true;
        StopAnimationPlayback();
        StopSlideshowTimerOnly();
        _slideshowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _slideshowTimer.Tick += async (_, _) => await AdvanceSlideshowAsync();
        _slideshowTimer.Start();
        RefreshSlideshowChrome();
        if (!resume)
        {
            _status.Text = "Slideshow on — advances every 3s (Esc to stop).";
        }
    }

    private void StopSlideshow()
    {
        _viewState.IsSlideshowActive = false;
        StopSlideshowTimerOnly();
        RefreshSlideshowChrome();
    }

    private void StopSlideshowTimerOnly()
    {
        if (_slideshowTimer is null)
        {
            return;
        }

        _slideshowTimer.Stop();
        _slideshowTimer = null;
    }

    private void RefreshAnimationChrome()
    {
        var animated = _document.FrameCount > 1;
        var visibility = animated ? Visibility.Visible : Visibility.Collapsed;
        _animPlayButton.Visibility = visibility;
        _animPrevButton.Visibility = visibility;
        _animNextButton.Visibility = visibility;
        _animRestartButton.Visibility = visibility;
        _animExtractButton.Visibility = visibility;
        _animLoopBox.Visibility = visibility;
        _animFrameLabel.Visibility = visibility;
        if (!animated)
        {
            StopAnimationPlayback();
            return;
        }

        _animPlayButton.Content = _animationPlaying ? "Pause" : "Play";
        _animFrameLabel.Text = AnimationFrameNav.FormatLabel(
            _document.CurrentFrameIndex,
            _document.FrameCount);
    }

    private void ToggleAnimationPlayback()
    {
        if (_document.FrameCount <= 1)
        {
            return;
        }

        if (_animationPlaying)
        {
            PauseAnimation();
            _status.Text = "Animation paused.";
            return;
        }

        StartAnimationPlayback();
        _status.Text = "Animation playing.";
    }

    private void StartAnimationPlayback()
    {
        if (_document.FrameCount <= 1)
        {
            return;
        }

        if (_viewState.IsSlideshowActive)
        {
            StopSlideshow();
        }

        _animationPlaying = true;
        _animationLoopsCompleted = 0;
        RefreshAnimationChrome();
        ScheduleNextAnimationTick();
    }

    private void PauseAnimation()
    {
        _animationPlaying = false;
        StopAnimationTimerOnly();
        RefreshAnimationChrome();
    }

    private void StopAnimationPlayback()
    {
        _animationPlaying = false;
        StopAnimationTimerOnly();
        RefreshAnimationChrome();
    }

    private void StopAnimationTimerOnly()
    {
        if (_animationTimer is null)
        {
            return;
        }

        _animationTimer.Stop();
        _animationTimer = null;
    }

    private void ScheduleNextAnimationTick()
    {
        StopAnimationTimerOnly();
        if (!_animationPlaying || _document.FrameCount <= 1)
        {
            return;
        }

        var delayMs = _document.GetFrameDelayMilliseconds(_document.CurrentFrameIndex);
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        _animationTimer.Tick += async (_, _) => await AdvanceAnimationFrameAsync();
        _animationTimer.Start();
    }

    private async Task AdvanceAnimationFrameAsync()
    {
        if (!_animationPlaying || _document.FrameCount <= 1)
        {
            StopAnimationPlayback();
            return;
        }

        var next = AnimationFrameNav.NextPlaybackFrame(
            _document.CurrentFrameIndex,
            _document.FrameCount,
            loopEnabled: _animLoopBox.IsChecked == true,
            animationIterations: _document.AnimationIterations,
            ref _animationLoopsCompleted);

        if (next is null)
        {
            PauseAnimation();
            _status.Text = _animLoopBox.IsChecked == true
                ? "Animation finished looping."
                : $"Animation finished · frame {_document.FrameCount}/{_document.FrameCount}.";
            return;
        }

        await _document.SetCurrentFrameAsync(next.Value);
        await RefreshAsync();
        RefreshAnimationChrome();
        UpdateStatus();
        ScheduleNextAnimationTick();
    }

    private async Task StepAnimationFrameAsync(int delta)
    {
        if (_document.FrameCount <= 1)
        {
            return;
        }

        PauseAnimation();
        var next = AnimationFrameNav.WrapStep(_document.CurrentFrameIndex, delta, _document.FrameCount);
        if (next < 0)
        {
            return;
        }

        await _document.SetCurrentFrameAsync(next);
        await RefreshAsync();
        RefreshAnimationChrome();
        UpdateStatus();
        _status.Text = AnimationFrameNav.FormatLabel(
            _document.CurrentFrameIndex,
            _document.FrameCount) + ".";
    }

    private async Task RestartAnimationAsync()
    {
        if (_document.FrameCount <= 1)
        {
            return;
        }

        await _document.SetCurrentFrameAsync(0);
        await RefreshAsync();
        _animationLoopsCompleted = 0;
        StartAnimationPlayback();
        _status.Text = "Animation restarted.";
    }

    private async Task SaveCurrentFrameAsync()
    {
        if (_document.FrameCount <= 1)
        {
            return;
        }

        try
        {
            PauseAnimation();
            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable for save picker.");
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add("PNG", [".png"]);
            var baseName = string.IsNullOrWhiteSpace(_document.Path)
                ? "frame"
                : System.IO.Path.GetFileNameWithoutExtension(_document.Path);
            picker.SuggestedFileName = $"{baseName}-frame{_document.CurrentFrameIndex + 1}.png";
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "Save frame cancelled.";
                return;
            }

            var buffer = await _document.ExtractFrameAsync(_document.CurrentFrameIndex);
            await _encoder.WriteBgraAsync(
                buffer.BgraPixels,
                buffer.Width,
                buffer.Height,
                file.Path,
                ImageEncodeFormat.Png);
            _status.Text = $"Saved frame {_document.CurrentFrameIndex + 1} → {file.Name}";
        }
        catch (Exception ex)
        {
            _status.Text = "Save frame failed: " + ex.Message;
        }
    }

    private void RefreshSlideshowChrome()
    {
        var active = _viewState.IsSlideshowActive;
        _slideshowButton.Content = active ? "Stop show" : "Slideshow";
        _slideshowButton.Background = active
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 140, 0))
            : null;
    }

    private async Task AdvanceSlideshowAsync()
    {
        if (!_viewState.IsSlideshowActive || _openSibling is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            return;
        }

        if (_siblings.Count == 0)
        {
            RefreshSiblingList();
        }

        if (_siblings.Count < 2)
        {
            StopSlideshow();
            _status.Text = "Slideshow stopped — not enough images.";
            return;
        }

        var index = ImageFolderNavigator.IndexOf(_siblings, _document.Path);
        var nextIndex = index < 0 ? 0 : (index + 1) % _siblings.Count;
        var target = _siblings[nextIndex];
        if (string.Equals(
                System.IO.Path.GetFullPath(target),
                System.IO.Path.GetFullPath(_document.Path),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _openSibling(target);
    }

    private void ImageDocumentView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_drawMode)
            {
                ExitDrawMode();
                _status.Text = "Draw mode off.";
                e.Handled = true;
                return;
            }

            if (_selectionMode)
            {
                ExitSelectionMode(keepSelection: false);
                _status.Text = "Selection mode off.";
                e.Handled = true;
                return;
            }

            if (_viewState.IsSlideshowActive)
            {
                StopSlideshow();
                _status.Text = "Slideshow stopped.";
                e.Handled = true;
                return;
            }

            if (_animationPlaying)
            {
                PauseAnimation();
                _status.Text = "Animation paused.";
                e.Handled = true;
                return;
            }
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrl && e.Key == Windows.System.VirtualKey.Z)
        {
            if (_markupStrokes.Count > 0 || _markupShapes.Count > 0)
            {
                UndoMarkupItem();
            }
            else
            {
                _ = UndoEditAsync();
            }

            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.C)
        {
            _ = CopyImageAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.P)
        {
            _ = PrintImageAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.S)
        {
            var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            _ = SaveDocumentAsync(saveAs: shift);
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (ctrl && (e.Key == Windows.System.VirtualKey.Add || e.Key == (Windows.System.VirtualKey)187))
        {
            _ = SetZoomAsync(ImageZoomCalculator.ZoomIn(_zoom));
            e.Handled = true;
            return;
        }

        if (ctrl && (e.Key == Windows.System.VirtualKey.Subtract || e.Key == (Windows.System.VirtualKey)189))
        {
            _ = SetZoomAsync(ImageZoomCalculator.ZoomOut(_zoom));
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.Number0)
        {
            _ = FitAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.X && _pixelSelection is not null)
        {
            _ = CutSelectionAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.V)
        {
            _ = PasteImageAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.A && _selectionMode)
        {
            SelectAllPixels();
            e.Handled = true;
            return;
        }

        if (_selectionMode && _pixelSelection is { } sel && !ctrl)
        {
            var dx = e.Key switch
            {
                Windows.System.VirtualKey.Left => -1,
                Windows.System.VirtualKey.Right => 1,
                _ => 0,
            };
            var dy = e.Key switch
            {
                Windows.System.VirtualKey.Up => -1,
                Windows.System.VirtualKey.Down => 1,
                _ => 0,
            };
            if (dx != 0 || dy != 0)
            {
                _ = NudgeSelectionAsync(sel, dx, dy);
                e.Handled = true;
            }
        }
    }

    private async Task NudgeSelectionAsync(ImageRect sel, int dx, int dy)
    {
        if (_selectionInverted)
        {
            _status.Text = "Cannot move an inverted selection — Invert again first.";
            return;
        }

        var destX = Math.Clamp(sel.X + dx, 0, Math.Max(0, _document.PixelWidth - sel.Width));
        var destY = Math.Clamp(sel.Y + dy, 0, Math.Max(0, _document.PixelHeight - sel.Height));
        if (destX == sel.X && destY == sel.Y)
        {
            return;
        }

        await MutateAsync(
            () => _processor.MoveRectAsync(_document, sel, destX, destY, _selectionKind, CurrentLassoOrNull()),
            $"Moved selection to ({destX},{destY}).");
        var dxPix = destX - sel.X;
        var dyPix = destY - sel.Y;
        TranslateLasso(dxPix, dyPix);
        SetPixelSelection(new ImageRect(destX, destY, sel.Width, sel.Height));
    }

    private bool IsLassoKind() =>
        _selectionKind is ImageSelectionKind.Freeform or ImageSelectionKind.Smart;

    private IReadOnlyList<ImageMarkupPoint>? CurrentLassoOrNull() =>
        IsLassoKind() && _lassoDocPoints.Count >= 3
            ? _lassoDocPoints.ToList()
            : null;

    private void TranslateLasso(int dx, int dy)
    {
        if (_lassoDocPoints.Count == 0 || (dx == 0 && dy == 0))
        {
            return;
        }

        for (var i = 0; i < _lassoDocPoints.Count; i++)
        {
            var p = _lassoDocPoints[i];
            _lassoDocPoints[i] = new ImageMarkupPoint(p.X + dx, p.Y + dy);
        }
    }

    private async Task CopyImageAsync()
    {
        if (_pixelSelection is { } sel && sel.Width >= 1 && sel.Height >= 1)
        {
            await CopySelectionAsync();
            return;
        }

        try
        {
            var full = new ImageRect(0, 0, _document.PixelWidth, _document.PixelHeight);
            var buffer = await _processor.ExtractRectAsync(_document, full);
            _selectionClipboard = buffer;
            var temp = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "glyph-img-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                await _encoder.WriteBgraAsync(
                    buffer.BgraPixels,
                    buffer.Width,
                    buffer.Height,
                    temp,
                    ImageEncodeFormat.Png);
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(temp);
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                _status.Text = $"Copied image {buffer.Width}×{buffer.Height}.";
            }
            finally
            {
                try { System.IO.File.Delete(temp); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Copy image failed: " + ex.Message;
        }
    }

    /// <summary>Paste system clipboard image (or selection clipboard) into this document.</summary>
    public Task PasteFromClipboardAsync() => PasteImageAsync();

    private async Task PasteImageAsync()
    {
        try
        {
            var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
            {
                var streamRef = await content.GetBitmapAsync();
                using var stream = await streamRef.OpenReadAsync();
                var temp = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "glyph-paste-" + Guid.NewGuid().ToString("N") + ".bmp");
                try
                {
                    await using (var file = System.IO.File.Create(temp))
                    {
                        var input = stream.AsStreamForRead();
                        await input.CopyToAsync(file);
                    }

                    var destX = _pixelSelection?.X ?? 0;
                    var destY = _pixelSelection?.Y ?? 0;
                    await MutateAsync(
                        () => _processor.PasteFileAsync(_document, temp, destX, destY),
                        $"Pasted clipboard image at ({destX},{destY}).");
                    return;
                }
                finally
                {
                    try { System.IO.File.Delete(temp); } catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex)
        {
            // Fall through to internal selection clipboard.
            if (_selectionClipboard is null)
            {
                _status.Text = "Paste failed: " + ex.Message;
                return;
            }
        }

        await PasteSelectionAsync();
    }

    private async Task CopySelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = "Make a selection first.";
            return;
        }

        try
        {
            var buffer = await _processor.ExtractRectAsync(
                _document,
                sel,
                _selectionKind,
                CurrentLassoOrNull(),
                _selectionInverted);
            _selectionClipboard = buffer;
            var temp = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "glyph-sel-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                await _encoder.WriteBgraAsync(
                    buffer.BgraPixels,
                    buffer.Width,
                    buffer.Height,
                    temp,
                    ImageEncodeFormat.Png);
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(temp);
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                var label = _selectionInverted ? "inverted selection" : "selection";
                _status.Text = $"Copied {label} {buffer.Width}×{buffer.Height}.";
            }
            finally
            {
                try { System.IO.File.Delete(temp); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Copy selection failed: " + ex.Message;
        }
    }

    private async Task CutSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = "Make a selection first.";
            return;
        }

        await CopySelectionAsync();
        if (_selectionClipboard is null)
        {
            return;
        }

        await MutateAsync(
            () => _processor.ClearRectAsync(
                _document,
                sel,
                transparent: true,
                _selectionKind,
                CurrentLassoOrNull(),
                _selectionInverted),
            _selectionInverted
                ? $"Cut inverted selection (kept {sel.Width}×{sel.Height} hole)."
                : $"Cut selection {sel.Width}×{sel.Height}.");
    }

    private async Task PasteSelectionAsync()
    {
        if (_selectionClipboard is null)
        {
            _status.Text = "Clipboard is empty — copy or cut a selection first.";
            return;
        }

        var destX = _pixelSelection?.X ?? 0;
        var destY = _pixelSelection?.Y ?? 0;
        var clip = _selectionClipboard;
        await MutateAsync(
            () => _processor.PasteRectAsync(_document, clip, destX, destY),
            $"Pasted {clip.Width}×{clip.Height} at ({destX},{destY}).");
    }

    private async Task DeleteSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = "Make a selection first.";
            return;
        }

        await MutateAsync(
            () => _processor.ClearRectAsync(
                _document,
                sel,
                transparent: true,
                _selectionKind,
                CurrentLassoOrNull(),
                _selectionInverted),
            _selectionInverted
                ? $"Cleared outside selection (kept {sel.Width}×{sel.Height})."
                : $"Cleared selection {sel.Width}×{sel.Height}.");
    }

    private async Task CropToSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = "Make a selection first.";
            return;
        }

        if (_selectionInverted)
        {
            _status.Text = "Cannot crop an inverted selection — Invert again or Deselect.";
            return;
        }

        ExitSelectionMode(keepSelection: false);
        await MutateAsync(
            () => _processor.CropAsync(_document, sel),
            $"Cropped to selection {sel.Width}×{sel.Height}.");
    }

    private async Task MutateAsync(Func<Task> mutation, string okStatus)
    {
        IImageEditCheckpoint? checkpoint = null;
        try
        {
            PauseAnimation();
            checkpoint = _document.CaptureCheckpoint();
            await mutation();
            PushUndo(checkpoint);
            checkpoint = null;
            await RefreshAsync();
            RefreshAnimationChrome();
            UpdateStatus();
            _status.Text = okStatus;
        }
        catch (Exception ex)
        {
            checkpoint?.Dispose();
            _status.Text = "Edit failed: " + ex.Message;
        }
    }

    private void ClearEditUndoStack()
    {
        foreach (var checkpoint in _editUndoStack)
        {
            checkpoint.Dispose();
        }

        _editUndoStack.Clear();
        _undoButton.IsEnabled = false;
    }

    private void PushUndo(IImageEditCheckpoint checkpoint)
    {
        _editUndoStack.Add(checkpoint);
        while (_editUndoStack.Count > MaxEditUndo)
        {
            _editUndoStack[0].Dispose();
            _editUndoStack.RemoveAt(0);
        }

        _undoButton.IsEnabled = _editUndoStack.Count > 0;
        _onEdited?.Invoke();
    }

    /// <summary>True when image edits or unflattened markup have not been saved (F50).</summary>
    public bool HasUnsavedEdits =>
        _editUndoStack.Count > 0 || _markupStrokes.Count > 0 || _markupShapes.Count > 0;

    public void ClearUnsavedEdits() => ClearEditUndoStack();

    /// <summary>Write current pixels to the crash-recovery store without changing the document path.</summary>
    public async Task WriteRecoverySnapshotAsync(
        Glyph.Infrastructure.Session.ICrashRecoveryStore store,
        string originalPath,
        CancellationToken cancellationToken = default)
    {
        var ext = System.IO.Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".png";
        }

        var temp = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "glyph-recovery-" + Guid.NewGuid().ToString("N") + ext);
        var previousPath = _document.Path;
        try
        {
            var (format, _) = GuessSaveFormat(_document.FormatName);
            await _encoder.SaveAsAsync(_document, temp, format, cancellationToken: cancellationToken);
            _document.Path = previousPath;
            await using var stream = File.OpenRead(temp);
            await store.SaveSnapshotAsync(originalPath, stream, ext, cancellationToken);
        }
        finally
        {
            _document.Path = previousPath;
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }

    private async Task UndoEditAsync()
    {
        if (_editUndoStack.Count == 0)
        {
            _status.Text = "Nothing to undo.";
            return;
        }

        try
        {
            var checkpoint = _editUndoStack[^1];
            _editUndoStack.RemoveAt(_editUndoStack.Count - 1);
            _document.RestoreCheckpoint(checkpoint);
            _undoButton.IsEnabled = _editUndoStack.Count > 0;
            await RefreshAsync();
            UpdateStatus();
            _status.Text = "Edit undone.";
        }
        catch (Exception ex)
        {
            _status.Text = "Undo failed: " + ex.Message;
        }
    }

    private void ToggleFullscreen()
    {
        if (App.CurrentApp.MainWindowInstance is MainWindow window)
        {
            window.ToggleFullscreen();
            _status.Text = "Fullscreen toggled.";
            return;
        }

        _status.Text = "Fullscreen unavailable.";
    }

    public void ToggleToolbarVisibility()
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.Visibility = _toolbar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        _status.Text = _toolbar.Visibility == Visibility.Visible ? "Toolbar shown." : "Toolbar hidden.";
    }

    public bool IsToolbarVisible => _toolbar?.Visibility != Visibility.Collapsed;

    private async Task RefreshAsync()
    {
        var generation = ++_refreshGeneration;
        var nativeMax = Math.Max(_document.PixelWidth, _document.PixelHeight);
        var targetEdge = ImageZoomCalculator.DecodeTargetEdge(nativeMax, _zoom);

        // Progressive decode for large rasters (F58-02/03): quick preview, then refine.
        var previewEdge = ImageZoomCalculator.ProgressivePreviewEdge;
        var needsProgressive = ImageZoomCalculator.NeedsProgressivePreview(nativeMax, targetEdge);

        if (needsProgressive)
        {
            await ApplyPixelBufferAsync(await _document.GetPixelsAsync(previewEdge), generation);
            if (generation != _refreshGeneration)
            {
                return;
            }

            _status.Text = $"Loading full preview… ({nativeMax:N0}px edge)";
            await Task.Yield();
        }

        if (generation != _refreshGeneration)
        {
            return;
        }

        await ApplyPixelBufferAsync(await _document.GetPixelsAsync(targetEdge), generation);
        if (generation != _refreshGeneration)
        {
            return;
        }

        _viewState.Zoom = _zoom;
        RebuildMarkupOverlay();
        if (_cropMode)
        {
            ClearCropSelection();
        }

        await ApplyImageAccessibleNameAsync();
        if (needsProgressive || nativeMax > 4096)
        {
            UpdateStatus();
        }
    }

    private async Task ApplyPixelBufferAsync(ImagePixelBuffer buffer, int generation)
    {
        if (generation != _refreshGeneration)
        {
            return;
        }

        var bitmap = new WriteableBitmap(buffer.Width, buffer.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            await stream.WriteAsync(buffer.BgraPixels, 0, buffer.BgraPixels.Length);
        }

        bitmap.Invalidate();
        if (generation != _refreshGeneration)
        {
            return;
        }

        _image.Source = bitmap;
        _image.Width = buffer.Width;
        _image.Height = buffer.Height;
        _displayWidth = buffer.Width;
        _displayHeight = buffer.Height;
        _cropOverlay.Width = buffer.Width;
        _cropOverlay.Height = buffer.Height;
        _markupOverlay.Width = buffer.Width;
        _markupOverlay.Height = buffer.Height;
        _imageSurface.Width = buffer.Width;
        _imageSurface.Height = buffer.Height;
    }

    private async Task ApplyImageAccessibleNameAsync()
    {
        try
        {
            var meta = await _document.GetMetadataAsync();
            var name = !string.IsNullOrWhiteSpace(meta.Description)
                ? meta.Description!
                : !string.IsNullOrWhiteSpace(meta.Title)
                    ? meta.Title!
                    : !string.IsNullOrWhiteSpace(_document.Path)
                        ? System.IO.Path.GetFileName(_document.Path)
                        : "Image";
            AutomationProperties.SetName(_image, name);
            AutomationProperties.SetName(this, name);
        }
        catch
        {
            var fallback = !string.IsNullOrWhiteSpace(_document.Path)
                ? System.IO.Path.GetFileName(_document.Path)
                : "Image";
            AutomationProperties.SetName(_image, fallback!);
        }
    }

    private async Task SetZoomAsync(double zoom)
    {
        _zoom = ImageZoomCalculator.Clamp(zoom);
        await RefreshAsync();
        UpdateStatus();
    }

    private async void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
        {
            return;
        }

        var delta = e.GetCurrentPoint(_scrollViewer).Properties.MouseWheelDelta;
        await SetZoomAsync(ImageZoomCalculator.ApplyWheelZoom(_zoom, delta));
        e.Handled = true;
    }

    private async void ScrollViewer_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (Math.Abs(e.Delta.Scale - 1.0) < 0.001)
        {
            return;
        }

        await SetZoomAsync(ImageZoomCalculator.ApplyManipulationScale(_zoom, e.Delta.Scale));
        e.Handled = true;
    }

    private async Task ZoomActualSizeAsync()
    {
        var meaning = "Pixels";
        try
        {
            meaning = App.Services.GetService<ISettingsStore>()?.Current.Zoom100Meaning ?? "Pixels";
        }
        catch
        {
            // DI may be unavailable.
        }

        if (string.Equals(meaning, "Print", StringComparison.OrdinalIgnoreCase))
        {
            double imageDpi = 96;
            try
            {
                var meta = await _document.GetMetadataAsync();
                if (meta.DpiX is > 0)
                {
                    imageDpi = meta.DpiX.Value;
                }
                else if (meta.DpiY is > 0)
                {
                    imageDpi = meta.DpiY.Value;
                }
            }
            catch
            {
                // Fall back to 96 DPI.
            }

            var screenDpi = 96.0 * (XamlRoot?.RasterizationScale ?? 1.0);
            await SetZoomAsync(ImageZoomCalculator.ActualSizePrint(screenDpi, imageDpi));
            return;
        }

        await SetZoomAsync(ImageZoomCalculator.ActualSizePixels());
    }

    private static int ResolveDefaultInterpolationIndex()
    {
        try
        {
            var name = App.Services.GetService<ISettingsStore>()?.Current.DefaultInterpolation;
            return ImageResizeDialogMath.ComboIndexFromPreferenceName(name);
        }
        catch
        {
            return 0;
        }
    }

    private async Task FitAsync()
    {
        await SetZoomAsync(ImageZoomCalculator.Fit(
            _scrollViewer.ActualWidth,
            _scrollViewer.ActualHeight,
            _document.PixelWidth,
            _document.PixelHeight));
    }

    private async Task CropAsync()
    {
        if (ImageCropRectParser.TryParse(_cropBox.Text) is not { } rect)
        {
            _status.Text = "Crop needs x,y,w,h integers.";
            return;
        }

        await MutateAsync(
            () => _processor.CropAsync(_document, rect),
            $"Cropped to {rect.Width}×{rect.Height}.");
    }

    private void EnterCropMode()
    {
        if (_drawMode)
        {
            ExitDrawMode();
        }

        if (_selectionMode)
        {
            ExitSelectionMode(keepSelection: true);
        }

        _cropMode = true;
        _cropOverlay.IsHitTestVisible = true;
        _interactiveCropButton.Visibility = Visibility.Collapsed;
        _cropAspectBox.Visibility = Visibility.Visible;
        _applyCropButton.Visibility = Visibility.Visible;
        _cancelCropButton.Visibility = Visibility.Visible;
        _cropRect.Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 200, 0));
        _cropRect.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 200, 0));
        ClearCropSelection();
        _status.Text = "Drag on the image to select a crop region (aspect from dropdown).";
    }

    private void ExitCropMode()
    {
        _cropMode = false;
        _cropDragging = false;
        if (!_selectionMode)
        {
            _cropOverlay.IsHitTestVisible = false;
        }

        _interactiveCropButton.Visibility = Visibility.Visible;
        _cropAspectBox.Visibility = Visibility.Collapsed;
        _applyCropButton.Visibility = Visibility.Collapsed;
        _cancelCropButton.Visibility = Visibility.Collapsed;
        ClearCropSelection();
        UpdateStatus();
    }

    private void ToggleSelectionMode()
    {
        if (_selectionMode)
        {
            ExitSelectionMode(keepSelection: false);
            _status.Text = "Selection mode off.";
            return;
        }

        if (_cropMode)
        {
            ExitCropMode();
        }

        if (_drawMode)
        {
            ExitDrawMode();
        }

        _selectionMode = true;
        _cropOverlay.IsHitTestVisible = true;
        _selectButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 140, 0));
        _selectionKindBox.Visibility = Visibility.Visible;
        _selectAllButton.Visibility = Visibility.Visible;
        _invertSelButton.Visibility = Visibility.Visible;
        _deselectButton.Visibility = Visibility.Visible;
        _copySelButton.Visibility = Visibility.Visible;
        _cutSelButton.Visibility = Visibility.Visible;
        _pasteSelButton.Visibility = Visibility.Visible;
        _deleteSelButton.Visibility = Visibility.Visible;
        _cropSelButton.Visibility = Visibility.Visible;
        _cropRect.Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 144, 255));
        _cropRect.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255));
        ClearCropSelection();
        _pixelSelection = null;
        _selectionInverted = false;
        ApplySelectionChrome();
        _status.Text = "Selection mode — drag a shape; drag inside to move (arrow keys nudge; Esc exits).";
    }

    private void ExitSelectionMode(bool keepSelection)
    {
        _selectionMode = false;
        _cropDragging = false;
        _selectionMoving = false;
        _moveSourcePixels = null;
        if (!_cropMode)
        {
            _cropOverlay.IsHitTestVisible = false;
        }

        _selectButton.Background = null;
        _selectionKindBox.Visibility = Visibility.Collapsed;
        _selectAllButton.Visibility = Visibility.Collapsed;
        _invertSelButton.Visibility = Visibility.Collapsed;
        _deselectButton.Visibility = Visibility.Collapsed;
        _copySelButton.Visibility = Visibility.Collapsed;
        _cutSelButton.Visibility = Visibility.Collapsed;
        _pasteSelButton.Visibility = Visibility.Collapsed;
        _deleteSelButton.Visibility = Visibility.Collapsed;
        _cropSelButton.Visibility = Visibility.Collapsed;
        if (!keepSelection)
        {
            ClearPixelSelection();
        }
    }

    private void Image_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var path = _document.Path;
        if (!ImageDragSemantics.CanDragFile(path))
        {
            args.Cancel = true;
            return;
        }

        // Deferred StorageItems so Explorer and other Glyph windows/tabs receive the file on drop (F59-05).
        var sourcePath = path!;
        args.Data.SetDataProvider(
            Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems,
            async request =>
            {
                var deferral = request.GetDeferral();
                try
                {
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
                    request.SetData(new Windows.Storage.IStorageItem[] { file });
                }
                catch
                {
                    request.SetData(Array.Empty<Windows.Storage.IStorageItem>());
                }
                finally
                {
                    deferral.Complete();
                }
            });
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        _status.Text = ImageDragSemantics.DraggingStatus;
    }

    private void SelectAllPixels()
    {
        if (!_selectionMode)
        {
            ToggleSelectionMode();
        }

        _lassoDocPoints.Clear();
        ClearLassoPolyline();
        _selectionInverted = false;
        if (IsLassoKind())
        {
            _selectionKind = ImageSelectionKind.Rectangle;
            _selectionKindBox.SelectedIndex = 0;
        }

        SetPixelSelection(new ImageRect(0, 0, _document.PixelWidth, _document.PixelHeight));
        ApplySelectionChrome();
        _status.Text = $"Selected all {_document.PixelWidth}×{_document.PixelHeight}.";
    }

    private void ImageSurface_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement target)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var copyItem = new MenuFlyoutItem { Text = _pixelSelection is not null ? "Copy selection" : "Copy image" };
        copyItem.Click += async (_, _) => await CopyImageAsync();
        flyout.Items.Add(copyItem);

        if (_pixelSelection is not null)
        {
            var cutItem = new MenuFlyoutItem { Text = "Cut selection" };
            cutItem.Click += async (_, _) => await CutSelectionAsync();
            flyout.Items.Add(cutItem);
            var deselectItem = new MenuFlyoutItem { Text = "Deselect" };
            deselectItem.Click += (_, _) => ClearPixelSelection();
            flyout.Items.Add(deselectItem);
        }
        else
        {
            var selectAllItem = new MenuFlyoutItem { Text = "Select all" };
            selectAllItem.Click += (_, _) => SelectAllPixels();
            flyout.Items.Add(selectAllItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var pasteItem = new MenuFlyoutItem { Text = "Paste" };
        pasteItem.Click += async (_, _) => await PasteImageAsync();
        flyout.Items.Add(pasteItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var fitItem = new MenuFlyoutItem { Text = "Fit" };
        fitItem.Click += async (_, _) => await FitAsync();
        flyout.Items.Add(fitItem);
        var rotateLeftItem = new MenuFlyoutItem { Text = "Rotate left" };
        rotateLeftItem.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), "Rotated left.");
        flyout.Items.Add(rotateLeftItem);
        var rotateRightItem = new MenuFlyoutItem { Text = "Rotate right" };
        rotateRightItem.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), "Rotated right.");
        flyout.Items.Add(rotateRightItem);

        flyout.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    private void InvertSelection()
    {
        if (!_selectionMode)
        {
            ToggleSelectionMode();
        }

        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = "Make a selection first.";
            return;
        }

        var fullImage = sel.X == 0
            && sel.Y == 0
            && sel.Width == _document.PixelWidth
            && sel.Height == _document.PixelHeight
            && _selectionKind == ImageSelectionKind.Rectangle
            && _lassoDocPoints.Count == 0;

        if (!_selectionInverted && fullImage)
        {
            ClearPixelSelection();
            _status.Text = "Inverted full selection → empty.";
            return;
        }

        _selectionInverted = !_selectionInverted;
        ApplySelectionChrome();
        _status.Text = _selectionInverted
            ? $"Selection inverted — copy/cut/delete apply outside {sel.Width}×{sel.Height}."
            : $"Selection restored ({SelectionKindLabel()} {sel.Width}×{sel.Height}).";
    }

    private void ApplySelectionChrome()
    {
        var stroke = _selectionInverted
            ? Windows.UI.Color.FromArgb(255, 220, 20, 60)
            : Windows.UI.Color.FromArgb(255, 30, 144, 255);
        var fill = _selectionInverted
            ? Windows.UI.Color.FromArgb(50, 220, 20, 60)
            : Windows.UI.Color.FromArgb(40, 30, 144, 255);
        var strokeBrush = new SolidColorBrush(stroke);
        var fillBrush = new SolidColorBrush(fill);
        _cropRect.Stroke = strokeBrush;
        _cropRect.Fill = fillBrush;
        _cropRect.StrokeDashArray = _selectionInverted
            ? new DoubleCollection { 4, 3 }
            : null;
        _selectionEllipse.Stroke = strokeBrush;
        _selectionEllipse.Fill = fillBrush;
        _selectionEllipse.StrokeDashArray = _selectionInverted
            ? new DoubleCollection { 4, 3 }
            : null;
        if (_lassoPolyline is not null)
        {
            _lassoPolyline.Stroke = strokeBrush;
            _lassoPolyline.Fill = fillBrush;
            _lassoPolyline.StrokeDashArray = _selectionInverted
                ? new DoubleCollection { 4, 3 }
                : null;
        }
    }

    private void SetPixelSelection(ImageRect pixels)
    {
        _pixelSelection = pixels;
        if (_displayWidth <= 0 || _displayHeight <= 0 || _document.PixelWidth <= 0 || _document.PixelHeight <= 0)
        {
            return;
        }

        var scaleX = _displayWidth / (double)_document.PixelWidth;
        var scaleY = _displayHeight / (double)_document.PixelHeight;
        PlaceSelectionOverlay(
            pixels.X * scaleX,
            pixels.Y * scaleY,
            Math.Max(1, pixels.Width * scaleX),
            Math.Max(1, pixels.Height * scaleY));
    }

    private void PlaceSelectionOverlay(double x, double y, double w, double h)
    {
        Canvas.SetLeft(_cropRect, x);
        Canvas.SetTop(_cropRect, y);
        _cropRect.Width = w;
        _cropRect.Height = h;
        Canvas.SetLeft(_selectionEllipse, x);
        Canvas.SetTop(_selectionEllipse, y);
        _selectionEllipse.Width = w;
        _selectionEllipse.Height = h;
        SyncSelectionOverlayShape();
    }

    private void SyncSelectionOverlayShape()
    {
        var hasSize = _cropRect.Width > 0 && _cropRect.Height > 0
            && (_pixelSelection is not null || _cropDragging || _selectionMoving);
        if (!_selectionMode || !hasSize)
        {
            if (!_cropMode)
            {
                _cropRect.Visibility = Visibility.Collapsed;
            }

            _selectionEllipse.Visibility = Visibility.Collapsed;
            return;
        }

        if (_selectionKind == ImageSelectionKind.Ellipse)
        {
            _selectionEllipse.Visibility = Visibility.Visible;
            _cropRect.Visibility = Visibility.Collapsed;
            ClearLassoPolyline();
        }
        else if (IsLassoKind())
        {
            _cropRect.Visibility = Visibility.Collapsed;
            _selectionEllipse.Visibility = Visibility.Collapsed;
            RebuildLassoPolylineFromDoc();
        }
        else
        {
            _cropRect.Visibility = Visibility.Visible;
            _selectionEllipse.Visibility = Visibility.Collapsed;
            ClearLassoPolyline();
        }
    }

    private void ClearPixelSelection()
    {
        _pixelSelection = null;
        _selectionMoving = false;
        _moveSourcePixels = null;
        _selectionInverted = false;
        _lassoDocPoints.Clear();
        ClearLassoPolyline();
        ClearCropSelection();
        ApplySelectionChrome();
        if (_selectionMode)
        {
            _status.Text = "Selection cleared.";
        }
    }

    private void ClearLassoPolyline()
    {
        if (_lassoPolyline is not null)
        {
            _cropOverlay.Children.Remove(_lassoPolyline);
            _lassoPolyline = null;
        }
    }

    private bool IsPointInSelectionOverlay(Windows.Foundation.Point point)
    {
        if (_pixelSelection is null)
        {
            return false;
        }

        var left = Canvas.GetLeft(_cropRect);
        var top = Canvas.GetTop(_cropRect);
        var w = _cropRect.Width;
        var h = _cropRect.Height;
        if (w <= 0 || h <= 0)
        {
            return false;
        }

        if (_selectionKind == ImageSelectionKind.Ellipse)
        {
            return ImageSelectionGeometry.ContainsInEllipse(point.X, point.Y, left, top, w, h);
        }

        if (IsLassoKind() && _lassoPolyline is { Points.Count: >= 3 })
        {
            var verts = _lassoPolyline.Points.Select(p => (p.X, p.Y)).ToList();
            return ImageSelectionGeometry.ContainsInPolygon(point.X, point.Y, verts);
        }

        return ImageSelectionGeometry.ContainsInRect(point.X, point.Y, left, top, w, h);
    }

    private void ClearCropSelection()
    {
        _cropRect.Visibility = Visibility.Collapsed;
        _cropRect.Width = 0;
        _cropRect.Height = 0;
        Canvas.SetLeft(_cropRect, 0);
        Canvas.SetTop(_cropRect, 0);
        _selectionEllipse.Visibility = Visibility.Collapsed;
        _selectionEllipse.Width = 0;
        _selectionEllipse.Height = 0;
        Canvas.SetLeft(_selectionEllipse, 0);
        Canvas.SetTop(_selectionEllipse, 0);
    }

    private void CropOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_cropMode && !_selectionMode)
        {
            return;
        }

        var point = e.GetCurrentPoint(_cropOverlay).Position;
        if (_selectionMode && IsPointInSelectionOverlay(point))
        {
            if (_selectionInverted)
            {
                _status.Text = "Cannot move an inverted selection — Invert again first.";
                e.Handled = true;
                return;
            }

            _selectionMoving = true;
            _cropDragging = false;
            _moveStart = point;
            _moveOriginLeft = Canvas.GetLeft(_cropRect);
            _moveOriginTop = Canvas.GetTop(_cropRect);
            _moveSourcePixels = _pixelSelection;
            _cropOverlay.CapturePointer(e.Pointer);
            _status.Text = "Moving selection…";
            e.Handled = true;
            return;
        }

        _selectionMoving = false;
        _moveSourcePixels = null;
        _cropDragging = true;
        _cropStart = point;
        _selectionInverted = false;
        ApplySelectionChrome();
        if (_selectionMode && IsLassoKind())
        {
            _lassoDocPoints.Clear();
            ClearLassoPolyline();
            if (_selectionKind == ImageSelectionKind.Smart)
            {
                _ = EnsureSmartEdgeMapAsync();
            }
            else
            {
                _smartEdgeMap = null;
            }

            _lassoPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 144, 255)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
                IsHitTestVisible = false,
            };
            _lassoPolyline.Points.Add(SnapSmartPoint(point));
            _cropOverlay.Children.Add(_lassoPolyline);
            _cropRect.Visibility = Visibility.Collapsed;
            _selectionEllipse.Visibility = Visibility.Collapsed;
        }
        else
        {
            UpdateCropRect(_cropStart, _cropStart);
        }

        _cropOverlay.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void CropOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_selectionMoving)
        {
            var point = e.GetCurrentPoint(_cropOverlay).Position;
            var dx = point.X - _moveStart.X;
            var dy = point.Y - _moveStart.Y;
            var maxLeft = Math.Max(0, _displayWidth - _cropRect.Width);
            var maxTop = Math.Max(0, _displayHeight - _cropRect.Height);
            var left = Math.Clamp(_moveOriginLeft + dx, 0, maxLeft);
            var top = Math.Clamp(_moveOriginTop + dy, 0, maxTop);
            PlaceSelectionOverlay(left, top, _cropRect.Width, _cropRect.Height);
            if (IsLassoKind() && _lassoDocPoints.Count >= 3)
            {
                RebuildLassoPolylineFromDoc(
                    previewOffsetX: left - _moveOriginLeft,
                    previewOffsetY: top - _moveOriginTop);
            }

            e.Handled = true;
            return;
        }

        if (!_cropDragging)
        {
            return;
        }

        var pos = e.GetCurrentPoint(_cropOverlay).Position;
        if (_selectionMode && IsLassoKind() && _lassoPolyline is not null)
        {
            _lassoPolyline.Points.Add(SnapSmartPoint(pos));
            e.Handled = true;
            return;
        }

        UpdateCropRect(_cropStart, pos);
        e.Handled = true;
    }

    private void CropOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_selectionMoving)
        {
            _selectionMoving = false;
            _cropOverlay.ReleasePointerCapture(e.Pointer);
            var source = _moveSourcePixels;
            _moveSourcePixels = null;
            if (source is { } sel && _displayWidth > 0 && _displayHeight > 0)
            {
                var dest = ImageCropMapper.ToDocumentPixels(
                    Canvas.GetLeft(_cropRect),
                    Canvas.GetTop(_cropRect),
                    _cropRect.Width,
                    _cropRect.Height,
                    _displayWidth,
                    _displayHeight,
                    _document.PixelWidth,
                    _document.PixelHeight);
                if (dest.X != sel.X || dest.Y != sel.Y)
                {
                    _ = CommitSelectionMoveAsync(sel, dest.X, dest.Y);
                }
                else
                {
                    SetPixelSelection(sel);
                    _status.Text = $"Selected {sel.Width}×{sel.Height} px";
                }
            }

            e.Handled = true;
            return;
        }

        if (!_cropDragging)
        {
            return;
        }

        _cropDragging = false;
        _cropOverlay.ReleasePointerCapture(e.Pointer);
        if (_selectionMode && IsLassoKind() && _lassoPolyline is { Points.Count: >= 3 })
        {
            FinishLassoSelection();
            e.Handled = true;
            return;
        }

        UpdateCropRect(_cropStart, e.GetCurrentPoint(_cropOverlay).Position);
        if (_selectionMode && _cropRect.Width > 0 && _cropRect.Height > 0 && _displayWidth > 0)
        {
            _pixelSelection = ImageCropMapper.ToDocumentPixels(
                Canvas.GetLeft(_cropRect),
                Canvas.GetTop(_cropRect),
                _cropRect.Width,
                _cropRect.Height,
                _displayWidth,
                _displayHeight,
                _document.PixelWidth,
                _document.PixelHeight);
            _lassoDocPoints.Clear();
            SyncSelectionOverlayShape();
            ApplySelectionChrome();
            _status.Text =
                $"Selected {_pixelSelection.Value.Width}×{_pixelSelection.Value.Height} px ({SelectionKindLabel()})";
        }

        e.Handled = true;
    }

    private async Task EnsureSmartEdgeMapAsync()
    {
        try
        {
            var pixels = await _document.GetPixelsAsync(maxEdge: Math.Max(256, Math.Max(_displayWidth, _displayHeight)));
            var w = pixels.Width;
            var h = pixels.Height;
            var data = pixels.BgraPixels;
            var lum = new float[h, w];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = ((y * w) + x) * 4;
                    lum[y, x] = (data[i] * 0.114f) + (data[i + 1] * 0.587f) + (data[i + 2] * 0.299f);
                }
            }

            var edges = new float[h, w];
            for (var y = 1; y < h - 1; y++)
            {
                for (var x = 1; x < w - 1; x++)
                {
                    var gx = -lum[y - 1, x - 1] - (2 * lum[y, x - 1]) - lum[y + 1, x - 1]
                        + lum[y - 1, x + 1] + (2 * lum[y, x + 1]) + lum[y + 1, x + 1];
                    var gy = -lum[y - 1, x - 1] - (2 * lum[y - 1, x]) - lum[y - 1, x + 1]
                        + lum[y + 1, x - 1] + (2 * lum[y + 1, x]) + lum[y + 1, x + 1];
                    edges[y, x] = MathF.Sqrt((gx * gx) + (gy * gy));
                }
            }

            _smartEdgeMap = edges;
            _smartEdgeMapWidth = w;
            _smartEdgeMapHeight = h;
        }
        catch
        {
            _smartEdgeMap = null;
        }
    }

    private Windows.Foundation.Point SnapSmartPoint(Windows.Foundation.Point point)
    {
        if (_selectionKind != ImageSelectionKind.Smart
            || _smartEdgeMap is null
            || _displayWidth <= 0
            || _displayHeight <= 0
            || _smartEdgeMapWidth <= 0
            || _smartEdgeMapHeight <= 0)
        {
            return point;
        }

        var scaleX = _smartEdgeMapWidth / (double)_displayWidth;
        var scaleY = _smartEdgeMapHeight / (double)_displayHeight;
        var cx = (int)Math.Round(point.X * scaleX);
        var cy = (int)Math.Round(point.Y * scaleY);
        const int radius = 8;
        var best = 0f;
        var bestX = cx;
        var bestY = cy;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var x = cx + dx;
                var y = cy + dy;
                if (x < 0 || y < 0 || x >= _smartEdgeMapWidth || y >= _smartEdgeMapHeight)
                {
                    continue;
                }

                var strength = _smartEdgeMap[y, x];
                if (strength > best)
                {
                    best = strength;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        if (best < 12f)
        {
            return point;
        }

        return new Windows.Foundation.Point(bestX / scaleX, bestY / scaleY);
    }

    private string SelectionKindLabel() =>
        _selectionKind switch
        {
            ImageSelectionKind.Ellipse => "ellipse",
            ImageSelectionKind.Freeform => "lasso",
            ImageSelectionKind.Smart => "smart",
            _ => "rect",
        };

    private void FinishLassoSelection()
    {
        if (_lassoPolyline is null || _displayWidth <= 0 || _document.PixelWidth <= 0)
        {
            return;
        }

        if (_lassoPolyline.Points.Count >= 3)
        {
            var first = _lassoPolyline.Points[0];
            var last = _lassoPolyline.Points[^1];
            if (Math.Abs(first.X - last.X) > 1 || Math.Abs(first.Y - last.Y) > 1)
            {
                _lassoPolyline.Points.Add(first);
            }
        }

        var scaleX = _document.PixelWidth / (double)_displayWidth;
        var scaleY = _document.PixelHeight / (double)_displayHeight;
        _lassoDocPoints.Clear();
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in _lassoPolyline.Points)
        {
            var dx = p.X * scaleX;
            var dy = p.Y * scaleY;
            _lassoDocPoints.Add(new ImageMarkupPoint(dx, dy));
            minX = Math.Min(minX, dx);
            minY = Math.Min(minY, dy);
            maxX = Math.Max(maxX, dx);
            maxY = Math.Max(maxY, dy);
        }

        var x = Math.Clamp((int)Math.Floor(minX), 0, Math.Max(0, _document.PixelWidth - 1));
        var y = Math.Clamp((int)Math.Floor(minY), 0, Math.Max(0, _document.PixelHeight - 1));
        var right = Math.Clamp((int)Math.Ceiling(maxX), x + 1, _document.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(maxY), y + 1, _document.PixelHeight);
        _pixelSelection = new ImageRect(x, y, right - x, bottom - y);
        PlaceSelectionOverlay(
            x * (_displayWidth / (double)_document.PixelWidth),
            y * (_displayHeight / (double)_document.PixelHeight),
            (right - x) * (_displayWidth / (double)_document.PixelWidth),
            (bottom - y) * (_displayHeight / (double)_document.PixelHeight));
        ApplySelectionChrome();
        _status.Text = $"Lasso selected {_pixelSelection.Value.Width}×{_pixelSelection.Value.Height} px ({_lassoDocPoints.Count} pts)";
    }

    private void RebuildLassoPolylineFromDoc(double previewOffsetX = 0, double previewOffsetY = 0)
    {
        if (_lassoDocPoints.Count < 3 || _displayWidth <= 0 || _document.PixelWidth <= 0)
        {
            return;
        }

        if (_lassoPolyline is null)
        {
            _lassoPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 144, 255)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 30, 144, 255)),
                IsHitTestVisible = false,
            };
            _cropOverlay.Children.Add(_lassoPolyline);
        }

        var scaleX = _displayWidth / (double)_document.PixelWidth;
        var scaleY = _displayHeight / (double)_document.PixelHeight;
        _lassoPolyline.Points.Clear();
        foreach (var p in _lassoDocPoints)
        {
            _lassoPolyline.Points.Add(new Windows.Foundation.Point(
                (p.X * scaleX) + previewOffsetX,
                (p.Y * scaleY) + previewOffsetY));
        }
    }

    private async Task CommitSelectionMoveAsync(ImageRect source, int destX, int destY)
    {
        var w = source.Width;
        var h = source.Height;
        destX = Math.Clamp(destX, 0, Math.Max(0, _document.PixelWidth - w));
        destY = Math.Clamp(destY, 0, Math.Max(0, _document.PixelHeight - h));
        var polygon = CurrentLassoOrNull();
        await MutateAsync(
            () => _processor.MoveRectAsync(_document, source, destX, destY, _selectionKind, polygon),
            $"Moved selection to ({destX},{destY}).");
        TranslateLasso(destX - source.X, destY - source.Y);
        SetPixelSelection(new ImageRect(destX, destY, w, h));
        if (IsLassoKind())
        {
            RebuildLassoPolylineFromDoc();
        }
    }

    private void UpdateCropRect(Windows.Foundation.Point a, Windows.Foundation.Point b)
    {
        var aspect = _cropMode ? ResolveCropAspect() : null;
        var (x, y, w, h) = ImageCropAspect.Constrain(
            a.X, a.Y, b.X, b.Y, _displayWidth, _displayHeight, aspect);
        if (_selectionMode)
        {
            PlaceSelectionOverlay(x, y, w, h);
            if (w > 0 && h > 0 && _displayWidth > 0 && _displayHeight > 0)
            {
                var mapped = ImageCropMapper.ToDocumentPixels(
                    x, y, w, h, _displayWidth, _displayHeight, _document.PixelWidth, _document.PixelHeight);
                _status.Text = $"Selection ({SelectionKindLabel()}) → {mapped.Width}×{mapped.Height} px";
            }

            return;
        }

        Canvas.SetLeft(_cropRect, x);
        Canvas.SetTop(_cropRect, y);
        _cropRect.Width = w;
        _cropRect.Height = h;
        _cropRect.Visibility = w > 0 && h > 0 ? Visibility.Visible : Visibility.Collapsed;
        _selectionEllipse.Visibility = Visibility.Collapsed;
        if (w > 0 && h > 0 && _displayWidth > 0 && _displayHeight > 0)
        {
            var mapped = ImageCropMapper.ToDocumentPixels(
                x, y, w, h, _displayWidth, _displayHeight, _document.PixelWidth, _document.PixelHeight);
            _status.Text = $"Crop selection → {mapped.Width}×{mapped.Height} px";
            _cropBox.Text = $"{mapped.X},{mapped.Y},{mapped.Width},{mapped.Height}";
        }
    }

    private double? ResolveCropAspect() =>
        _cropAspectBox.SelectedIndex switch
        {
            1 => _document.PixelHeight > 0
                ? _document.PixelWidth / (double)_document.PixelHeight
                : null,
            2 => 1.0,
            3 => 4.0 / 3.0,
            4 => 3.0 / 2.0,
            5 => 16.0 / 9.0,
            _ => null,
        };

    private async Task ApplyInteractiveCropAsync()
    {
        if (_cropRect.Visibility != Visibility.Visible || _cropRect.Width < 1 || _cropRect.Height < 1)
        {
            _status.Text = "Drag a crop region first.";
            return;
        }

        if (_displayWidth <= 0 || _displayHeight <= 0)
        {
            _status.Text = "Image not ready to crop.";
            return;
        }

        var mapped = ImageCropMapper.ToDocumentPixels(
            Canvas.GetLeft(_cropRect),
            Canvas.GetTop(_cropRect),
            _cropRect.Width,
            _cropRect.Height,
            _displayWidth,
            _displayHeight,
            _document.PixelWidth,
            _document.PixelHeight);

        ExitCropMode();
        await MutateAsync(
            () => _processor.CropAsync(_document, mapped),
            $"Cropped to {mapped.Width}×{mapped.Height}.");
    }

    private async Task ResizeAsync()
    {
        var srcW = _document.PixelWidth;
        var srcH = _document.PixelHeight;
        if (srcW <= 0 || srcH <= 0)
        {
            _status.Text = "Nothing to resize.";
            return;
        }

        var meta = await _document.GetMetadataAsync();
        var currentDpi = meta.DpiX is > 0 ? meta.DpiX.Value : (meta.DpiY is > 0 ? meta.DpiY.Value : 96.0);
        var aspect = (double)srcW / srcH;
        var updating = false;
        var unitBox = new ComboBox
        {
            Header = "Units",
            Width = 140,
            ItemsSource = new[] { "Pixels", "Inches", "Centimeters" },
            SelectedIndex = 0,
        };
        var dpiBox = new NumberBox
        {
            Header = "DPI / PPI",
            Value = currentDpi,
            Minimum = 1,
            Maximum = 1200,
            SmallChange = 1,
            LargeChange = 10,
            Width = 140,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        var widthBox = new TextBox { Text = srcW.ToString(), Width = 96, Header = "Width" };
        var heightBox = new TextBox { Text = srcH.ToString(), Width = 96, Header = "Height" };
        var percentBox = new TextBox { Text = "100", Width = 96, Header = "Scale %" };
        var lockAspect = new CheckBox { Content = "Lock aspect ratio", IsChecked = true };
        var filterBox = new ComboBox
        {
            Header = "Resampling",
            Width = 180,
            ItemsSource = new[] { "Auto", "Nearest-neighbor", "Bilinear", "Bicubic" },
            SelectedIndex = ResolveDefaultInterpolationIndex(),
        };
        var preview = new TextBlock
        {
            Text = $"Result: {srcW}×{srcH} px · ~{ImageResizeDialogMath.EstimateRawBgraMegabytes(srcW, srcH):0.##} MB raw",
            Opacity = 0.8,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };

        double ActiveDpi() => dpiBox.Value > 0 ? dpiBox.Value : 96;

        (int PxW, int PxH) ParsePixelSize()
        {
            if (!double.TryParse(widthBox.Text, out var wVal) || wVal <= 0
                || !double.TryParse(heightBox.Text, out var hVal) || hVal <= 0)
            {
                return (0, 0);
            }

            return unitBox.SelectedIndex switch
            {
                1 => (
                    Math.Max(1, (int)Math.Round(wVal * ActiveDpi())),
                    Math.Max(1, (int)Math.Round(hVal * ActiveDpi()))),
                2 => (
                    Math.Max(1, (int)Math.Round(wVal / 2.54 * ActiveDpi())),
                    Math.Max(1, (int)Math.Round(hVal / 2.54 * ActiveDpi()))),
                _ => (Math.Max(1, (int)Math.Round(wVal)), Math.Max(1, (int)Math.Round(hVal))),
            };
        }

        void UpdatePreview()
        {
            var (w, h) = ParsePixelSize();
            if (w <= 0 || h <= 0)
            {
                preview.Text = "Result: —";
                return;
            }

            preview.Text = $"Result: {w}×{h} px @ {ActiveDpi():0.#} DPI · ~{ImageResizeDialogMath.EstimateRawBgraMegabytes(w, h):0.##} MB raw BGRA";
        }

        void WritePhysicalFromPixels(int pxW, int pxH)
        {
            updating = true;
            try
            {
                if (unitBox.SelectedIndex == 1)
                {
                    widthBox.Text = (pxW / ActiveDpi()).ToString("0.###");
                    heightBox.Text = (pxH / ActiveDpi()).ToString("0.###");
                }
                else if (unitBox.SelectedIndex == 2)
                {
                    widthBox.Text = (pxW / ActiveDpi() * 2.54).ToString("0.###");
                    heightBox.Text = (pxH / ActiveDpi() * 2.54).ToString("0.###");
                }
                else
                {
                    widthBox.Text = pxW.ToString();
                    heightBox.Text = pxH.ToString();
                }

                percentBox.Text = Math.Round(100.0 * pxW / srcW).ToString("0");
                UpdatePreview();
            }
            finally
            {
                updating = false;
            }
        }

        void SyncFromWidth()
        {
            if (updating)
            {
                return;
            }

            var (w, h) = ParsePixelSize();
            if (w <= 0)
            {
                return;
            }

            if (lockAspect.IsChecked == true)
            {
                h = Math.Max(1, (int)Math.Round(w / aspect));
                WritePhysicalFromPixels(w, h);
            }
            else
            {
                UpdatePreview();
            }
        }

        void SyncFromHeight()
        {
            if (updating)
            {
                return;
            }

            var (w, h) = ParsePixelSize();
            if (h <= 0)
            {
                return;
            }

            if (lockAspect.IsChecked == true)
            {
                w = Math.Max(1, (int)Math.Round(h * aspect));
                WritePhysicalFromPixels(w, h);
            }
            else
            {
                UpdatePreview();
            }
        }

        void SyncFromPercent()
        {
            if (updating || !double.TryParse(percentBox.Text, out var pct) || pct <= 0)
            {
                return;
            }

            var w = Math.Max(1, (int)Math.Round(srcW * pct / 100.0));
            var h = lockAspect.IsChecked == true
                ? Math.Max(1, (int)Math.Round(w / aspect))
                : Math.Max(1, (int)Math.Round(srcH * pct / 100.0));
            WritePhysicalFromPixels(w, h);
        }

        widthBox.TextChanged += (_, _) => SyncFromWidth();
        heightBox.TextChanged += (_, _) => SyncFromHeight();
        percentBox.TextChanged += (_, _) => SyncFromPercent();
        lockAspect.Checked += (_, _) => SyncFromWidth();
        lockAspect.Unchecked += (_, _) => UpdatePreview();
        unitBox.SelectionChanged += (_, _) => WritePhysicalFromPixels(srcW, srcH);
        dpiBox.ValueChanged += (_, _) =>
        {
            if (unitBox.SelectedIndex != 0)
            {
                SyncFromWidth();
            }
            else
            {
                UpdatePreview();
            }
        };

        var batchFolder = new CheckBox
        {
            Content = _siblings.Count > 1
                ? $"Also resize all {_siblings.Count} images in folder (scale %)"
                : "Also resize folder images",
            IsChecked = false,
            IsEnabled = _siblings.Count > 1 && _decoder is not null,
        };
        ToolTipService.SetToolTip(
            batchFolder,
            "Applies Scale % to every image in this folder (overwrites files on disk). Current image is resized in memory until Save.");

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"Current: {srcW}×{srcH} px · {currentDpi:0.#} DPI" },
                unitBox,
                dpiBox,
                widthBox,
                heightBox,
                percentBox,
                lockAspect,
                filterBox,
                batchFolder,
                preview,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Resize image",
            Content = panel,
            PrimaryButtonText = "Resize",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var (width, height) = ParsePixelSize();
        if (width <= 0 || height <= 0)
        {
            _status.Text = "Resize needs positive width and height.";
            return;
        }

        var filter = ImageResizeDialogMath.FilterFromComboIndex(filterBox.SelectedIndex);
        var options = new ImageResizeOptions(Filter: filter, DensityDpi: ActiveDpi());
        await MutateAsync(
            () => _processor.ResizeAsync(_document, width, height, options),
            $"Resized to {width}×{height} @ {ActiveDpi():0.#} DPI.");

        if (batchFolder.IsChecked == true && _decoder is not null && _siblings.Count > 1)
        {
            if (!double.TryParse(percentBox.Text, out var pct) || pct <= 0)
            {
                _status.Text = "Batch resize needs a positive Scale %.";
                return;
            }

            var batchCount = await BatchResizeFolderAsync(
                pct,
                lockAspect.IsChecked == true,
                options);
            _status.Text =
                $"Resized current to {width}×{height}; batch-updated {batchCount} folder image(s) at {pct:0.#}%.";
        }
    }

    private async Task<int> BatchResizeFolderAsync(
        double percent,
        bool lockAspect,
        ImageResizeOptions options)
    {
        if (_decoder is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            return 0;
        }

        var current = System.IO.Path.GetFullPath(_document.Path);
        var targets = _siblings
            .Where(s => !string.Equals(System.IO.Path.GetFullPath(s), current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (updated, cancelled) = await RunBatchWithProgressAsync(
            "Batch resize",
            targets,
            async (sibling, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await using var doc = await _decoder.OpenAsync(sibling, ct);
                var w = Math.Max(1, (int)Math.Round(doc.PixelWidth * percent / 100.0));
                var h = lockAspect
                    ? Math.Max(1, (int)Math.Round(w * (doc.PixelHeight / (double)Math.Max(1, doc.PixelWidth))))
                    : Math.Max(1, (int)Math.Round(doc.PixelHeight * percent / 100.0));
                await _processor.ResizeAsync(doc, w, h, options, ct);
                await _encoder.SaveAsync(doc, sibling, ct);
                return true;
            });
        if (cancelled)
        {
            _status.Text = $"Batch resize cancelled after {updated} file(s).";
        }

        return updated;
    }

    private async Task BatchOrientFolderAsync()
    {
        if (_decoder is null || _siblings.Count < 2 || string.IsNullOrWhiteSpace(_document.Path))
        {
            _status.Text = "Batch folder ops need a folder with multiple images.";
            return;
        }

        var categoryBox = new ComboBox
        {
            Header = "Category",
            Width = 260,
            ItemsSource = new[] { "Orientation", "Convert / export", "Strip metadata", "Rename", "Color profile" },
            SelectedIndex = 0,
        };
        var opBox = new ComboBox
        {
            Header = "Operation",
            Width = 260,
            ItemsSource = new[]
            {
                "Rotate left 90°",
                "Rotate right 90°",
                "Rotate 180°",
                "Flip horizontal",
                "Flip vertical",
                "Normalize EXIF orientation",
            },
            SelectedIndex = 1,
        };
        var formatBox = new ComboBox
        {
            Header = "Export format",
            Width = 260,
            Visibility = Visibility.Collapsed,
            ItemsSource = new[] { "PNG", "JPEG", "WebP", "TIFF", "BMP", "GIF", "AVIF", "JPEG 2000" },
            SelectedIndex = 0,
        };
        var quality = new Slider
        {
            Header = "Quality (JPEG/WebP/AVIF)",
            Minimum = 1,
            Maximum = 100,
            Value = 85,
            StepFrequency = 1,
            Width = 260,
            Visibility = Visibility.Collapsed,
        };
        var renamePattern = new TextBox
        {
            Header = "Rename pattern ({n}=1-based index, {name}=base name)",
            Text = "{name}-{n:000}",
            Width = 260,
            Visibility = Visibility.Collapsed,
        };
        var profileBox = new ComboBox
        {
            Header = "Color profile",
            Width = 260,
            Visibility = Visibility.Collapsed,
            ItemsSource = new[] { "Assign sRGB", "Convert → sRGB", "Assign Adobe RGB", "Convert → Adobe RGB" },
            SelectedIndex = 1,
        };
        var includeCurrent = new CheckBox
        {
            Content = "Also apply to the open image file",
            IsChecked = true,
        };

        void SyncCategory()
        {
            var cat = categoryBox.SelectedIndex;
            opBox.Visibility = cat == 0 ? Visibility.Visible : Visibility.Collapsed;
            formatBox.Visibility = cat == 1 ? Visibility.Visible : Visibility.Collapsed;
            var fmt = formatBox.SelectedItem as string;
            quality.Visibility = cat == 1 && fmt is "JPEG" or "WebP" or "AVIF"
                ? Visibility.Visible
                : Visibility.Collapsed;
            renamePattern.Visibility = cat == 3 ? Visibility.Visible : Visibility.Collapsed;
            profileBox.Visibility = cat == 4 ? Visibility.Visible : Visibility.Collapsed;
            includeCurrent.Visibility = cat is 1 or 3 ? Visibility.Collapsed : Visibility.Visible;
        }

        categoryBox.SelectionChanged += (_, _) => SyncCategory();
        formatBox.SelectionChanged += (_, _) => SyncCategory();
        SyncCategory();

        var dialog = new ContentDialog
        {
            Title = "Batch folder images",
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Applies to {_siblings.Count} images in this folder. Convert/rename write new files; orientation/strip/profile overwrite originals.",
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 360,
                    },
                    categoryBox,
                    opBox,
                    formatBox,
                    quality,
                    renamePattern,
                    profileBox,
                    includeCurrent,
                },
            },
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (categoryBox.SelectedIndex == 1)
        {
            var (format, extension) = (formatBox.SelectedItem as string) switch
            {
                "PNG" => (ImageEncodeFormat.Png, ".png"),
                "JPEG" => (ImageEncodeFormat.Jpeg, ".jpg"),
                "WebP" => (ImageEncodeFormat.Webp, ".webp"),
                "TIFF" => (ImageEncodeFormat.Tiff, ".tif"),
                "BMP" => (ImageEncodeFormat.Bmp, ".bmp"),
                "GIF" => (ImageEncodeFormat.Gif, ".gif"),
                "AVIF" => (ImageEncodeFormat.Avif, ".avif"),
                "JPEG 2000" => (ImageEncodeFormat.Jpeg2000, ".jp2"),
                _ => (ImageEncodeFormat.Png, ".png"),
            };
            ImageEncodeOptions? options = format is ImageEncodeFormat.Jpeg or ImageEncodeFormat.Webp or ImageEncodeFormat.Avif
                ? new ImageEncodeOptions(Quality: (int)quality.Value, PreserveMetadata: true)
                : new ImageEncodeOptions(PreserveMetadata: true);
            var converted = await BatchConvertFolderAsync(format, extension, options);
            _status.Text = $"Batch convert → {format}: wrote {converted} file(s).";
            return;
        }

        if (categoryBox.SelectedIndex == 2)
        {
            var strippedCount = await BatchStripMetadataFolderAsync(includeCurrent: includeCurrent.IsChecked == true);
            _status.Text = $"Batch strip metadata: updated {strippedCount} folder image(s).";
            return;
        }

        if (categoryBox.SelectedIndex == 3)
        {
            var renamed = await BatchRenameFolderAsync(renamePattern.Text ?? "{name}-{n:000}");
            _status.Text = $"Batch rename: renamed {renamed} file(s).";
            RefreshSiblingList();
            return;
        }

        if (categoryBox.SelectedIndex == 4)
        {
            var (kind, convert) = profileBox.SelectedIndex switch
            {
                0 => (ImageColorProfileKind.Srgb, false),
                1 => (ImageColorProfileKind.Srgb, true),
                2 => (ImageColorProfileKind.AdobeRgb, false),
                _ => (ImageColorProfileKind.AdobeRgb, true),
            };
            if (includeCurrent.IsChecked == true)
            {
                await MutateAsync(
                    () => convert
                        ? _processor.ConvertColorProfileAsync(_document, kind)
                        : _processor.AssignColorProfileAsync(_document, kind),
                    convert ? $"Converted current image → {kind}." : $"Assigned {kind} profile to current image.");
            }

            var profiled = await BatchColorProfileFolderAsync(kind, convert, includeCurrent.IsChecked == true);
            _status.Text = $"Batch color profile: updated {profiled} folder image(s)"
                + (includeCurrent.IsChecked == true ? " (+ current)." : ".");
            return;
        }

        var op = opBox.SelectedIndex;
        async Task ApplyAsync(IImageDocument doc)
        {
            switch (op)
            {
                case 0:
                    await _processor.RotateAsync(doc, -90);
                    break;
                case 1:
                    await _processor.RotateAsync(doc, 90);
                    break;
                case 2:
                    await _processor.RotateAsync(doc, 180);
                    break;
                case 3:
                    await _processor.FlipHorizontalAsync(doc);
                    break;
                case 4:
                    await _processor.FlipVerticalAsync(doc);
                    break;
                default:
                    await _processor.NormalizeOrientationAsync(doc);
                    break;
            }
        }

        var label = opBox.SelectedItem?.ToString() ?? "orientation";
        if (includeCurrent.IsChecked == true)
        {
            await MutateAsync(() => ApplyAsync(_document), $"Current image: {label}.");
        }

        var current = System.IO.Path.GetFullPath(_document.Path);
        var targets = _siblings
            .Where(s => !string.Equals(System.IO.Path.GetFullPath(s), current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (updated, cancelled) = await RunBatchWithProgressAsync(
            $"Batch {label}",
            targets,
            async (sibling, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await using var doc = await _decoder.OpenAsync(sibling, ct);
                await ApplyAsync(doc);
                await _encoder.SaveAsync(doc, sibling, ct);
                return true;
            });
        _status.Text = cancelled
            ? $"Batch {label} cancelled after {updated} file(s)."
            : $"Batch {label}: updated {updated} folder image(s)"
                + (includeCurrent.IsChecked == true ? " (+ current)." : ".");
    }

    /// <summary>
    /// Runs a per-file folder batch with a progress dialog and Cancel (F36 progress).
    /// </summary>
    private async Task<(int Updated, bool Cancelled)> RunBatchWithProgressAsync(
        string title,
        IReadOnlyList<string> targets,
        Func<string, int, CancellationToken, Task<bool>> processOne)
    {
        if (targets.Count == 0)
        {
            return (0, false);
        }

        using var cts = new CancellationTokenSource();
        var workDone = false;
        var progressLabel = new TextBlock
        {
            Text = $"0 / {targets.Count}",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
        };
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = targets.Count,
            Value = 0,
            Width = 320,
        };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    progressLabel,
                    bar,
                    new TextBlock
                    {
                        Text = "Cancel stops after the current file.",
                        Opacity = 0.7,
                        FontSize = 12,
                    },
                },
            },
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        dialog.Closing += (_, args) =>
        {
            cts.Cancel();
            if (!workDone && args.Result == ContentDialogResult.None)
            {
                args.Cancel = true;
            }
        };

        var showTask = dialog.ShowAsync().AsTask();
        var updated = 0;
        var cancelled = false;
        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (cts.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                var path = targets[i];
                var name = System.IO.Path.GetFileName(path);
                progressLabel.Text = $"{i + 1} / {targets.Count} · {name}";
                bar.Value = i;
                try
                {
                    if (await processOne(path, i, cts.Token))
                    {
                        updated++;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    progressLabel.Text = $"{i + 1} / {targets.Count} · skipped {name}: {ex.Message}";
                }

                bar.Value = i + 1;
            }
        }
        finally
        {
            workDone = true;
            dialog.Hide();
            try
            {
                await showTask;
            }
            catch
            {
                // Dialog may already be dismissed.
            }
        }

        return (updated, cancelled);
    }

    private async Task<int> BatchConvertFolderAsync(
        ImageEncodeFormat format,
        string extension,
        ImageEncodeOptions? options)
    {
        if (_decoder is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            return 0;
        }

        var (written, cancelled) = await RunBatchWithProgressAsync(
            "Batch convert",
            _siblings,
            async (sibling, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await using var doc = await _decoder.OpenAsync(sibling, ct);
                var dest = System.IO.Path.ChangeExtension(sibling, extension);
                await _encoder.SaveAsAsync(doc, dest, format, options, ct);
                return true;
            });
        if (cancelled)
        {
            _status.Text = $"Batch convert cancelled after {written} file(s).";
        }

        return written;
    }

    private async Task<int> BatchStripMetadataFolderAsync(bool includeCurrent)
    {
        if (_decoder is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            return 0;
        }

        var current = System.IO.Path.GetFullPath(_document.Path);
        var targets = _siblings
            .Where(s => includeCurrent
                || !string.Equals(System.IO.Path.GetFullPath(s), current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (updated, cancelled) = await RunBatchWithProgressAsync(
            "Batch strip metadata",
            targets,
            async (sibling, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await using var doc = await _decoder.OpenAsync(sibling, ct);
                var format = ImageEncodeFormatResolver.FromExtension(System.IO.Path.GetExtension(sibling));
                var temp = sibling + ".glyph-strip-tmp" + System.IO.Path.GetExtension(sibling);
                await _encoder.SaveAsAsync(
                    doc,
                    temp,
                    format,
                    new ImageEncodeOptions(PreserveMetadata: false),
                    ct);
                System.IO.File.Copy(temp, sibling, overwrite: true);
                System.IO.File.Delete(temp);
                return true;
            });
        if (cancelled)
        {
            _status.Text = $"Batch strip cancelled after {updated} file(s).";
        }

        return updated;
    }

    private async Task<int> BatchRenameFolderAsync(string pattern)
    {
        if (string.IsNullOrWhiteSpace(_document.Path) || _siblings.Count == 0)
        {
            return 0;
        }

        var dir = System.IO.Path.GetDirectoryName(_document.Path);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return 0;
        }

        // Snapshot paths so renames don't disturb enumeration / {n} indexing.
        var targets = _siblings.ToList();
        var (renamed, cancelled) = await RunBatchWithProgressAsync(
            "Batch rename",
            targets,
            async (sibling, index, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var baseName = System.IO.Path.GetFileNameWithoutExtension(sibling);
                var ext = System.IO.Path.GetExtension(sibling);
                var n = index + 1;
                var stem = pattern
                    .Replace("{name}", baseName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{n:000}", n.ToString("000"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{n:00}", n.ToString("00"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{n}", n.ToString(), StringComparison.OrdinalIgnoreCase);
                foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                {
                    stem = stem.Replace(c, '_');
                }

                if (string.IsNullOrWhiteSpace(stem))
                {
                    return false;
                }

                var dest = System.IO.Path.Combine(dir, stem + ext);
                if (string.Equals(
                        System.IO.Path.GetFullPath(sibling),
                        System.IO.Path.GetFullPath(dest),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (System.IO.File.Exists(dest))
                {
                    dest = System.IO.Path.Combine(dir, stem + "-" + Guid.NewGuid().ToString("N")[..6] + ext);
                }

                System.IO.File.Move(sibling, dest);
                if (string.Equals(
                        System.IO.Path.GetFullPath(sibling),
                        System.IO.Path.GetFullPath(_document.Path!),
                        StringComparison.OrdinalIgnoreCase))
                {
                    _document.Path = dest;
                }

                return true;
            });
        if (cancelled)
        {
            _status.Text = $"Batch rename cancelled after {renamed} file(s).";
        }

        return renamed;
    }

    private async Task<int> BatchColorProfileFolderAsync(
        ImageColorProfileKind kind,
        bool convert,
        bool includeCurrent)
    {
        if (_decoder is null || string.IsNullOrWhiteSpace(_document.Path))
        {
            return 0;
        }

        _ = includeCurrent; // Current image handled by caller MutateAsync when selected.
        var current = System.IO.Path.GetFullPath(_document.Path);
        var targets = _siblings
            .Where(s => !string.Equals(System.IO.Path.GetFullPath(s), current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (updated, cancelled) = await RunBatchWithProgressAsync(
            "Batch color profile",
            targets,
            async (sibling, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await using var doc = await _decoder.OpenAsync(sibling, ct);
                if (convert)
                {
                    await _processor.ConvertColorProfileAsync(doc, kind, ct);
                }
                else
                {
                    await _processor.AssignColorProfileAsync(doc, kind, ct);
                }

                await _encoder.SaveAsync(doc, sibling, ct);
                return true;
            });
        if (cancelled)
        {
            _status.Text = $"Batch color profile cancelled after {updated} file(s).";
        }

        return updated;
    }

    private async Task BackgroundToolsAsync()
    {
        var fuzzSlider = new Slider
        {
            Minimum = 0,
            Maximum = 40,
            StepFrequency = 1,
            Value = 12,
            Width = 220,
        };
        var fuzzLabel = new TextBlock { Text = "Fuzz 12%" };
        fuzzSlider.ValueChanged += (_, args) =>
        {
            fuzzLabel.Text = $"Fuzz {args.NewValue:0}%";
        };
        var trimBox = new CheckBox
        {
            Content = "Trim to opaque bounds after remove",
            IsChecked = true,
        };
        var actionBox = new ComboBox
        {
            Width = 280,
            ItemsSource = new[]
            {
                "Remove background (edit in place)",
                "Extract subject → clipboard PNG",
                "Extract subject → save PNG",
            },
            SelectedIndex = 0,
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Corner flood-fill removes connected background colors (studio / solid BG).",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 320,
                    Opacity = 0.8,
                },
                fuzzLabel,
                fuzzSlider,
                trimBox,
                actionBox,
            },
        };
        var dialog = new ContentDialog
        {
            Title = "Background / subject",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var fuzz = fuzzSlider.Value;
        var trim = trimBox.IsChecked == true;
        var action = actionBox.SelectedIndex;

        try
        {
            if (action == 0)
            {
                await MutateAsync(
                    async () =>
                    {
                        await _processor.RemoveBackgroundAsync(_document, fuzz);
                        if (trim)
                        {
                            await _processor.TrimTransparentAsync(_document);
                        }
                    },
                    trim
                        ? $"Background removed (fuzz {fuzz:0}%) and trimmed."
                        : $"Background removed (fuzz {fuzz:0}%).");
                var format = _document.FormatName;
                if (format.Contains("Jpeg", StringComparison.OrdinalIgnoreCase)
                    || format.Contains("Jpg", StringComparison.OrdinalIgnoreCase)
                    || format.Contains("Bmp", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(format, "Gif", StringComparison.OrdinalIgnoreCase))
                {
                    _status.Text += " · Save/Convert to PNG/WebP to keep transparency.";
                }

                return;
            }

            // Extract without keeping edits: mutate, capture pixels, restore.
            PauseAnimation();
            var restore = _document.CaptureCheckpoint();
            try
            {
                await _processor.RemoveBackgroundAsync(_document, fuzz);
                if (trim)
                {
                    await _processor.TrimTransparentAsync(_document);
                }

                var buffer = await _document.GetPixelsAsync();
                if (action == 1)
                {
                    var temp = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "glyph-subject-" + Guid.NewGuid().ToString("N") + ".png");
                    try
                    {
                        await _encoder.WriteBgraAsync(
                            buffer.BgraPixels,
                            buffer.Width,
                            buffer.Height,
                            temp,
                            ImageEncodeFormat.Png);
                        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(temp);
                        var stream = await file.OpenReadAsync();
                        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                        package.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream));
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                        _status.Text = $"Subject copied ({buffer.Width}×{buffer.Height}).";
                    }
                    finally
                    {
                        if (System.IO.File.Exists(temp))
                        {
                            System.IO.File.Delete(temp);
                        }
                    }
                }
                else
                {
                    var window = App.CurrentApp.MainWindowInstance
                        ?? throw new InvalidOperationException("Main window unavailable for save picker.");
                    var picker = new Windows.Storage.Pickers.FileSavePicker();
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                    picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                    picker.FileTypeChoices.Add("PNG", [".png"]);
                    var baseName = string.IsNullOrWhiteSpace(_document.Path)
                        ? "subject"
                        : System.IO.Path.GetFileNameWithoutExtension(_document.Path);
                    picker.SuggestedFileName = baseName + "-subject.png";
                    var file = await picker.PickSaveFileAsync();
                    if (file is null)
                    {
                        _status.Text = "Save subject cancelled.";
                    }
                    else
                    {
                        await _encoder.WriteBgraAsync(
                            buffer.BgraPixels,
                            buffer.Width,
                            buffer.Height,
                            file.Path,
                            ImageEncodeFormat.Png);
                        _status.Text = "Subject saved → " + file.Name;
                    }
                }
            }
            finally
            {
                _document.RestoreCheckpoint(restore);
                await RefreshAsync();
                UpdateStatus();
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Background tools failed: " + ex.Message;
        }
    }

    private async Task AdjustAsync()
    {
        static Slider MakeSlider(string header, double min, double max, double value, double step = 1)
        {
            return new Slider
            {
                Header = header,
                Minimum = min,
                Maximum = max,
                Value = value,
                StepFrequency = step,
                Width = 260,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
        }

        static FrameworkElement WithReset(Slider slider, double defaultValue, Action onChanged)
        {
            var resetOne = new Button
            {
                Content = "↺",
                Width = 36,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 0, 0),
            };
            ToolTipService.SetToolTip(resetOne, "Reset this adjustment");
            resetOne.Click += (_, _) =>
            {
                slider.Value = defaultValue;
                onChanged();
            };
            slider.ValueChanged += (_, _) => onChanged();
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };
            Grid.SetColumn(slider, 0);
            Grid.SetColumn(resetOne, 1);
            row.Children.Add(slider);
            row.Children.Add(resetOne);
            return row;
        }

        var brightness = MakeSlider("Brightness (−100…100)", -100, 100, 0);
        var contrast = MakeSlider("Contrast (−100…100)", -100, 100, 0);
        var saturation = MakeSlider("Saturation (−100…100)", -100, 100, 0);
        var highlights = MakeSlider("Highlights (−100 recover…100)", -100, 100, 0);
        var shadows = MakeSlider("Shadows (−100 crush…100 lift)", -100, 100, 0);
        var blackPoint = MakeSlider("Black point (0…100)", 0, 100, 0);
        var whitePoint = MakeSlider("White point (0…100)", 0, 100, 100);
        var gamma = MakeSlider("Gamma (0.1…3.0)", 0.1, 3.0, 1.0, step: 0.05);
        var temperature = MakeSlider("Temperature (−100 cold…100 warm)", -100, 100, 0);
        var tint = MakeSlider("Tint (−100 green…100 magenta)", -100, 100, 0);
        var sharpness = MakeSlider("Sharpness (0…100)", 0, 100, 0);
        var autoLevels = new CheckBox { Content = "Auto Levels", IsChecked = false };
        var sepia = new CheckBox { Content = "Sepia", IsChecked = false };
        var histCanvas = new Canvas
        {
            Width = 280,
            Height = 64,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)),
        };
        var histLabel = new TextBlock { Text = "Luminance histogram", Opacity = 0.75, FontSize = 12 };

        ImageAdjustments BuildAdjustments() => new(
            Brightness: brightness.Value,
            Contrast: contrast.Value,
            Saturation: saturation.Value,
            AutoLevels: autoLevels.IsChecked == true,
            Sharpness: sharpness.Value,
            Sepia: sepia.IsChecked == true,
            Temperature: temperature.Value,
            Tint: tint.Value,
            Highlights: highlights.Value,
            Shadows: shadows.Value,
            BlackPoint: blackPoint.Value,
            WhitePoint: whitePoint.Value,
            Gamma: gamma.Value);

        IImageEditCheckpoint? baseline = null;
        var previewBusy = false;
        var previewQueued = false;
        var dialogOpen = true;

        async Task UpdateHistogramAsync()
        {
            try
            {
                var pixels = await _document.GetPixelsAsync(maxEdge: 160);
                var bins = ImageLuminanceHistogram.BuildBins(pixels.BgraPixels);
                var max = Math.Max(1, bins.Max());
                histCanvas.Children.Clear();
                var barW = histCanvas.Width / bins.Length;
                for (var i = 0; i < bins.Length; i++)
                {
                    var h = bins[i] * (histCanvas.Height - 2) / max;
                    var bar = new Rectangle
                    {
                        Width = Math.Max(1, barW - 1),
                        Height = Math.Max(1, h),
                        Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 200, 200, 200)),
                    };
                    Canvas.SetLeft(bar, i * barW);
                    Canvas.SetTop(bar, histCanvas.Height - bar.Height);
                    histCanvas.Children.Add(bar);
                }
            }
            catch
            {
                // Histogram is best-effort.
            }
        }

        async Task PreviewAsync()
        {
            if (!dialogOpen || baseline is null)
            {
                return;
            }

            if (previewBusy)
            {
                previewQueued = true;
                return;
            }

            previewBusy = true;
            try
            {
                do
                {
                    previewQueued = false;
                    var snap = baseline.Clone();
                    _document.RestoreCheckpoint(snap);
                    var adj = BuildAdjustments();
                    if (!adj.IsIdentity)
                    {
                        await _processor.AdjustAsync(_document, adj);
                    }

                    await RefreshAsync();
                    await UpdateHistogramAsync();
                }
                while (previewQueued && dialogOpen);
            }
            catch (Exception ex)
            {
                _status.Text = "Preview failed: " + ex.Message;
            }
            finally
            {
                previewBusy = false;
            }
        }

        void RequestPreview() => _ = PreviewAsync();

        var reset = new Button { Content = "Reset all", HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            brightness.Value = 0;
            contrast.Value = 0;
            saturation.Value = 0;
            highlights.Value = 0;
            shadows.Value = 0;
            blackPoint.Value = 0;
            whitePoint.Value = 100;
            gamma.Value = 1.0;
            temperature.Value = 0;
            tint.Value = 0;
            sharpness.Value = 0;
            autoLevels.IsChecked = false;
            sepia.IsChecked = false;
            RequestPreview();
        };
        autoLevels.Checked += (_, _) => RequestPreview();
        autoLevels.Unchecked += (_, _) => RequestPreview();
        sepia.Checked += (_, _) => RequestPreview();
        sepia.Unchecked += (_, _) => RequestPreview();

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Live preview on the image. ↺ resets one control; Reset all clears everything. Apply keeps the preview (Undo / Ctrl+Z).",
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 320,
                },
                histLabel,
                histCanvas,
                autoLevels,
                WithReset(brightness, 0, RequestPreview),
                WithReset(contrast, 0, RequestPreview),
                WithReset(highlights, 0, RequestPreview),
                WithReset(shadows, 0, RequestPreview),
                WithReset(blackPoint, 0, RequestPreview),
                WithReset(whitePoint, 100, RequestPreview),
                WithReset(gamma, 1.0, RequestPreview),
                WithReset(saturation, 0, RequestPreview),
                WithReset(temperature, 0, RequestPreview),
                WithReset(tint, 0, RequestPreview),
                WithReset(sharpness, 0, RequestPreview),
                sepia,
                reset,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Color adjustments",
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        try
        {
            baseline = _document.CaptureCheckpoint();
            await UpdateHistogramAsync();
            var result = await dialog.ShowAsync();
            dialogOpen = false;
            if (result != ContentDialogResult.Primary)
            {
                if (baseline is not null)
                {
                    _document.RestoreCheckpoint(baseline);
                    baseline = null;
                    await RefreshAsync();
                }

                _status.Text = "Adjustments cancelled.";
                return;
            }

            var final = BuildAdjustments();
            if (final.IsIdentity)
            {
                if (baseline is not null)
                {
                    _document.RestoreCheckpoint(baseline);
                    baseline = null;
                    await RefreshAsync();
                }

                _status.Text = "No adjustments to apply.";
                return;
            }

            // Preview already matches final values; keep it and record undo from the pre-dialog state.
            PushUndo(baseline!);
            baseline = null;
            UpdateStatus();
            _status.Text = "Color adjustments applied.";
        }
        catch (Exception ex)
        {
            dialogOpen = false;
            if (baseline is not null)
            {
                try
                {
                    _document.RestoreCheckpoint(baseline);
                    baseline = null;
                    await RefreshAsync();
                }
                catch
                {
                    baseline?.Dispose();
                    baseline = null;
                }
            }

            _status.Text = "Adjust failed: " + ex.Message;
        }
        finally
        {
            dialogOpen = false;
            baseline?.Dispose();
        }
    }

    private async Task StampSignatureAsync()
    {
        if (_signatures is null)
        {
            _status.Text = "Signature library unavailable.";
            return;
        }

        try
        {
            var entries = await _signatures.ListAsync();
            if (entries.Count == 0)
            {
                _status.Text = "No signatures saved — add one from a PDF Sign toolbar first.";
                return;
            }

            var list = new ListView
            {
                ItemsSource = entries.Select(e =>
                    string.IsNullOrWhiteSpace(e.Description) ? e.Name : $"{e.Name} — {e.Description}").ToList(),
                SelectionMode = ListViewSelectionMode.Single,
                SelectedIndex = 0,
                MaxHeight = 240,
                Width = 320,
            };
            AutomationProperties.SetName(list, "Saved signatures");
            var dialog = new ContentDialog
            {
                Title = "Stamp signature",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Places at selection top-left (or 0,0). Undo with Ctrl+Z.",
                            Opacity = 0.75,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        list,
                    },
                },
                PrimaryButtonText = "Stamp",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedIndex < 0)
            {
                return;
            }

            var entry = entries[list.SelectedIndex];
            await using var stream = await _signatures.OpenImageAsync(entry.Id);
            var temp = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "glyph-stamp-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                await using (var file = System.IO.File.Create(temp))
                {
                    await stream.CopyToAsync(file);
                }

                var destX = _pixelSelection?.X ?? 0;
                var destY = _pixelSelection?.Y ?? 0;
                await MutateAsync(
                    () => _processor.PasteFileAsync(_document, temp, destX, destY),
                    $"Stamped “{entry.Name}” at ({destX},{destY}).");
            }
            finally
            {
                try { System.IO.File.Delete(temp); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Stamp failed: " + ex.Message;
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

    private async Task PrintImageAsync()
    {
        try
        {
            var scaleBox = new ComboBox
            {
                Header = "Scale",
                Width = 240,
                ItemsSource = new[] { "Fit to printable area", "Fill page", "Actual size" },
                SelectedIndex = 0,
            };
            var nUpBox = new ComboBox
            {
                Header = "Pages per sheet",
                Width = 240,
                ItemsSource = new[] { "1", "2", "4" },
                SelectedIndex = 0,
            };
            var grayscale = new CheckBox { Content = "Grayscale" };
            var center = new CheckBox { Content = "Center on page", IsChecked = true };
            var includeSiblings = new CheckBox
            {
                Content = $"Also print other folder images ({Math.Max(0, _siblings.Count - 1)})",
                IsEnabled = _siblings.Count > 1 && _decoder is not null,
            };
            var dialog = new ContentDialog
            {
                Title = "Print image",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "System print dialog sets printer, copies, collate, duplex, and paper.",
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 320,
                            Opacity = 0.8,
                        },
                        scaleBox,
                        nUpBox,
                        grayscale,
                        center,
                        includeSiblings,
                    },
                },
                PrimaryButtonText = "Print…",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                _status.Text = "Print cancelled.";
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

            var bitmaps = new List<WriteableBitmap>();
            async Task AddDocAsync(IImageDocument doc)
            {
                // Use unmanaged pixels for print fidelity (no display soft-proof).
                var managed = doc.ColorManagedDisplay;
                doc.ColorManagedDisplay = false;
                try
                {
                    var buffer = await doc.GetPixelsAsync();
                    var pixels = buffer.BgraPixels.ToArray();
                    if (grayscale.IsChecked == true)
                    {
                        DocumentPrintHelper.ApplyGrayscale(pixels);
                    }

                    bitmaps.Add(await DocumentPrintHelper.ToWriteableBitmapAsync(
                        buffer.Width,
                        buffer.Height,
                        pixels));
                }
                finally
                {
                    doc.ColorManagedDisplay = managed;
                }
            }

            await AddDocAsync(_document);
            if (includeSiblings.IsChecked == true && _decoder is not null && !string.IsNullOrWhiteSpace(_document.Path))
            {
                var current = System.IO.Path.GetFullPath(_document.Path);
                foreach (var sibling in _siblings)
                {
                    if (string.Equals(System.IO.Path.GetFullPath(sibling), current, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        await using var doc = await _decoder.OpenAsync(sibling);
                        await AddDocAsync(doc);
                    }
                    catch (Exception ex)
                    {
                        _status.Text = $"Print skipped {System.IO.Path.GetFileName(sibling)}: {ex.Message}";
                    }
                }
            }

            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable for print.");
            using var helper = new DocumentPrintHelper(
                window,
                jobName: System.IO.Path.GetFileName(_document.Path) ?? "Glyph image",
                scaleMode: scaleMode,
                center: center.IsChecked == true,
                autoRotate: true,
                pagesPerSheet: pagesPerSheet);
            await helper.PrintAsync(bitmaps);
            _status.Text = $"Print UI shown · {bitmaps.Count} image(s)"
                + (pagesPerSheet > 1 ? $" · {pagesPerSheet}-up." : ".");
        }
        catch (Exception ex)
        {
            _status.Text = "Print failed: " + ex.Message;
        }
    }

    private async Task ConvertAsync()
    {
        var formatBox = new ComboBox
        {
            Header = "Format",
            Width = 200,
            ItemsSource = new[] { "WebP", "TIFF", "BMP", "GIF", "AVIF", "JPEG 2000", "HEIC", "PDF" },
            SelectedIndex = 0,
        };
        var quality = new Slider
        {
            Header = "Quality (1–100)",
            Minimum = 1,
            Maximum = 100,
            Value = 85,
            StepFrequency = 1,
            Width = 240,
        };
        var lossless = new CheckBox { Content = "Lossless WebP", IsChecked = false };
        var preserveAlpha = new CheckBox { Content = "Preserve alpha", IsChecked = true };
        var stripByDefault = false;
        try
        {
            stripByDefault = App.Services.GetService<ISettingsStore>()?.Current.StripMetadataByDefault == true;
        }
        catch
        {
            // DI may be unavailable in tests.
        }

        var preserveMeta = new CheckBox
        {
            Content = "Preserve metadata (EXIF/IPTC/XMP)",
            IsChecked = !stripByDefault,
        };
        var embedSrgb = new CheckBox { Content = "Embed sRGB ICC profile", IsChecked = false };
        var tiffCompression = new ComboBox
        {
            Header = "TIFF compression",
            Width = 200,
            Visibility = Visibility.Collapsed,
            ItemsSource = new[] { "Default", "None", "LZW", "ZIP", "JPEG" },
            SelectedIndex = 0,
        };
        void SyncWebpOptions()
        {
            var selected = formatBox.SelectedItem as string;
            var isWebp = selected == "WebP";
            var isTiff = selected == "TIFF";
            var needsQuality = selected is "WebP" or "AVIF" or "HEIC"
                || (isTiff && tiffCompression.SelectedIndex == 4);
            quality.Visibility = needsQuality && !(isWebp && lossless.IsChecked == true)
                ? Visibility.Visible
                : Visibility.Collapsed;
            lossless.Visibility = isWebp ? Visibility.Visible : Visibility.Collapsed;
            tiffCompression.Visibility = isTiff ? Visibility.Visible : Visibility.Collapsed;
        }

        formatBox.SelectionChanged += (_, _) => SyncWebpOptions();
        lossless.Checked += (_, _) => SyncWebpOptions();
        lossless.Unchecked += (_, _) => SyncWebpOptions();
        tiffCompression.SelectionChanged += (_, _) => SyncWebpOptions();
        SyncWebpOptions();

        var panel = new StackPanel
        {
            Spacing = 8,
            Children = { formatBox, lossless, tiffCompression, quality, preserveAlpha, preserveMeta, embedSrgb },
        };
        var dialog = new ContentDialog
        {
            Title = "Convert image",
            Content = panel,
            PrimaryButtonText = "Export…",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var (format, extension) = (formatBox.SelectedItem as string) switch
        {
            "WebP" => (ImageEncodeFormat.Webp, ".webp"),
            "TIFF" => (ImageEncodeFormat.Tiff, ".tif"),
            "BMP" => (ImageEncodeFormat.Bmp, ".bmp"),
            "GIF" => (ImageEncodeFormat.Gif, ".gif"),
            "AVIF" => (ImageEncodeFormat.Avif, ".avif"),
            "JPEG 2000" => (ImageEncodeFormat.Jpeg2000, ".jp2"),
            "HEIC" => (ImageEncodeFormat.Heic, ".heic"),
            "PDF" => (ImageEncodeFormat.Pdf, ".pdf"),
            _ => (ImageEncodeFormat.Webp, ".webp"),
        };

        ImageEncodeOptions? options = null;
        var keepAlpha = preserveAlpha.IsChecked != false;
        var keepMeta = preserveMeta.IsChecked != false;
        var embedProfile = embedSrgb.IsChecked == true;
        ImageTiffCompression? tiffComp = format == ImageEncodeFormat.Tiff
            ? tiffCompression.SelectedIndex switch
            {
                1 => ImageTiffCompression.None,
                2 => ImageTiffCompression.Lzw,
                3 => ImageTiffCompression.Zip,
                4 => ImageTiffCompression.Jpeg,
                _ => ImageTiffCompression.Default,
            }
            : null;
        if (format == ImageEncodeFormat.Webp)
        {
            options = lossless.IsChecked == true
                ? new ImageEncodeOptions(Lossless: true, PreserveAlpha: keepAlpha, PreserveMetadata: keepMeta, EmbedSrgbProfile: embedProfile)
                : new ImageEncodeOptions(Quality: (int)quality.Value, PreserveAlpha: keepAlpha, PreserveMetadata: keepMeta, EmbedSrgbProfile: embedProfile);
        }
        else if (format is ImageEncodeFormat.Avif or ImageEncodeFormat.Heic)
        {
            options = new ImageEncodeOptions(
                Quality: (int)quality.Value,
                PreserveAlpha: keepAlpha,
                PreserveMetadata: keepMeta,
                EmbedSrgbProfile: embedProfile);
        }
        else if (format == ImageEncodeFormat.Tiff)
        {
            options = new ImageEncodeOptions(
                Quality: tiffComp == ImageTiffCompression.Jpeg ? (int)quality.Value : null,
                PreserveAlpha: keepAlpha,
                PreserveMetadata: keepMeta,
                EmbedSrgbProfile: embedProfile,
                TiffCompression: tiffComp);
        }
        else
        {
            options = new ImageEncodeOptions(
                PreserveAlpha: keepAlpha,
                PreserveMetadata: keepMeta,
                EmbedSrgbProfile: embedProfile);
        }

        await ExportAsync(format, extension, options);
    }

    private async Task ExportJpegAsync()
    {
        var quality = new Slider
        {
            Header = "JPEG quality (1–100)",
            Minimum = 1,
            Maximum = 100,
            Value = 90,
            StepFrequency = 1,
            Width = 240,
        };
        var dialog = new ContentDialog
        {
            Title = "Export JPEG",
            Content = quality,
            PrimaryButtonText = "Export…",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await ExportAsync(ImageEncodeFormat.Jpeg, ".jpg", new ImageEncodeOptions(Quality: (int)quality.Value));
    }

    private async Task ShowMetadataAsync()
    {
        try
        {
            var info = await _document.GetMetadataAsync();
            long? fileBytes = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(_document.Path) && System.IO.File.Exists(_document.Path))
                {
                    fileBytes = new System.IO.FileInfo(_document.Path).Length;
                }
            }
            catch
            {
                // ignore
            }

            static string Bytes(long? size) => ByteSizeFormat.FormatOptional(size);

            var summary = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                Margin = new Thickness(0, 0, 0, 8),
                Text =
                    $"Path: {(_document.Path ?? "—")}\n"
                    + $"Dimensions: {_document.PixelWidth} × {_document.PixelHeight} px\n"
                    + $"Format: {_document.FormatName}\n"
                    + $"File size: {Bytes(fileBytes)}",
            };
            var list = new ListView
            {
                ItemsSource = info.Entries
                    .Select(e => $"{e.Group} · {e.Name}: {e.Value}")
                    .ToList(),
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 360,
                Width = 420,
            };

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var assignSrgb = new Button { Content = "Assign sRGB" };
            assignSrgb.Click += async (_, _) =>
            {
                await MutateAsync(
                    () => _processor.AssignColorProfileAsync(_document, ImageColorProfileKind.Srgb),
                    "Assigned sRGB ICC profile.");
            };
            var convertSrgb = new Button { Content = "Convert → sRGB" };
            convertSrgb.Click += async (_, _) =>
            {
                await MutateAsync(
                    () => _processor.ConvertColorProfileAsync(_document, ImageColorProfileKind.Srgb),
                    "Converted pixels to sRGB.");
            };
            actions.Children.Add(assignSrgb);
            actions.Children.Add(convertSrgb);

            var colorManaged = new CheckBox
            {
                Content = "Color-managed display (ICC → sRGB)",
                IsChecked = _document.ColorManagedDisplay,
            };
            colorManaged.Checked += async (_, _) =>
            {
                _document.ColorManagedDisplay = true;
                await RefreshAsync();
                _status.Text = "Color-managed display on.";
            };
            colorManaged.Unchecked += async (_, _) =>
            {
                _document.ColorManagedDisplay = false;
                await RefreshAsync();
                _status.Text = "Color-managed display off.";
            };
            var softProof = new CheckBox
            {
                Content = "Soft-proof Adobe RGB",
                IsChecked = _document.SoftProofProfile == ImageColorProfileKind.AdobeRgb,
            };
            softProof.Checked += async (_, _) =>
            {
                _document.SoftProofProfile = ImageColorProfileKind.AdobeRgb;
                _document.ColorManagedDisplay = true;
                colorManaged.IsChecked = true;
                await RefreshAsync();
                _status.Text = "Soft-proof Adobe RGB on.";
            };
            softProof.Unchecked += async (_, _) =>
            {
                _document.SoftProofProfile = null;
                await RefreshAsync();
                _status.Text = "Soft-proof off.";
            };
            var intentBox = new ComboBox
            {
                Width = 160,
                ItemsSource = new[] { "Perceptual", "Relative", "Saturation", "Absolute" },
                SelectedIndex = (int)_document.DisplayRenderingIntent,
            };
            intentBox.SelectionChanged += async (_, _) =>
            {
                if (intentBox.SelectedIndex < 0)
                {
                    return;
                }

                _document.DisplayRenderingIntent = (ImageRenderingIntent)intentBox.SelectedIndex;
                await RefreshAsync();
                _status.Text = "Rendering intent: " + intentBox.SelectedItem;
            };
            var colorPanel = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    colorManaged,
                    softProof,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Intent", VerticalAlignment = VerticalAlignment.Center },
                            intentBox,
                        },
                    },
                },
            };
            if (info.GpsLatitude is double lat && info.GpsLongitude is double lon)
            {
                var coords = $"{lat:0.######}, {lon:0.######}";
                var copy = new Button { Content = "Copy GPS" };
                copy.Click += async (_, _) =>
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(coords);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    _status.Text = "Copied GPS " + coords;
                };
                var maps = new Button { Content = "Open map" };
                maps.Click += async (_, _) =>
                {
                    var uri = new Uri(
                        "https://www.openstreetmap.org/?mlat="
                        + Uri.EscapeDataString(lat.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        + "&mlon="
                        + Uri.EscapeDataString(lon.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        + "#map=15/"
                        + Uri.EscapeDataString(lat.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        + "/"
                        + Uri.EscapeDataString(lon.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                };
                var strip = new Button { Content = "Remove GPS" };
                strip.Click += async (_, _) =>
                {
                    await MutateAsync(() => _processor.RemoveGpsMetadataAsync(_document), "GPS metadata removed.");
                };
                actions.Children.Add(copy);
                actions.Children.Add(maps);
                actions.Children.Add(strip);
            }

            var panel = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    summary,
                    new TextBlock
                    {
                        Text = $"{info.FormatName} · {info.PixelWidth}×{info.PixelHeight}"
                            + (info.Make is null ? string.Empty : $" · {info.Make} {info.Model}".TrimEnd())
                            + (info.ColorSpace is null ? string.Empty : $" · {info.ColorSpace}")
                            + (info.HasIccProfile ? " · ICC" : " · no ICC"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new TextBlock
                    {
                        Text = ImageDescriptiveMetadataSummary.Format(info),
                        Opacity = 0.8,
                        TextWrapping = TextWrapping.Wrap,
                        Visibility = ImageDescriptiveMetadataSummary.HasAny(info)
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                    },
                    list,
                    colorPanel,
                    actions,
                },
            };

            var dialog = new ContentDialog
            {
                Title = "Image metadata",
                Content = panel,
                PrimaryButtonText = "Edit…",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await EditDescriptiveMetadataAsync(info);
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Metadata failed: " + ex.Message;
        }
    }

    /// <summary>File → Properties entry point (F48).</summary>
    public Task ShowPropertiesAsync() => ShowMetadataAsync();

    /// <summary>File → Save / Save As (F01-16/17).</summary>
    public async Task SaveDocumentAsync(bool saveAs)
    {
        try
        {
            if (!await EnsureMarkupFlattenedAsync())
            {
                return;
            }

            if (!saveAs && !string.IsNullOrWhiteSpace(_document.Path))
            {
                await _encoder.SaveAsync(_document, _document.Path);
                ClearUnsavedEdits();
                _status.Text = "Saved " + System.IO.Path.GetFileName(_document.Path);
                App.CurrentApp.MainWindowInstance?.NotifyActiveDocumentSaved(_document.Path!);
                return;
            }

            var (format, extension) = GuessSaveFormat(_document.FormatName);
            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable for save picker.");
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add(format.ToString(), [extension]);
            picker.SuggestedFileName = string.IsNullOrWhiteSpace(_document.Path)
                ? "image" + extension
                : System.IO.Path.GetFileName(_document.Path);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "Save cancelled.";
                return;
            }

            await _encoder.SaveAsAsync(_document, file.Path, format);
            ClearUnsavedEdits();
            _status.Text = "Saved " + file.Name;
            App.CurrentApp.MainWindowInstance?.NotifyActiveDocumentSaved(file.Path);
        }
        catch (Exception ex)
        {
            _status.Text = "Save failed: " + ex.Message;
        }
    }

    private static (ImageEncodeFormat Format, string Extension) GuessSaveFormat(string formatName) =>
        ImageEncodeFormatResolver.FromFormatName(formatName);

    private async Task EditDescriptiveMetadataAsync(ImageMetadataInfo current)
    {
        var title = new TextBox { Header = "Title", Text = current.Title ?? string.Empty, Width = 360 };
        var description = new TextBox
        {
            Header = "Description / caption",
            Text = current.Description ?? string.Empty,
            Width = 360,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
        };
        var keywords = new TextBox
        {
            Header = "Keywords (comma-separated)",
            Text = current.Keywords ?? string.Empty,
            Width = 360,
        };
        var copyright = new TextBox { Header = "Copyright", Text = current.Copyright ?? string.Empty, Width = 360 };
        var editDialog = new ContentDialog
        {
            Title = "Edit descriptive metadata",
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Writes IPTC title, caption, keywords, and copyright. Save the image to persist on disk.",
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 360,
                    },
                    title,
                    description,
                    keywords,
                    copyright,
                },
            },
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await editDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var metadata = new ImageDescriptiveMetadata(
            Title: title.Text,
            Description: description.Text,
            Keywords: keywords.Text,
            Copyright: copyright.Text);
        await MutateAsync(
            () => _processor.SetDescriptiveMetadataAsync(_document, metadata),
            "Descriptive metadata updated (IPTC).");
    }

    private async Task RunOcrAsync()
    {
        if (_ocr is null)
        {
            _status.Text = "OCR engine unavailable.";
            return;
        }

        var ocrFolder = false;
        if (_siblings.Count > 1 && _decoder is not null)
        {
            var chooser = new ContentDialog
            {
                Title = "OCR",
                Content = $"OCR this image, or all {_siblings.Count} images in the folder?",
                PrimaryButtonText = "This image",
                SecondaryButtonText = $"Folder ({_siblings.Count})",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            var choice = await chooser.ShowAsync();
            if (choice == ContentDialogResult.None)
            {
                _status.Text = "OCR cancelled.";
                return;
            }

            ocrFolder = choice == ContentDialogResult.Secondary;
        }

        if (ocrFolder)
        {
            await RunFolderOcrAsync();
            return;
        }

        try
        {
            _status.Text = "Running OCR…";
            var buffer = await _document.GetPixelsAsync(maxEdge: 4096);
            string? languageTag = null;
            try
            {
                languageTag = App.Services.GetService<ISettingsStore>()?.Current.OcrLanguageTag;
            }
            catch
            {
                // DI may be unavailable.
            }

            var result = await _ocr.RecognizeAsync(
                new OcrRequest(buffer.Width, buffer.Height, buffer.BgraPixels, LanguageTag: languageTag));

            var text = string.IsNullOrWhiteSpace(result.Text) ? "(no text recognized)" : result.Text;
            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Width = 480,
                Height = 320,
            };
            var copy = new Button { Content = "Copy text", Margin = new Thickness(0, 8, 0, 0) };
            copy.Click += (_, _) =>
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(result.Text ?? string.Empty);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                _status.Text = "OCR text copied.";
            };

            var panel = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{result.Lines.Count} line(s) · {result.Lines.Sum(l => l.Words.Count)} word(s)",
                        Opacity = 0.75,
                    },
                    box,
                    copy,
                },
            };

            var dialog = new ContentDialog
            {
                Title = "OCR result",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
            _status.Text = string.IsNullOrWhiteSpace(result.Text)
                ? "OCR finished — no text."
                : $"OCR finished — {result.Lines.Count} line(s).";
        }
        catch (Exception ex)
        {
            _status.Text = "OCR failed: " + ex.Message;
        }
    }

    private async Task RunFolderOcrAsync()
    {
        if (_ocr is null || _decoder is null || _siblings.Count == 0)
        {
            _status.Text = "Folder OCR unavailable.";
            return;
        }

        string? languageTag = null;
        try
        {
            languageTag = App.Services.GetService<ISettingsStore>()?.Current.OcrLanguageTag;
        }
        catch
        {
            // DI may be unavailable.
        }

        var sections = new List<string>(_siblings.Count);
        var (updated, cancelled) = await RunBatchWithProgressAsync(
            "OCR folder images",
            _siblings.ToList(),
            async (path, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await using var doc = await _decoder.OpenAsync(path, ct);
                var buffer = await doc.GetPixelsAsync(maxEdge: 4096, ct);
                var result = await _ocr.RecognizeAsync(
                    new OcrRequest(buffer.Width, buffer.Height, buffer.BgraPixels, LanguageTag: languageTag),
                    ct);
                var name = System.IO.Path.GetFileName(path);
                var body = string.IsNullOrWhiteSpace(result.Text) ? "(no text recognized)" : result.Text.Trim();
                sections.Add($"--- {name} ---\n{body}");
                return true;
            });

        if (sections.Count == 0)
        {
            _status.Text = cancelled ? "Folder OCR cancelled." : "Folder OCR produced no results.";
            return;
        }

        var combined = string.Join("\n\n", sections);
        var box = new TextBox
        {
            Text = combined,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = 520,
            Height = 360,
        };
        var copy = new Button { Content = "Copy all", Margin = new Thickness(0, 8, 0, 0) };
        copy.Click += (_, _) =>
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(combined);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            _status.Text = "Folder OCR text copied.";
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = cancelled
                        ? $"Cancelled after {updated} image(s)."
                        : $"OCR’d {updated} image(s).",
                    Opacity = 0.75,
                },
                box,
                copy,
            },
        };
        var dialog = new ContentDialog
        {
            Title = "Folder OCR results",
            Content = panel,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
        _status.Text = cancelled
            ? $"Folder OCR cancelled after {updated} image(s)."
            : $"Folder OCR finished — {updated} image(s).";
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!await EnsureMarkupFlattenedAsync())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_document.Path))
            {
                await ExportAsync(ImageEncodeFormat.Png, ".png");
                return;
            }

            await _encoder.SaveAsync(_document, _document.Path);
            _status.Text = "Saved " + System.IO.Path.GetFileName(_document.Path);
        }
        catch (Exception ex)
        {
            _status.Text = "Save failed: " + ex.Message;
        }
    }

    private async Task ExportAsync(ImageEncodeFormat format, string extension, ImageEncodeOptions? options = null)
    {
        try
        {
            if (!await EnsureMarkupFlattenedAsync())
            {
                return;
            }

            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable for save picker.");
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add(format.ToString(), [extension]);
            picker.SuggestedFileName = "image" + extension;
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "Export cancelled.";
                return;
            }

            await _encoder.SaveAsAsync(_document, file.Path, format, options);
            _status.Text = "Exported " + file.Name;
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed: " + ex.Message;
        }
    }

    private async Task<bool> EnsureMarkupFlattenedAsync()
    {
        if (_markupStrokes.Count == 0 && _markupShapes.Count == 0)
        {
            return true;
        }

        var total = _markupStrokes.Count + _markupShapes.Count;
        var dialog = new ContentDialog
        {
            Title = "Flatten markup?",
            Content = $"{total} markup item(s) will be baked into pixels before saving.",
            PrimaryButtonText = "Flatten & continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = "Save cancelled — markup still on overlay.";
            return false;
        }

        await FlattenMarkupAsync();
        return true;
    }

    private async Task ToggleDrawModeAsync()
    {
        if (_drawMode)
        {
            ExitDrawMode();
            _status.Text = "Draw mode off.";
            return;
        }

        if (_cropMode)
        {
            ExitCropMode();
        }

        if (_selectionMode)
        {
            ExitSelectionMode(keepSelection: false);
        }

        var toolBox = new ComboBox
        {
            Header = "Tool",
            Width = 200,
            ItemsSource = new[] { "Freehand", "Rectangle", "Ellipse", "Line", "Arrow", "Text", "Callout" },
            SelectedIndex = 0,
        };
        var widthSlider = new Slider
        {
            Header = "Stroke width (px)",
            Minimum = 1,
            Maximum = 32,
            Value = _drawWidthPixels,
            StepFrequency = 1,
            Width = 240,
        };
        var colorBox = new ComboBox
        {
            Header = "Color",
            Width = 200,
            ItemsSource = new[] { "Red", "Black", "White", "Yellow", "Blue", "Green" },
            SelectedIndex = 0,
        };
        var panel = new StackPanel { Spacing = 8, Children = { toolBox, colorBox, widthSlider } };
        var dialog = new ContentDialog
        {
            Title = "Draw markup",
            Content = panel,
            PrimaryButtonText = "Start drawing",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _drawColor = (colorBox.SelectedItem as string) switch
        {
            "Black" => Windows.UI.Color.FromArgb(255, 0, 0, 0),
            "White" => Windows.UI.Color.FromArgb(255, 255, 255, 255),
            "Yellow" => Windows.UI.Color.FromArgb(255, 255, 215, 0),
            "Blue" => Windows.UI.Color.FromArgb(255, 30, 144, 255),
            "Green" => Windows.UI.Color.FromArgb(255, 34, 139, 34),
            _ => Windows.UI.Color.FromArgb(255, 220, 20, 60),
        };
        _drawWidthPixels = widthSlider.Value;
        _markupShapeTool = toolBox.SelectedIndex switch
        {
            1 => ImageMarkupShapeKind.Rectangle,
            2 => ImageMarkupShapeKind.Ellipse,
            3 => ImageMarkupShapeKind.Line,
            4 => ImageMarkupShapeKind.Arrow,
            5 => ImageMarkupShapeKind.Text,
            6 => ImageMarkupShapeKind.Callout,
            _ => null,
        };
        if (_markupShapeTool is ImageMarkupShapeKind.Text or ImageMarkupShapeKind.Callout)
        {
            var isCallout = _markupShapeTool == ImageMarkupShapeKind.Callout;
            var textBox = new TextBox
            {
                Header = isCallout ? "Callout text" : "Text",
                Text = isCallout ? "Note" : "Label",
                Width = 280,
            };
            var fontSlider = new Slider
            {
                Header = "Font size (px)",
                Minimum = 8,
                Maximum = 96,
                Value = isCallout ? 18 : 24,
                StepFrequency = 1,
                Width = 240,
            };
            var textDialog = new ContentDialog
            {
                Title = isCallout ? "Callout markup" : "Text markup",
                Content = new StackPanel { Spacing = 8, Children = { textBox, fontSlider } },
                PrimaryButtonText = isCallout ? "Draw callout" : "Place text",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await textDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            _pendingText = string.IsNullOrWhiteSpace(textBox.Text)
                ? (isCallout ? "Note" : "Label")
                : textBox.Text.Trim();
            _pendingFontSize = fontSlider.Value;
            _status.Text = isCallout
                ? "Callout markup — drag a box on the image (Esc exits)."
                : "Text markup — click on image to place (Esc exits).";
        }
        else
        {
            _pendingText = null;
            var toolName = toolBox.SelectedItem as string ?? "Freehand";
            _status.Text = $"{toolName} markup — drag on image (Esc exits; Ctrl+Z undoes).";
        }

        _drawMode = true;
        _markupOverlay.IsHitTestVisible = true;
        _drawButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 220, 20, 60));
        UpdateFlattenButtonVisibility();
    }

    private void ExitDrawMode()
    {
        _drawMode = false;
        _drawDragging = false;
        _markupShapeTool = null;
        _pendingText = null;
        _drawPoints.Clear();
        if (_activeDrawPolyline is not null)
        {
            _markupOverlay.Children.Remove(_activeDrawPolyline);
            _activeDrawPolyline = null;
        }

        ClearActiveShapePreview();
        _markupOverlay.IsHitTestVisible = false;
        _drawButton.Background = null;
        UpdateFlattenButtonVisibility();
    }

    private void UpdateFlattenButtonVisibility()
    {
        _flattenMarkupButton.Visibility =
            _markupStrokes.Count > 0 || _markupShapes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_markupStrokes.Count > 0 || _markupShapes.Count > 0)
        {
            _onEdited?.Invoke();
        }
    }

    private async Task FlattenMarkupAsync()
    {
        if (_markupStrokes.Count == 0 && _markupShapes.Count == 0)
        {
            _status.Text = "No markup to flatten.";
            return;
        }

        var layer = new ImageMarkupLayer(_markupStrokes.ToList(), _markupShapes.ToList());
        await MutateAsync(
            () => _processor.FlattenMarkupAsync(_document, layer),
            $"Flattened {layer.Count} markup item(s).");
        _markupStrokes.Clear();
        _markupShapes.Clear();
        _markupUndoWasShape.Clear();
        RebuildMarkupOverlay();
        UpdateFlattenButtonVisibility();
    }

    private void UndoMarkupItem()
    {
        if (_markupUndoWasShape.Count == 0)
        {
            return;
        }

        var wasShape = _markupUndoWasShape[^1];
        _markupUndoWasShape.RemoveAt(_markupUndoWasShape.Count - 1);
        if (wasShape)
        {
            if (_markupShapes.Count > 0)
            {
                _markupShapes.RemoveAt(_markupShapes.Count - 1);
            }
        }
        else if (_markupStrokes.Count > 0)
        {
            _markupStrokes.RemoveAt(_markupStrokes.Count - 1);
        }

        RebuildMarkupOverlay();
        UpdateFlattenButtonVisibility();
        var remaining = _markupStrokes.Count + _markupShapes.Count;
        _status.Text = remaining == 0
            ? "Markup undone."
            : $"Markup undone ({remaining} left).";
    }

    private void ClearActiveShapePreview()
    {
        if (_activeShapePreview is not null)
        {
            _markupOverlay.Children.Remove(_activeShapePreview);
            _activeShapePreview = null;
        }
    }

    private void RebuildMarkupOverlay()
    {
        _markupOverlay.Children.Clear();
        _activeDrawPolyline = null;
        _activeShapePreview = null;
        if (_displayWidth <= 0 || _document.PixelWidth <= 0)
        {
            return;
        }

        var scaleX = _displayWidth / (double)_document.PixelWidth;
        var scaleY = _displayHeight / (double)_document.PixelHeight;
        foreach (var stroke in _markupStrokes)
        {
            var poly = new Polyline
            {
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(stroke.A, stroke.R, stroke.G, stroke.B)),
                StrokeThickness = Math.Max(1, stroke.WidthPixels * ((scaleX + scaleY) / 2.0)),
                Fill = null,
                IsHitTestVisible = false,
            };
            foreach (var p in stroke.Points)
            {
                poly.Points.Add(new Windows.Foundation.Point(p.X * scaleX, p.Y * scaleY));
            }

            _markupOverlay.Children.Add(poly);
        }

        foreach (var shape in _markupShapes)
        {
            AddShapeVisual(shape, scaleX, scaleY);
        }
    }

    private void AddShapeVisual(ImageMarkupShape shape, double scaleX, double scaleY)
    {
        var brush = new SolidColorBrush(Windows.UI.Color.FromArgb(shape.A, shape.R, shape.G, shape.B));
        var thickness = Math.Max(1, shape.WidthPixels * ((scaleX + scaleY) / 2.0));
        var x1 = shape.X1 * scaleX;
        var y1 = shape.Y1 * scaleY;
        var x2 = shape.X2 * scaleX;
        var y2 = shape.Y2 * scaleY;
        switch (shape.Kind)
        {
            case ImageMarkupShapeKind.Rectangle:
            {
                var rect = new Rectangle
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    Fill = null,
                    Width = Math.Abs(x2 - x1),
                    Height = Math.Abs(y2 - y1),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(rect, Math.Min(x1, x2));
                Canvas.SetTop(rect, Math.Min(y1, y2));
                _markupOverlay.Children.Add(rect);
                break;
            }
            case ImageMarkupShapeKind.Ellipse:
            {
                var ellipse = new Ellipse
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    Fill = null,
                    Width = Math.Abs(x2 - x1),
                    Height = Math.Abs(y2 - y1),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(ellipse, Math.Min(x1, x2));
                Canvas.SetTop(ellipse, Math.Min(y1, y2));
                _markupOverlay.Children.Add(ellipse);
                break;
            }
            case ImageMarkupShapeKind.Line:
            case ImageMarkupShapeKind.Arrow:
            {
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = brush,
                    StrokeThickness = thickness,
                    IsHitTestVisible = false,
                };
                _markupOverlay.Children.Add(line);
                if (shape.Kind == ImageMarkupShapeKind.Arrow)
                {
                    AddArrowHeadVisual(x1, y1, x2, y2, brush, thickness);
                }

                break;
            }
            case ImageMarkupShapeKind.Text:
            {
                var label = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(shape.Text) ? "Text" : shape.Text,
                    Foreground = brush,
                    FontSize = Math.Max(8, shape.FontSizePixels * ((scaleX + scaleY) / 2.0)),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(label, x1);
                Canvas.SetTop(label, y1);
                _markupOverlay.Children.Add(label);
                break;
            }
            case ImageMarkupShapeKind.Callout:
            {
                var left = Math.Min(x1, x2);
                var top = Math.Min(y1, y2);
                var w = Math.Abs(x2 - x1);
                var h = Math.Abs(y2 - y1);
                var rect = new Rectangle
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, shape.R, shape.G, shape.B)),
                    Width = w,
                    Height = h,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                _markupOverlay.Children.Add(rect);
                var midX = left + (w / 2.0);
                var tipY = top + h + Math.Max(12, h * 0.25);
                var tipSpread = Math.Max(8, w * 0.12);
                _markupOverlay.Children.Add(new Line
                {
                    X1 = midX - tipSpread,
                    Y1 = top + h,
                    X2 = midX,
                    Y2 = tipY,
                    Stroke = brush,
                    StrokeThickness = thickness,
                    IsHitTestVisible = false,
                });
                _markupOverlay.Children.Add(new Line
                {
                    X1 = midX + tipSpread,
                    Y1 = top + h,
                    X2 = midX,
                    Y2 = tipY,
                    Stroke = brush,
                    StrokeThickness = thickness,
                    IsHitTestVisible = false,
                });
                var label = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(shape.Text) ? "Callout" : shape.Text,
                    Foreground = brush,
                    FontSize = Math.Max(8, shape.FontSizePixels * ((scaleX + scaleY) / 2.0)),
                    IsHitTestVisible = false,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = Math.Max(20, w - 8),
                };
                Canvas.SetLeft(label, left + 4);
                Canvas.SetTop(label, top + 2);
                _markupOverlay.Children.Add(label);
                break;
            }
        }
    }

    private void AddArrowHeadVisual(
        double x1,
        double y1,
        double x2,
        double y2,
        Brush brush,
        double thickness)
    {
        var angle = Math.Atan2(y2 - y1, x2 - x1);
        var head = Math.Max(8, thickness * 4);
        var a1 = angle + Math.PI - (Math.PI / 6);
        var a2 = angle + Math.PI + (Math.PI / 6);
        _markupOverlay.Children.Add(new Line
        {
            X1 = x2,
            Y1 = y2,
            X2 = x2 + (head * Math.Cos(a1)),
            Y2 = y2 + (head * Math.Sin(a1)),
            Stroke = brush,
            StrokeThickness = thickness,
            IsHitTestVisible = false,
        });
        _markupOverlay.Children.Add(new Line
        {
            X1 = x2,
            Y1 = y2,
            X2 = x2 + (head * Math.Cos(a2)),
            Y2 = y2 + (head * Math.Sin(a2)),
            Stroke = brush,
            StrokeThickness = thickness,
            IsHitTestVisible = false,
        });
    }

    private void MarkupOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_drawMode)
        {
            return;
        }

        _drawDragging = true;
        _drawPoints.Clear();
        ClearActiveShapePreview();
        var point = e.GetCurrentPoint(_markupOverlay).Position;
        _drawPoints.Add(point);
        if (_markupShapeTool == ImageMarkupShapeKind.Text)
        {
            // Click-to-place; commit immediately on release (or here if single click).
            _markupOverlay.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (_markupShapeTool is null)
        {
            var scale = _displayWidth > 0 && _document.PixelWidth > 0
                ? (_displayWidth / (double)_document.PixelWidth)
                : 1.0;
            _activeDrawPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(_drawColor),
                StrokeThickness = Math.Max(1, _drawWidthPixels * scale),
                Fill = null,
                IsHitTestVisible = false,
            };
            _activeDrawPolyline.Points.Add(point);
            _markupOverlay.Children.Add(_activeDrawPolyline);
        }

        _markupOverlay.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void MarkupOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_drawDragging)
        {
            return;
        }

        var point = e.GetCurrentPoint(_markupOverlay).Position;
        if (_markupShapeTool is { } kind && kind != ImageMarkupShapeKind.Text)
        {
            UpdateShapePreview(kind, _drawPoints[0], point);
            e.Handled = true;
            return;
        }

        if (_activeDrawPolyline is null)
        {
            return;
        }

        _drawPoints.Add(point);
        _activeDrawPolyline.Points.Add(point);
        e.Handled = true;
    }

    private void UpdateShapePreview(ImageMarkupShapeKind kind, Windows.Foundation.Point a, Windows.Foundation.Point b)
    {
        ClearActiveShapePreview();
        var brush = new SolidColorBrush(_drawColor);
        var scale = _displayWidth > 0 && _document.PixelWidth > 0
            ? (_displayWidth / (double)_document.PixelWidth)
            : 1.0;
        var thickness = Math.Max(1, _drawWidthPixels * scale);
        Shape preview;
        if (kind is ImageMarkupShapeKind.Line or ImageMarkupShapeKind.Arrow)
        {
            preview = new Line
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                Stroke = brush,
                StrokeThickness = thickness,
                IsHitTestVisible = false,
            };
        }
        else if (kind == ImageMarkupShapeKind.Ellipse)
        {
            preview = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = thickness,
                Fill = null,
                Width = Math.Abs(b.X - a.X),
                Height = Math.Abs(b.Y - a.Y),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(preview, Math.Min(a.X, b.X));
            Canvas.SetTop(preview, Math.Min(a.Y, b.Y));
        }
        else
        {
            preview = new Rectangle
            {
                Stroke = brush,
                StrokeThickness = thickness,
                Fill = null,
                Width = Math.Abs(b.X - a.X),
                Height = Math.Abs(b.Y - a.Y),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(preview, Math.Min(a.X, b.X));
            Canvas.SetTop(preview, Math.Min(a.Y, b.Y));
        }

        _activeShapePreview = preview;
        _markupOverlay.Children.Add(preview);
    }

    private void MarkupOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_drawDragging)
        {
            return;
        }

        _drawDragging = false;
        try { _markupOverlay.ReleasePointerCapture(e.Pointer); } catch { /* ignore */ }
        var end = e.GetCurrentPoint(_markupOverlay).Position;
        if (_displayWidth <= 0
            || _displayHeight <= 0
            || _document.PixelWidth <= 0
            || _document.PixelHeight <= 0)
        {
            ClearActiveShapePreview();
            if (_activeDrawPolyline is not null)
            {
                _markupOverlay.Children.Remove(_activeDrawPolyline);
                _activeDrawPolyline = null;
            }

            _drawPoints.Clear();
            e.Handled = true;
            return;
        }

        var scaleX = _document.PixelWidth / (double)_displayWidth;
        var scaleY = _document.PixelHeight / (double)_displayHeight;
        if (_markupShapeTool == ImageMarkupShapeKind.Text && _drawPoints.Count >= 1)
        {
            var start = _drawPoints[0];
            var shape = new ImageMarkupShape(
                ImageMarkupShapeKind.Text,
                start.X * scaleX,
                start.Y * scaleY,
                start.X * scaleX,
                start.Y * scaleY,
                _drawColor.A,
                _drawColor.R,
                _drawColor.G,
                _drawColor.B,
                _drawWidthPixels,
                _pendingText ?? "Label",
                _pendingFontSize);
            _markupShapes.Add(shape);
            _markupUndoWasShape.Add(true);
            RebuildMarkupOverlay();
            UpdateFlattenButtonVisibility();
            _status.Text = $"Text placed ({_markupStrokes.Count + _markupShapes.Count} total).";
            _drawPoints.Clear();
            e.Handled = true;
            return;
        }

        if (_markupShapeTool is { } kind && _drawPoints.Count >= 1)
        {
            ClearActiveShapePreview();
            var start = _drawPoints[0];
            if (Math.Abs(end.X - start.X) >= 2 || Math.Abs(end.Y - start.Y) >= 2)
            {
                var shape = new ImageMarkupShape(
                    kind,
                    start.X * scaleX,
                    start.Y * scaleY,
                    end.X * scaleX,
                    end.Y * scaleY,
                    _drawColor.A,
                    _drawColor.R,
                    _drawColor.G,
                    _drawColor.B,
                    _drawWidthPixels,
                    kind == ImageMarkupShapeKind.Callout ? (_pendingText ?? "Note") : null,
                    kind == ImageMarkupShapeKind.Callout ? _pendingFontSize : 16);
                _markupShapes.Add(shape);
                _markupUndoWasShape.Add(true);
                RebuildMarkupOverlay();
                UpdateFlattenButtonVisibility();
                _status.Text = $"Markup {kind} added ({_markupStrokes.Count + _markupShapes.Count} total).";
            }
        }
        else if (_drawPoints.Count >= 2)
        {
            var docPoints = _drawPoints
                .Select(p => new ImageMarkupPoint(p.X * scaleX, p.Y * scaleY))
                .ToList();
            _markupStrokes.Add(new ImageMarkupStroke(
                docPoints,
                _drawColor.A,
                _drawColor.R,
                _drawColor.G,
                _drawColor.B,
                _drawWidthPixels));
            _markupUndoWasShape.Add(false);
            UpdateFlattenButtonVisibility();
            _status.Text = $"Markup stroke added ({_markupStrokes.Count + _markupShapes.Count} total).";
        }
        else if (_activeDrawPolyline is not null)
        {
            _markupOverlay.Children.Remove(_activeDrawPolyline);
        }

        _activeDrawPolyline = null;
        _drawPoints.Clear();
        e.Handled = true;
    }

    private void UpdateStatus()
    {
        var baseStatus =
            $"{_document.FormatName} {_document.PixelWidth}×{_document.PixelHeight} · {(_zoom * 100):0}%";
        if (_displayWidth > 0
            && _displayHeight > 0
            && (_displayWidth < _document.PixelWidth || _displayHeight < _document.PixelHeight))
        {
            baseStatus += $" · view {_displayWidth}×{_displayHeight}";
        }
        if (_document.FrameCount > 1)
        {
            baseStatus += $" · frame {_document.CurrentFrameIndex + 1}/{_document.FrameCount}";
            if (_animationPlaying)
            {
                baseStatus += " · playing";
            }
        }

        if (!string.IsNullOrWhiteSpace(_document.Path) && _siblings.Count > 0)
        {
            var index = ImageFolderNavigator.IndexOf(_siblings, _document.Path);
            if (index >= 0)
            {
                baseStatus += $" · {index + 1}/{_siblings.Count}";
            }
        }

        var markupCount = _markupStrokes.Count + _markupShapes.Count;
        if (markupCount > 0)
        {
            baseStatus += $" · {markupCount} markup";
        }

        _status.Text = baseStatus;
    }
}
