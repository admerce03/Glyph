using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.App.Printing;
using Glyph.Core.Documents;
using Glyph.Core.Printing;
using Glyph.Core.IO;
using Glyph.Core.Ocr;
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
    private const int MaxEditUndo = ImageEditUndoPolicy.MaxDepth;
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
            PlaceholderText = ImageDialogPlaceholders.CropXyWh,
            Width = 140,
        };
        _siblingList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 180,
        };
        _siblingList.SelectionChanged += SiblingList_SelectionChanged;

        var zoomOut = new Button { Content = ImageViewerChromeLabels.Minus, Width = 36 };
        var zoomIn = new Button { Content = ImageViewerChromeLabels.Plus, Width = 36 };
        var fit = new Button { Content = ImageViewerChromeLabels.Fit };
        var actual = new Button { Content = ImageViewerChromeLabels.Zoom100 };
        var rotateLeft = new Button { Content = ImageViewerChromeLabels.RotateCcw };
        var rotateRight = new Button { Content = ImageViewerChromeLabels.RotateCw };
        var flipH = new Button { Content = ImageViewerChromeLabels.FlipHorizontal };
        var flipV = new Button { Content = ImageViewerChromeLabels.FlipVertical };
        var crop = new Button { Content = ImageViewerChromeLabels.Crop };
        _interactiveCropButton = new Button { Content = ImageViewerChromeLabels.CropEllipsis };
        _applyCropButton = new Button { Content = ImageViewerChromeLabels.ApplyCrop, Visibility = Visibility.Collapsed };
        _cancelCropButton = new Button { Content = ImageViewerChromeLabels.CancelCrop, Visibility = Visibility.Collapsed };
        _cropAspectBox = new ComboBox
        {
            Width = 110,
            Visibility = Visibility.Collapsed,
            ItemsSource = ImageDialogOptions.CropAspectRatios.ToList(),
            SelectedIndex = 0,
        };
        ToolTipService.SetToolTip(_cropAspectBox, ImageViewerTooltips.CropAspectFreeOriginalImageRatio);
        _selectButton = new Button { Content = ImageViewerChromeLabels.Select };
        _selectionKindBox = new ComboBox
        {
            Width = 88,
            Visibility = Visibility.Collapsed,
            ItemsSource = ImageDialogOptions.SelectionTools.ToList(),
            SelectedIndex = 0,
        };
        _selectAllButton = new Button { Content = ImageViewerChromeLabels.All, Visibility = Visibility.Collapsed };
        _invertSelButton = new Button { Content = ImageViewerChromeLabels.Invert, Visibility = Visibility.Collapsed };
        _deselectButton = new Button { Content = ImageViewerChromeLabels.Deselect, Visibility = Visibility.Collapsed };
        _copySelButton = new Button { Content = ImageViewerChromeLabels.CopySelection, Visibility = Visibility.Collapsed };
        _cutSelButton = new Button { Content = ImageViewerChromeLabels.CutSelection, Visibility = Visibility.Collapsed };
        _pasteSelButton = new Button { Content = ImageViewerChromeLabels.Paste, Visibility = Visibility.Collapsed };
        _deleteSelButton = new Button { Content = ImageViewerChromeLabels.DeleteSelection, Visibility = Visibility.Collapsed };
        _cropSelButton = new Button { Content = ImageViewerChromeLabels.CropSelection, Visibility = Visibility.Collapsed };
        _drawButton = new Button { Content = ImageViewerChromeLabels.Draw };
        _flattenMarkupButton = new Button { Content = ImageViewerChromeLabels.Flatten, Visibility = Visibility.Collapsed };
        ToolTipService.SetToolTip(_selectButton, ImageViewerTooltips.PixelSelectionDragOnImageDrag);
        ToolTipService.SetToolTip(_selectionKindBox, ImageViewerTooltips.SelectionShapeRectangleEllipseFreeformLasso);
        ToolTipService.SetToolTip(_selectAllButton, ImageViewerTooltips.SelectEntireImage);
        ToolTipService.SetToolTip(_invertSelButton, ImageViewerTooltips.InvertSelectionOperationsApplyToOutside);
        ToolTipService.SetToolTip(_deselectButton, ImageViewerTooltips.ClearSelection);
        ToolTipService.SetToolTip(_copySelButton, ImageViewerTooltips.CopySelectionToClipboardAsPng);
        ToolTipService.SetToolTip(_cutSelButton, ImageViewerTooltips.CutSelectionCopyClear);
        ToolTipService.SetToolTip(_pasteSelButton, ImageViewerTooltips.PasteAtSelectionTopLeftOr);
        ToolTipService.SetToolTip(_deleteSelButton, ImageViewerTooltips.ClearSelectionToTransparent);
        ToolTipService.SetToolTip(_cropSelButton, ImageViewerTooltips.CropImageToSelection);
        ToolTipService.SetToolTip(_drawButton, ImageViewerTooltips.FreehandMarkupNonDestructiveOverlayUntil);
        ToolTipService.SetToolTip(_flattenMarkupButton, ImageViewerTooltips.BakeMarkupStrokesIntoPixels);
        var resize = new Button { Content = ImageViewerChromeLabels.Resize };
        var adjust = new Button { Content = ImageViewerChromeLabels.Adjust };
        var bgRemove = new Button { Content = ImageViewerChromeLabels.Background };
        var stamp = new Button { Content = SignatureLibraryUi.ToolbarStamp };
        var meta = new Button { Content = ImageViewerChromeLabels.Metadata };
        var ocrButton = new Button { Content = ImageViewerChromeLabels.Ocr };
        var rotate180 = new Button { Content = ImageViewerChromeLabels.Rotate180 };
        var orient = new Button { Content = ImageViewerChromeLabels.Orient };
        var straighten = new Button { Content = ImageViewerChromeLabels.Straighten };
        var batchOrient = new Button { Content = ImageViewerChromeLabels.BatchEllipsis };
        var fullscreen = new Button { Content = ImageViewerChromeLabels.Fullscreen };
        var save = new Button { Content = ImageViewerChromeLabels.Save };
        var exportPng = new Button { Content = ImageViewerChromeLabels.ToPng };
        var exportJpeg = new Button { Content = ImageViewerChromeLabels.ToJpeg };
        var convert = new Button { Content = ImageViewerChromeLabels.Convert };
        var printImage = new Button { Content = ImageViewerChromeLabels.Print };
        var copyImage = new Button { Content = ImageViewerChromeLabels.Copy };
        var pasteImage = new Button { Content = ImageViewerChromeLabels.Paste };
        _prevButton = new Button { Content = ImageViewerChromeLabels.PrevSibling, Width = 36 };
        _nextButton = new Button { Content = ImageViewerChromeLabels.NextSibling, Width = 36 };
        _slideshowButton = new Button { Content = ImageViewerChromeLabels.Slideshow };
        _animPlayButton = new Button { Content = ImageViewerChromeLabels.Play, Visibility = Visibility.Collapsed };
        _animPrevButton = new Button { Content = ImageViewerChromeLabels.PrevFrame, Visibility = Visibility.Collapsed };
        _animNextButton = new Button { Content = ImageViewerChromeLabels.NextFrame, Visibility = Visibility.Collapsed };
        _animRestartButton = new Button { Content = ImageViewerChromeLabels.Restart, Visibility = Visibility.Collapsed };
        _animExtractButton = new Button { Content = ImageViewerChromeLabels.SaveFrame, Visibility = Visibility.Collapsed };
        _animLoopBox = new CheckBox
        {
            Content = ImageViewerChromeLabels.Loop,
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
        _undoButton = new Button { Content = ImageViewerChromeLabels.Undo, IsEnabled = false };

        ToolTipService.SetToolTip(crop, ImageViewerTooltips.CropUsingXYWH);
        ToolTipService.SetToolTip(_interactiveCropButton, ImageViewerTooltips.DragARectangleOnTheImage);
        ToolTipService.SetToolTip(_applyCropButton, ImageViewerTooltips.ApplyTheDraggedCropRectangle);
        ToolTipService.SetToolTip(_cancelCropButton, ImageViewerTooltips.CancelInteractiveCrop);
        ToolTipService.SetToolTip(resize, ImageViewerTooltips.ResizeWidthHeightWithOptionalAspect);
        ToolTipService.SetToolTip(adjust, ImageViewerTooltips.BrightnessContrastSaturationLevels);
        ToolTipService.SetToolTip(bgRemove, ImageViewerTooltips.RemoveSolidBackgroundExtractSubjectCorner);
        ToolTipService.SetToolTip(stamp, ImageViewerTooltips.StampASignatureFromTheLibrary);
        ToolTipService.SetToolTip(meta, ImageViewerTooltips.ImageMetadataExifIptcXmpAnd);
        ToolTipService.SetToolTip(ocrButton, ImageViewerTooltips.RunOfflineOcrOnThisImage);
        ToolTipService.SetToolTip(rotate180, ImageViewerTooltips.Rotate180);
        ToolTipService.SetToolTip(orient, ImageViewerTooltips.ApplyExifOrientationIntoPixels);
        ToolTipService.SetToolTip(straighten, ImageViewerTooltips.DeskewStraightenScannedPageMagick);
        ToolTipService.SetToolTip(batchOrient, ImageViewerTooltips.BatchFolderRotateFlipOrientConvert);
        ToolTipService.SetToolTip(fullscreen, ImageViewerTooltips.ToggleWindowFullscreen);
        ToolTipService.SetToolTip(exportPng, ImageViewerTooltips.ExportAsPng);
        ToolTipService.SetToolTip(exportJpeg, ImageViewerTooltips.ExportAsJpeg);
        ToolTipService.SetToolTip(convert, ImageViewerTooltips.ExportAsWebpTiffBmpGif);
        ToolTipService.SetToolTip(printImage, ImageViewerTooltips.PrintThisImageCtrlP);
        ToolTipService.SetToolTip(copyImage, ImageViewerTooltips.CopyWholeImageToClipboardCtrl);
        ToolTipService.SetToolTip(pasteImage, ImageViewerTooltips.PasteImageFromClipboardCtrlV);
        ToolTipService.SetToolTip(_prevButton, ImageViewerTooltips.PreviousImageInFolder);
        ToolTipService.SetToolTip(_nextButton, ImageViewerTooltips.NextImageInFolder);
        ToolTipService.SetToolTip(_slideshowButton, ImageViewerTooltips.PlayStopFolderSlideshow3sLoops);
        ToolTipService.SetToolTip(_animPlayButton, ImageViewerTooltips.PlayPauseAnimatedFramesGifWebp);
        ToolTipService.SetToolTip(_animPrevButton, ImageViewerTooltips.PreviousAnimationFrame);
        ToolTipService.SetToolTip(_animNextButton, ImageViewerTooltips.NextAnimationFrame);
        ToolTipService.SetToolTip(_animRestartButton, ImageViewerTooltips.RestartAnimationFromFirstFrame);
        ToolTipService.SetToolTip(_animExtractButton, ImageViewerTooltips.SaveCurrentFrameAsPng);
        ToolTipService.SetToolTip(_animLoopBox, ImageViewerTooltips.LoopAnimationPlayback);
        ToolTipService.SetToolTip(_undoButton, ImageViewerTooltips.UndoLastCropResizeRotateAdjust);
        ToolTipService.SetToolTip(zoomOut, ImageViewerTooltips.ZoomOut);
        ToolTipService.SetToolTip(zoomIn, ImageViewerTooltips.ZoomIn);
        ToolTipService.SetToolTip(fit, ImageViewerTooltips.FitImageInView);
        ToolTipService.SetToolTip(actual, ImageViewerTooltips.ZoomTo100);
        ToolTipService.SetToolTip(rotateLeft, ImageViewerTooltips.RotateLeft90);
        ToolTipService.SetToolTip(rotateRight, ImageViewerTooltips.RotateRight90);
        ToolTipService.SetToolTip(flipH, ImageViewerTooltips.FlipHorizontal);
        ToolTipService.SetToolTip(flipV, ImageViewerTooltips.FlipVertical);
        ToolTipService.SetToolTip(save, ImageViewerTooltips.SaveImage);
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
        rotateLeft.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), ImageViewerStatus.RotatedLeft);
        rotateRight.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), ImageViewerStatus.RotatedRight);
        rotate180.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 180), ImageViewerStatus.Rotated180);
        orient.Click += async (_, _) => await MutateAsync(() => _processor.NormalizeOrientationAsync(_document), ImageViewerStatus.OrientationNormalized);
        straighten.Click += async (_, _) => await MutateAsync(
            () => _processor.DeskewAsync(_document, thresholdPercent: 40, crop: true),
            ImageViewerStatus.Straightened);
        batchOrient.Click += async (_, _) => await BatchOrientFolderAsync();
        fullscreen.Click += (_, _) => ToggleFullscreen();
        flipH.Click += async (_, _) => await MutateAsync(() => _processor.FlipHorizontalAsync(_document), ImageViewerStatus.FlippedHorizontally);
        flipV.Click += async (_, _) => await MutateAsync(() => _processor.FlipVerticalAsync(_document), ImageViewerStatus.FlippedVertically);
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
            Text = ImageViewerTextLabels.Images,
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
            _status.Text = ImageViewerStatus.AnimationPlayingAutoplay;
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
            _status.Text = ImageViewerStatus.FolderNavUnavailable;
            return;
        }

        var target = delta < 0
            ? ImageFolderNavigator.Previous(_siblings, _document.Path)
            : ImageFolderNavigator.Next(_siblings, _document.Path);
        if (target is null)
        {
            _status.Text = delta < 0 ? ImageViewerStatus.AlreadyFirst : ImageViewerStatus.AlreadyLast;
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
        var direction = ImageSwipeNavigation.Resolve(dx, dy);
        var step = ImageSwipeNavigation.SiblingStep(direction);
        if (step == 0)
        {
            return;
        }

        await NavigateSiblingAsync(step);
    }

    private void ToggleSlideshow()
    {
        if (_viewState.IsSlideshowActive)
        {
            StopSlideshow();
            _status.Text = SlideshowPolicy.Stopped;
            return;
        }

        if (!SlideshowPolicy.CanStart(_siblings.Count) || _openSibling is null)
        {
            _status.Text = SlideshowPolicy.NeedsMoreImages;
            return;
        }

        StartSlideshow(resume: false);
    }

    private void StartSlideshow(bool resume)
    {
        if (!SlideshowPolicy.CanStart(_siblings.Count) || _openSibling is null)
        {
            _viewState.IsSlideshowActive = false;
            RefreshSlideshowChrome();
            return;
        }

        _viewState.IsSlideshowActive = true;
        StopAnimationPlayback();
        StopSlideshowTimerOnly();
        _slideshowTimer = new DispatcherTimer { Interval = SlideshowPolicy.Interval };
        _slideshowTimer.Tick += async (_, _) => await AdvanceSlideshowAsync();
        _slideshowTimer.Start();
        RefreshSlideshowChrome();
        if (!resume)
        {
            _status.Text = SlideshowPolicy.Started;
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

        _animPlayButton.Content = AnimationFrameNav.PlayButtonLabel(_animationPlaying);
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
            _status.Text = AnimationFrameNav.Paused;
            return;
        }

        StartAnimationPlayback();
        _status.Text = ImageViewerStatus.AnimationPlaying;
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
                ? ImageViewerStatus.AnimationFinishedLooping
                : AnimationFrameNav.Finished(_document.FrameCount);
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
        _status.Text = AnimationFrameNav.Restarted;
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
                ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add("PNG", [".png"]);
            var baseName = string.IsNullOrWhiteSpace(_document.Path)
                ? "frame"
                : System.IO.Path.GetFileNameWithoutExtension(_document.Path);
            picker.SuggestedFileName = AnimationFrameNav.SuggestedFileName(
                baseName,
                _document.CurrentFrameIndex + 1);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = AnimationFrameNav.SaveCancelled;
                return;
            }

            var buffer = await _document.ExtractFrameAsync(_document.CurrentFrameIndex);
            await _encoder.WriteBgraAsync(
                buffer.BgraPixels,
                buffer.Width,
                buffer.Height,
                file.Path,
                ImageEncodeFormat.Png);
            _status.Text = AnimationFrameNav.SavedFrame(
                _document.CurrentFrameIndex + 1,
                file.Name);
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.SaveFrameFailedPrefix, ex.Message);
        }
    }

    private void RefreshSlideshowChrome()
    {
        var active = _viewState.IsSlideshowActive;
        _slideshowButton.Content = SlideshowPolicy.ButtonLabel(active);
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

        if (!SlideshowPolicy.CanStart(_siblings.Count))
        {
            StopSlideshow();
            _status.Text = SlideshowPolicy.StoppedNotEnough;
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
                _status.Text = ImageViewerStatus.DrawModeOff;
                e.Handled = true;
                return;
            }

            if (_selectionMode)
            {
                ExitSelectionMode(keepSelection: false);
                _status.Text = ImagePixelSelectionPolicy.ModeOff;
                e.Handled = true;
                return;
            }

            if (_viewState.IsSlideshowActive)
            {
                StopSlideshow();
                _status.Text = SlideshowPolicy.Stopped;
                e.Handled = true;
                return;
            }

            if (_animationPlaying)
            {
                PauseAnimation();
                _status.Text = AnimationFrameNav.Paused;
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
            _status.Text = ImageViewerStatus.CannotMoveInverted;
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
            ImageViewerStatus.FormatMovedSelection(destX, destY));
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
                _status.Text = ImageViewerStatus.FormatCopiedImage(buffer.Width, buffer.Height);
            }
            finally
            {
                try { System.IO.File.Delete(temp); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.CopyImageFailedPrefix, ex.Message);
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
                        ImageViewerStatus.FormatPastedClipboardImage(destX, destY));
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
                _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.PasteFailedPrefix, ex.Message);
                return;
            }
        }

        await PasteSelectionAsync();
    }

    private async Task CopySelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = ImageSelectionClipboardPolicy.NeedSelection;
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
                _status.Text = ImageSelectionClipboardPolicy.Copied(
                    _selectionInverted,
                    buffer.Width,
                    buffer.Height);
            }
            finally
            {
                try { System.IO.File.Delete(temp); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _status.Text = ImageSelectionClipboardPolicy.CopyFailed(ex.Message);
        }
    }

    private async Task CutSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = ImageSelectionClipboardPolicy.NeedSelection;
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
            ImageSelectionClipboardPolicy.Cut(_selectionInverted, sel.Width, sel.Height));
    }

    private async Task PasteSelectionAsync()
    {
        if (_selectionClipboard is null)
        {
            _status.Text = ImageSelectionClipboardPolicy.EmptyClipboard;
            return;
        }

        var destX = _pixelSelection?.X ?? 0;
        var destY = _pixelSelection?.Y ?? 0;
        var clip = _selectionClipboard;
        await MutateAsync(
            () => _processor.PasteRectAsync(_document, clip, destX, destY),
            ImageViewerStatus.FormatPastedRect(clip.Width, clip.Height, destX, destY));
    }

    private async Task DeleteSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = ImageSelectionClipboardPolicy.NeedSelection;
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
                ? ImageViewerStatus.FormatClearedOutsideSelection(sel.Width, sel.Height)
                : ImageViewerStatus.FormatClearedSelection(sel.Width, sel.Height));
    }

    private async Task CropToSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = ImageSelectionClipboardPolicy.NeedSelection;
            return;
        }

        if (_selectionInverted)
        {
            _status.Text = ImageCropSelectionPolicy.CannotCropInverted;
            return;
        }

        ExitSelectionMode(keepSelection: false);
        await MutateAsync(
            () => _processor.CropAsync(_document, sel),
            ImageCropSelectionPolicy.CroppedToSelection(sel.Width, sel.Height));
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
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.EditFailedPrefix, ex.Message);
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
            _status.Text = ImageEditUndoPolicy.NothingToUndo;
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
            _status.Text = ImageEditUndoPolicy.Undone;
        }
        catch (Exception ex)
        {
            _status.Text = ImageEditUndoPolicy.Failed(ex.Message);
        }
    }

    private void ToggleFullscreen()
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
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.Visibility = _toolbar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        _status.Text = _toolbar.Visibility == Visibility.Visible ? ImageViewerStatus.ToolbarShown : ImageViewerStatus.ToolbarHidden;
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

            _status.Text = ImageViewerStatus.FormatLoadingPreview(nativeMax);
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
            var name = ImageDescriptiveMetadataSummary.AutomationNameFromDescription(meta)
                ?? (!string.IsNullOrWhiteSpace(meta.Title)
                    ? meta.Title!
                    : !string.IsNullOrWhiteSpace(_document.Path)
                        ? System.IO.Path.GetFileName(_document.Path)
                        : "Image");
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
        if (!WheelInputPolicy.PreferZoomOverScroll(
                e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control)))
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
            _status.Text = ImageCropSelectionPolicy.NeedIntegerBox;
            return;
        }

        await MutateAsync(
            () => _processor.CropAsync(_document, rect),
            ImageCropSelectionPolicy.CroppedTo(rect.Width, rect.Height));
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
        _status.Text = ImageCropSelectionPolicy.InteractiveHint;
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
            _status.Text = ImagePixelSelectionPolicy.ModeOff;
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
        _status.Text = ImagePixelSelectionPolicy.ModeOn;
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

        SetPixelSelection(ImagePixelSelectionPolicy.FullImageRect(_document.PixelWidth, _document.PixelHeight));
        ApplySelectionChrome();
        _status.Text = ImagePixelSelectionPolicy.SelectedAll(_document.PixelWidth, _document.PixelHeight);
    }

    private void ImageSurface_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement target)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var copyItem = new MenuFlyoutItem
        {
            Text = _pixelSelection is not null
                ? ImageSelectionClipboardPolicy.MenuCopySelection
                : ImageSelectionClipboardPolicy.MenuCopyImage,
        };
        copyItem.Click += async (_, _) => await CopyImageAsync();
        flyout.Items.Add(copyItem);

        if (_pixelSelection is not null)
        {
            var cutItem = new MenuFlyoutItem { Text = ImageSelectionClipboardPolicy.MenuCutSelection };
            cutItem.Click += async (_, _) => await CutSelectionAsync();
            flyout.Items.Add(cutItem);
            var deselectItem = new MenuFlyoutItem { Text = ImagePixelSelectionPolicy.MenuDeselect };
            deselectItem.Click += (_, _) => ClearPixelSelection();
            flyout.Items.Add(deselectItem);
        }
        else
        {
            var selectAllItem = new MenuFlyoutItem { Text = ImagePixelSelectionPolicy.MenuSelectAll };
            selectAllItem.Click += (_, _) => SelectAllPixels();
            flyout.Items.Add(selectAllItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var pasteItem = new MenuFlyoutItem { Text = ImageViewerTextLabels.Paste };
        pasteItem.Click += async (_, _) => await PasteImageAsync();
        flyout.Items.Add(pasteItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var fitItem = new MenuFlyoutItem { Text = ImageViewerTextLabels.Fit };
        fitItem.Click += async (_, _) => await FitAsync();
        flyout.Items.Add(fitItem);
        var rotateLeftItem = new MenuFlyoutItem { Text = ImageViewerTextLabels.RotateLeft };
        rotateLeftItem.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), ImageViewerStatus.RotatedLeft);
        flyout.Items.Add(rotateLeftItem);
        var rotateRightItem = new MenuFlyoutItem { Text = ImageViewerTextLabels.RotateRight };
        rotateRightItem.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), ImageViewerStatus.RotatedRight);
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
            _status.Text = ImageSelectionClipboardPolicy.NeedSelection;
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
            _status.Text = ImageViewerStatus.InvertedFullEmpty;
            return;
        }

        _selectionInverted = !_selectionInverted;
        ApplySelectionChrome();
        _status.Text = _selectionInverted
            ? ImageViewerStatus.FormatSelectionInverted(sel.Width, sel.Height)
            : ImageViewerStatus.FormatSelectionRestored(SelectionKindLabel(), sel.Width, sel.Height);
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
            _status.Text = ImagePixelSelectionPolicy.Cleared;
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
                _status.Text = ImageViewerStatus.CannotMoveInverted;
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
            _status.Text = ImageViewerStatus.MovingSelection;
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
                    _status.Text = ImageViewerStatus.FormatSelected(sel.Width, sel.Height);
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
                ImageViewerStatus.FormatSelectedPixels(_pixelSelection.Value.Width, _pixelSelection.Value.Height, SelectionKindLabel());
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
            var lum = ImageSmartLassoEdges.LuminanceFromBgra(pixels.BgraPixels, w, h);
            _smartEdgeMap = ImageSmartLassoEdges.ComputeSobel(lum);
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
        var (bestX, bestY) = ImageSmartLassoEdges.FindStrongestEdge(
            _smartEdgeMap,
            cx,
            cy,
            ImageSmartLassoEdges.DefaultSnapRadius);

        if (_smartEdgeMap[bestY, bestX] < 12f)
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
        _status.Text = ImageViewerStatus.FormatLassoSelected(_pixelSelection.Value.Width, _pixelSelection.Value.Height, _lassoDocPoints.Count);
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
            ImageViewerStatus.FormatMovedSelection(destX, destY));
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
                _status.Text = ImageViewerStatus.FormatSelectionMapped(SelectionKindLabel(), mapped.Width, mapped.Height);
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
            _status.Text = ImageCropSelectionPolicy.SelectionPreview(mapped.Width, mapped.Height);
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
            _status.Text = ImageCropSelectionPolicy.NeedRegion;
            return;
        }

        if (_displayWidth <= 0 || _displayHeight <= 0)
        {
            _status.Text = ImageCropSelectionPolicy.ImageNotReady;
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
            ImageCropSelectionPolicy.CroppedTo(mapped.Width, mapped.Height));
    }

    private async Task ResizeAsync()
    {
        var srcW = _document.PixelWidth;
        var srcH = _document.PixelHeight;
        if (srcW <= 0 || srcH <= 0)
        {
            _status.Text = ImageViewerStatus.NothingToResize;
            return;
        }

        var meta = await _document.GetMetadataAsync();
        var currentDpi = meta.DpiX is > 0 ? meta.DpiX.Value : (meta.DpiY is > 0 ? meta.DpiY.Value : 96.0);
        var aspect = (double)srcW / srcH;
        var updating = false;
        var unitBox = new ComboBox
        {
            Header = ImageDialogHeaders.Units,
            Width = 140,
            ItemsSource = ImageDialogOptions.SizeUnits.ToList(),
            SelectedIndex = 0,
        };
        var dpiBox = new NumberBox
        {
            Header = ImageDialogHeaders.DpiPpi,
            Value = currentDpi,
            Minimum = 1,
            Maximum = 1200,
            SmallChange = 1,
            LargeChange = 10,
            Width = 140,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        var widthBox = new TextBox { Text = srcW.ToString(), Width = 96, Header = ImageDialogHeaders.Width };
        var heightBox = new TextBox { Text = srcH.ToString(), Width = 96, Header = ImageDialogHeaders.Height };
        var percentBox = new TextBox { Text = "100", Width = 96, Header = ImageDialogHeaders.Scale2 };
        var lockAspect = new CheckBox { Content = ImageViewerChromeLabels.LockAspectRatio, IsChecked = true };
        var filterBox = new ComboBox
        {
            Header = ImageDialogHeaders.Resampling,
            Width = 180,
            ItemsSource = ImageDialogOptions.ResamplingModes.ToList(),
            SelectedIndex = ResolveDefaultInterpolationIndex(),
        };
        var preview = new TextBlock
        {
            Text = ImageResizeDialogMath.FormatResultEstimate(srcW, srcH),
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
                preview.Text = ImageViewerTextLabels.ResultPlaceholder;
                return;
            }

            preview.Text = ImageResizeDialogMath.FormatResultWithDpi(w, h, ActiveDpi());
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
                h = ImageResizeDialogMath.HeightForWidth(w, aspect);
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
                w = ImageResizeDialogMath.WidthForHeight(h, aspect);
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

            var (w, h) = ImageResizeDialogMath.ScaleByPercent(srcW, srcH, pct);
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
            Content = ImageResizeDialogMath.FormatAlsoResizeFolder(_siblings.Count),
            IsChecked = false,
            IsEnabled = _siblings.Count > 1 && _decoder is not null,
        };
        ToolTipService.SetToolTip(
            batchFolder, ImageViewerTooltips.AppliesScaleToEveryImageIn);

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = ImageResizeDialogMath.FormatCurrent(srcW, srcH, currentDpi) },
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
            Title = ImageDialogTitles.ResizeImage,
            Content = panel,
            PrimaryButtonText = DialogButtons.Resize,
            CloseButtonText = DialogButtons.Cancel,
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
            _status.Text = ImageViewerStatus.ResizeNeedsPositive;
            return;
        }

        var filter = ImageResizeDialogMath.FilterFromComboIndex(filterBox.SelectedIndex);
        var options = new ImageResizeOptions(Filter: filter, DensityDpi: ActiveDpi());
        await MutateAsync(
            () => _processor.ResizeAsync(_document, width, height, options),
            ImageViewerStatus.FormatResized(width, height, ActiveDpi()));

        if (batchFolder.IsChecked == true && _decoder is not null && _siblings.Count > 1)
        {
            if (!double.TryParse(percentBox.Text, out var pct) || pct <= 0)
            {
                _status.Text = ImageViewerStatus.BatchResizeNeedsScale;
                return;
            }

            var batchCount = await BatchResizeFolderAsync(
                pct,
                lockAspect.IsChecked == true,
                options);
            _status.Text =
                ImageViewerStatus.FormatResizedCurrentWithBatch(width, height, batchCount, pct);
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
            _status.Text = BatchProgressUi.CancelledStatus(BatchProgressUi.BatchResize, updated);
        }

        return updated;
    }

    private async Task BatchOrientFolderAsync()
    {
        if (_decoder is null || _siblings.Count < 2 || string.IsNullOrWhiteSpace(_document.Path))
        {
            _status.Text = ImageViewerStatus.BatchNeedsFolder;
            return;
        }

        var categoryBox = new ComboBox
        {
            Header = ImageDialogHeaders.Category,
            Width = 260,
            ItemsSource = ImageDialogOptions.BatchCategories.ToList(),
            SelectedIndex = 0,
        };
        var opBox = new ComboBox
        {
            Header = ImageDialogHeaders.Operation,
            Width = 260,
            ItemsSource = ImageDialogOptions.BatchOrientations.ToList(),
            SelectedIndex = 1,
        };
        var formatBox = new ComboBox
        {
            Header = ImageDialogHeaders.ExportFormat,
            Width = 260,
            Visibility = Visibility.Collapsed,
            ItemsSource = ImageDialogOptions.ExportFormats.ToList(),
            SelectedIndex = 0,
        };
        var quality = new Slider
        {
            Header = ImageDialogHeaders.QualityJpegWebpAvif,
            Minimum = 1,
            Maximum = 100,
            Value = 85,
            StepFrequency = 1,
            Width = 260,
            Visibility = Visibility.Collapsed,
        };
        var renamePattern = new TextBox
        {
            Header = ImageDialogHeaders.RenamePatternN1BasedIndexNameBase,
            Text = BatchRenamePattern.DefaultPattern,
            Width = 260,
            Visibility = Visibility.Collapsed,
        };
        var profileBox = new ComboBox
        {
            Header = ImageDialogHeaders.ColorProfile,
            Width = 260,
            Visibility = Visibility.Collapsed,
            ItemsSource = ImageDialogOptions.ColorProfileOps.ToList(),
            SelectedIndex = 1,
        };
        var includeCurrent = new CheckBox
        {
            Content = ImageViewerChromeLabels.AlsoApplyToOpenImage,
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
            Title = ImageDialogTitles.BatchFolderImages,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = BatchProgressUi.FormatAppliesToFolder(_siblings.Count),
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
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
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
            _status.Text = BatchProgressUi.FormatConvertWrote(format, converted);
            return;
        }

        if (categoryBox.SelectedIndex == 2)
        {
            var strippedCount = await BatchStripMetadataFolderAsync(includeCurrent: includeCurrent.IsChecked == true);
            _status.Text = BatchProgressUi.FormatStripMetadata(strippedCount);
            return;
        }

        if (categoryBox.SelectedIndex == 3)
        {
            var renamed = await BatchRenameFolderAsync(renamePattern.Text ?? BatchRenamePattern.DefaultPattern);
            _status.Text = BatchProgressUi.FormatRenamed(renamed);
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
                    convert ? ImageViewerStatus.FormatConvertedCurrent(kind.ToString()) : ImageViewerStatus.FormatAssignedProfile(kind.ToString()));
            }

            var profiled = await BatchColorProfileFolderAsync(kind, convert, includeCurrent.IsChecked == true);
            _status.Text = ImageBatchColorProfilePolicy.UpdatedStatus(profiled)
                + (includeCurrent.IsChecked == true ? ImageViewerStatus.WithCurrentSuffix : ".");
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
            await MutateAsync(() => ApplyAsync(_document), ImageViewerStatus.FormatCurrentImage(label));
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
            ? ImageViewerStatus.FormatBatchCancelled(label, updated)
            : ImageViewerStatus.FormatBatchUpdated(label, updated, includeCurrent.IsChecked == true);
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
            Text = BatchProgressUi.ProgressLabel(0, targets.Count),
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
                        Text = BatchProgressUi.CancelHint,
                        Opacity = 0.7,
                        FontSize = 12,
                    },
                },
            },
            CloseButtonText = BatchProgressUi.CloseButton,
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
                progressLabel.Text = BatchProgressUi.ProgressWithFile(
                    i + 1,
                    targets.Count,
                    name);
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
            _status.Text = BatchProgressUi.CancelledStatus(BatchProgressUi.BatchConvert, written);
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
            _status.Text = BatchProgressUi.CancelledStatus(BatchProgressUi.BatchStrip, updated);
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
                var stem = BatchRenamePattern.Expand(pattern, baseName, n);
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
            _status.Text = BatchProgressUi.CancelledStatus(BatchProgressUi.BatchRename, renamed);
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
            ImageBatchColorProfilePolicy.BatchTitle,
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
            _status.Text = ImageBatchColorProfilePolicy.CancelledStatus(updated);
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
        var fuzzLabel = new TextBlock { Text = ImageViewerTextLabels.Fuzz12Percent };
        fuzzSlider.ValueChanged += (_, args) =>
        {
            fuzzLabel.Text = ImageResizeDialogMath.FormatFuzzPercent(args.NewValue);
        };
        var trimBox = new CheckBox
        {
            Content = ImageViewerChromeLabels.TrimToOpaqueBounds,
            IsChecked = true,
        };
        var actionBox = new ComboBox
        {
            Width = 280,
            ItemsSource = new[]
            {
                ImageBackgroundSubjectPolicy.RemoveInPlace,
                ImageBackgroundSubjectPolicy.ExtractToClipboard,
                ImageBackgroundSubjectPolicy.ExtractToFile,
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
                    Text = ImageViewerTextLabels.FloodFillHint,
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
            Title = ImageBackgroundSubjectPolicy.DialogTitle,
            Content = panel,
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
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
                        ? ImageViewerStatus.FormatBackgroundRemoved(fuzz, trimmed: true)
                        : ImageViewerStatus.FormatBackgroundRemoved(fuzz, trimmed: false));
                var format = _document.FormatName;
                var ext = string.IsNullOrWhiteSpace(_document.Path)
                    ? format
                    : System.IO.Path.GetExtension(_document.Path);
                if (!ImageBackgroundSubjectPolicy.FormatSupportsAlpha(ext)
                    && !ImageBackgroundSubjectPolicy.FormatSupportsAlpha(format))
                {
                    _status.Text += ImageBackgroundSubjectPolicy.TransparencyHint;
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
                        _status.Text = ImageViewerStatus.FormatSubjectCopied(buffer.Width, buffer.Height);
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
                        ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
                    var picker = new Windows.Storage.Pickers.FileSavePicker();
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                    picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                    picker.FileTypeChoices.Add("PNG", [".png"]);
                    var baseName = string.IsNullOrWhiteSpace(_document.Path)
                        ? "subject"
                        : System.IO.Path.GetFileNameWithoutExtension(_document.Path);
                    picker.SuggestedFileName = ImageBackgroundSubjectPolicy.SuggestedSubjectFileName(baseName);
                    var file = await picker.PickSaveFileAsync();
                    if (file is null)
                    {
                        _status.Text = ImageBackgroundSubjectPolicy.SaveCancelled;
                    }
                    else
                    {
                        await _encoder.WriteBgraAsync(
                            buffer.BgraPixels,
                            buffer.Width,
                            buffer.Height,
                            file.Path,
                            ImageEncodeFormat.Png);
                        _status.Text = ImageViewerStatus.FormatSubjectSaved(file.Name);
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
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.BackgroundToolsFailedPrefix, ex.Message);
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
                Content = ImageViewerChromeLabels.RotateCounter,
                Width = 36,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 0, 0),
            };
            ToolTipService.SetToolTip(resetOne, ImageViewerTooltips.ResetThisAdjustment);
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

        var brightness = MakeSlider(ImageDialogHeaders.Brightness, -100, 100, 0);
        var contrast = MakeSlider(ImageDialogHeaders.Contrast, -100, 100, 0);
        var saturation = MakeSlider(ImageDialogHeaders.Saturation, -100, 100, 0);
        var highlights = MakeSlider(ImageDialogHeaders.Highlights, -100, 100, 0);
        var shadows = MakeSlider(ImageDialogHeaders.Shadows, -100, 100, 0);
        var blackPoint = MakeSlider(ImageDialogHeaders.BlackPoint, 0, 100, 0);
        var whitePoint = MakeSlider("White point (0…100)", 0, 100, 100);
        var gamma = MakeSlider("Gamma (0.1…3.0)", 0.1, 3.0, 1.0, step: 0.05);
        var temperature = MakeSlider("Temperature (−100 cold…100 warm)", -100, 100, 0);
        var tint = MakeSlider("Tint (−100 green…100 magenta)", -100, 100, 0);
        var sharpness = MakeSlider("Sharpness (0…100)", 0, 100, 0);
        var autoLevels = new CheckBox { Content = ImageViewerChromeLabels.AutoLevels, IsChecked = false };
        var sepia = new CheckBox { Content = ImageViewerChromeLabels.Sepia, IsChecked = false };
        var histCanvas = new Canvas
        {
            Width = 280,
            Height = 64,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)),
        };
        var histLabel = new TextBlock { Text = ImageViewerTextLabels.LuminanceHistogram, Opacity = 0.75, FontSize = 12 };

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
                _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.PreviewFailedPrefix, ex.Message);
            }
            finally
            {
                previewBusy = false;
            }
        }

        void RequestPreview() => _ = PreviewAsync();

        var reset = new Button { Content = ImageViewerChromeLabels.ResetAll, HorizontalAlignment = HorizontalAlignment.Left };
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
                    Text = ImageViewerTextLabels.AdjustPreviewHint,
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
            Title = ImageDialogTitles.ColorAdjustments,
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
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
                if (ImageAdjustLivePreviewPolicy.ShouldRestoreBaselineOnCancel(baseline is not null)
                    && baseline is not null)
                {
                    _document.RestoreCheckpoint(baseline);
                    baseline = null;
                    await RefreshAsync();
                }

                _status.Text = ImageViewerStatus.AdjustmentsCancelled;
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

                _status.Text = ImageViewerStatus.NoAdjustments;
                return;
            }

            // Preview already matches final values; keep it and record undo from the pre-dialog state.
            PushUndo(baseline!);
            baseline = null;
            UpdateStatus();
            _status.Text = ImageViewerStatus.AdjustmentsApplied;
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

            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.AdjustFailedPrefix, ex.Message);
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
            _status.Text = ImageViewerStatus.SignatureLibraryUnavailable;
            return;
        }

        try
        {
            var entries = await _signatures.ListAsync();
            if (entries.Count == 0)
            {
                _status.Text = ImageViewerStatus.NoSignaturesSaved;
                return;
            }

            var list = new ListView
            {
                ItemsSource = entries.Select(SignatureDisplayText.ListLabel).ToList(),
                SelectionMode = ListViewSelectionMode.Single,
                SelectedIndex = 0,
                MaxHeight = 240,
                Width = 320,
            };
            AutomationProperties.SetName(list, SignatureLibraryUi.LibraryTitle);
            var dialog = new ContentDialog
            {
                Title = ImageDialogTitles.StampSignature,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = SignatureLibraryUi.ImageMarkupUsesPasteFile
                                ? BatchProgressUi.StampPlacementHintWithUndo
                                : BatchProgressUi.StampPlacementHint,
                            Opacity = 0.75,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        list,
                    },
                },
                PrimaryButtonText = DialogButtons.Stamp,
                CloseButtonText = DialogButtons.Cancel,
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
                    ImageViewerStatus.FormatStamped(entry.Name, destX, destY));
            }
            finally
            {
                try { System.IO.File.Delete(temp); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.StampFailedPrefix, ex.Message);
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
                Header = ImageDialogHeaders.Scale,
                Width = 240,
                ItemsSource = ImageDialogOptions.PrintScales.ToList(),
                SelectedIndex = 0,
            };
            var nUpBox = new ComboBox
            {
                Header = ImageDialogHeaders.PagesPerSheet,
                Width = 240,
                ItemsSource = ImageDialogOptions.PagesPerSheet.ToList(),
                SelectedIndex = 0,
            };
            var grayscale = new CheckBox { Content = ImageViewerChromeLabels.Grayscale };
            var center = new CheckBox { Content = ImageViewerChromeLabels.CenterOnPage, IsChecked = true };
            var includeSiblings = new CheckBox
            {
                Content = $"Also print other folder images ({Math.Max(0, _siblings.Count - 1)})",
                IsEnabled = _siblings.Count > 1 && _decoder is not null,
            };
            var dialog = new ContentDialog
            {
                Title = ImageDialogTitles.PrintImage,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = ImageViewerTextLabels.PrintDialogHint,
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
                        _status.Text = ImageViewerStatus.FormatPrintSkipped(System.IO.Path.GetFileName(sibling), ex.Message);
                    }
                }
            }

            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException(MainWindowRequiredMessages.Print);
            using var helper = new DocumentPrintHelper(
                window,
                jobName: System.IO.Path.GetFileName(_document.Path) ?? "Glyph image",
                scaleMode: scaleMode,
                center: center.IsChecked == true,
                autoRotate: true,
                pagesPerSheet: pagesPerSheet);
            await helper.PrintAsync(bitmaps);
            _status.Text = ImageViewerStatus.FormatPrintUiShown(bitmaps.Count, pagesPerSheet);
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.PrintFailedPrefix, ex.Message);
        }
    }

    private async Task ConvertAsync()
    {
        var formatBox = new ComboBox
        {
            Header = ImageDialogHeaders.Format,
            Width = 200,
            ItemsSource = ImageDialogOptions.ConvertFormatsExtra.ToList(),
            SelectedIndex = 0,
        };
        var quality = new Slider
        {
            Header = ImageDialogHeaders.Quality1100,
            Minimum = 1,
            Maximum = 100,
            Value = 85,
            StepFrequency = 1,
            Width = 240,
        };
        var lossless = new CheckBox { Content = DocumentExportFormats.WebpLosslessLabel, IsChecked = false };
        var preserveAlpha = new CheckBox { Content = ImageViewerChromeLabels.PreserveAlpha, IsChecked = true };
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
            Content = ImageViewerChromeLabels.PreserveMetadata,
            IsChecked = !stripByDefault,
        };
        var embedSrgb = new CheckBox { Content = ImageEncodeEmbedSrgb.CheckboxLabel, IsChecked = false };
        var tiffCompression = new ComboBox
        {
            Header = ImageDialogHeaders.TiffCompression,
            Width = 200,
            Visibility = Visibility.Collapsed,
            ItemsSource = ImageDialogOptions.TiffCompressions.ToList(),
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
            Title = ImageDialogTitles.ConvertImage,
            Content = panel,
            PrimaryButtonText = DialogButtons.ExportEllipsis,
            CloseButtonText = DialogButtons.Cancel,
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
            Header = ImageDialogHeaders.JpegQuality1100,
            Minimum = 1,
            Maximum = 100,
            Value = 90,
            StepFrequency = 1,
            Width = 240,
        };
        var dialog = new ContentDialog
        {
            Title = ImageDialogTitles.ExportJpeg,
            Content = quality,
            PrimaryButtonText = DialogButtons.ExportEllipsis,
            CloseButtonText = DialogButtons.Cancel,
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
            var assignSrgb = new Button { Content = ImageViewerChromeLabels.AssignSrgb };
            assignSrgb.Click += async (_, _) =>
            {
                await MutateAsync(
                    () => _processor.AssignColorProfileAsync(_document, ImageColorProfileKind.Srgb),
                    ImageViewerStatus.AssignedSrgbIcc);
            };
            var convertSrgb = new Button { Content = ImageViewerChromeLabels.ConvertToSrgb };
            convertSrgb.Click += async (_, _) =>
            {
                await MutateAsync(
                    () => _processor.ConvertColorProfileAsync(_document, ImageColorProfileKind.Srgb),
                    ImageViewerStatus.ConvertedPixelsToSrgb);
            };
            actions.Children.Add(assignSrgb);
            actions.Children.Add(convertSrgb);

            var colorManaged = new CheckBox
            {
                Content = ImageColorManagedDisplayPolicy.ColorManagedLabel,
                IsChecked = _document.ColorManagedDisplay,
            };
            colorManaged.Checked += async (_, _) =>
            {
                _document.ColorManagedDisplay = true;
                await RefreshAsync();
                _status.Text = ImageColorManagedDisplayPolicy.ColorManagedOn;
            };
            colorManaged.Unchecked += async (_, _) =>
            {
                _document.ColorManagedDisplay = false;
                await RefreshAsync();
                _status.Text = ImageColorManagedDisplayPolicy.ColorManagedOff;
            };
            var softProof = new CheckBox
            {
                Content = ImageColorManagedDisplayPolicy.SoftProofLabel,
                IsChecked = _document.SoftProofProfile == ImageColorProfileKind.AdobeRgb,
            };
            softProof.Checked += async (_, _) =>
            {
                _document.SoftProofProfile = ImageColorProfileKind.AdobeRgb;
                _document.ColorManagedDisplay = true;
                colorManaged.IsChecked = true;
                await RefreshAsync();
                _status.Text = ImageColorManagedDisplayPolicy.SoftProofOn;
            };
            softProof.Unchecked += async (_, _) =>
            {
                _document.SoftProofProfile = null;
                await RefreshAsync();
                _status.Text = ImageColorManagedDisplayPolicy.SoftProofOff;
            };
            var intentBox = new ComboBox
            {
                Width = 160,
                ItemsSource = ImageRenderingIntentUi.Labels.ToList(),
                SelectedIndex = (int)_document.DisplayRenderingIntent,
            };
            intentBox.SelectionChanged += async (_, _) =>
            {
                if (intentBox.SelectedIndex < 0)
                {
                    return;
                }

                _document.DisplayRenderingIntent = ImageRenderingIntentUi.FromComboIndex(intentBox.SelectedIndex);
                await RefreshAsync();
                _status.Text = ImageRenderingIntentUi.StatusAfterChange(_document.DisplayRenderingIntent);
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
                            new TextBlock { Text = ImageViewerTextLabels.Intent, VerticalAlignment = VerticalAlignment.Center },
                            intentBox,
                        },
                    },
                },
            };
            if (info.GpsLatitude is double lat && info.GpsLongitude is double lon)
            {
                var coords = ImageGpsActions.FormatCoords(lat, lon);
                var copy = new Button { Content = ImageGpsActions.CopyButton };
                copy.Click += async (_, _) =>
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(coords);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    _status.Text = ImageGpsActions.CopiedStatus(coords);
                };
                var maps = new Button { Content = ImageGpsActions.OpenMapButton };
                maps.Click += async (_, _) =>
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(ImageGpsActions.OpenStreetMapUri(lat, lon)));
                };
                var strip = new Button { Content = ImageGpsActions.RemoveButton };
                strip.Click += async (_, _) =>
                {
                    await MutateAsync(
                        () => _processor.RemoveGpsMetadataAsync(_document),
                        ImageGpsActions.RemovedStatus);
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
                Title = ImageDialogTitles.ImageMetadata,
                Content = panel,
                PrimaryButtonText = DialogButtons.EditEllipsis,
                CloseButtonText = DialogButtons.Close,
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
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.MetadataFailedPrefix, ex.Message);
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
                _status.Text = DocumentSaveStatus.SavedFileName(System.IO.Path.GetFileName(_document.Path));
                App.CurrentApp.MainWindowInstance?.NotifyActiveDocumentSaved(_document.Path!);
                return;
            }

            var (format, extension) = GuessSaveFormat(_document.FormatName);
            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
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
                _status.Text = DocumentSaveStatus.Cancelled;
                return;
            }

            await _encoder.SaveAsAsync(_document, file.Path, format);
            ClearUnsavedEdits();
            _status.Text = DocumentSaveStatus.SavedFileName(file.Name);
            App.CurrentApp.MainWindowInstance?.NotifyActiveDocumentSaved(file.Path);
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.SaveFailedPrefix, ex.Message);
        }
    }

    private static (ImageEncodeFormat Format, string Extension) GuessSaveFormat(string formatName) =>
        ImageEncodeFormatResolver.FromFormatName(formatName);

    private async Task EditDescriptiveMetadataAsync(ImageMetadataInfo current)
    {
        var title = new TextBox { Header = ImageDialogHeaders.Title, Text = current.Title ?? string.Empty, Width = 360 };
        var description = new TextBox
        {
            Header = ImageDialogHeaders.DescriptionCaption,
            Text = current.Description ?? string.Empty,
            Width = 360,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
        };
        var keywords = new TextBox
        {
            Header = ImageDialogHeaders.KeywordsCommaSeparated,
            Text = current.Keywords ?? string.Empty,
            Width = 360,
        };
        var copyright = new TextBox { Header = ImageDialogHeaders.Copyright, Text = current.Copyright ?? string.Empty, Width = 360 };
        var editDialog = new ContentDialog
        {
            Title = ImageDialogTitles.EditDescriptiveMetadata,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = ImageViewerTextLabels.IptcPersistHint,
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
            PrimaryButtonText = DialogButtons.Apply,
            CloseButtonText = DialogButtons.Cancel,
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
            ImageViewerStatus.DescriptiveMetadataUpdated);
    }

    private async Task RunOcrAsync()
    {
        if (_ocr is null)
        {
            _status.Text = OcrResultDialog.EngineUnavailable;
            return;
        }

        var ocrFolder = false;
        if (ImageOcrFolderChooser.ShouldOfferFolder(_siblings.Count) && _decoder is not null)
        {
            var chooser = new ContentDialog
            {
                Title = ImageOcrFolderChooser.Title,
                Content = ImageOcrFolderChooser.Prompt(_siblings.Count),
                PrimaryButtonText = ImageOcrFolderChooser.PrimaryButton,
                SecondaryButtonText = ImageOcrFolderChooser.SecondaryButton(_siblings.Count),
                CloseButtonText = DialogButtons.Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            var choice = await chooser.ShowAsync();
            if (choice == ContentDialogResult.None)
            {
                _status.Text = ImageOcrFolderChooser.Cancelled;
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
            _status.Text = ImageViewerStatus.RunningOcr;
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
            var copy = new Button { Content = ImageViewerChromeLabels.CopyText, Margin = new Thickness(0, 8, 0, 0) };
            copy.Click += (_, _) =>
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(result.Text ?? string.Empty);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                _status.Text = OcrResultDialog.TextCopied;
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
                Title = ImageDialogTitles.OcrResult,
                Content = panel,
                CloseButtonText = DialogButtons.Close,
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
            _status.Text = string.IsNullOrWhiteSpace(result.Text)
                ? ImageViewerStatus.OcrFinishedNoText
                : ImageViewerStatus.FormatOcrFinished(result.Lines.Count);
        }
        catch (Exception ex)
        {
            _status.Text = OcrResultDialog.Failed(ex.Message);
        }
    }

    private async Task RunFolderOcrAsync()
    {
        if (_ocr is null || _decoder is null || _siblings.Count == 0)
        {
            _status.Text = ImageViewerStatus.FolderOcrUnavailable;
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
            _status.Text = cancelled ? ImageViewerStatus.FolderOcrCancelled : ImageViewerStatus.FolderOcrNoResults;
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
        var copy = new Button { Content = ImageViewerChromeLabels.CopyAll, Margin = new Thickness(0, 8, 0, 0) };
        copy.Click += (_, _) =>
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(combined);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            _status.Text = ImageViewerStatus.FolderOcrCopied;
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
            Title = ImageDialogTitles.FolderOcrResults,
            Content = panel,
            CloseButtonText = DialogButtons.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
        _status.Text = cancelled
            ? ImageViewerStatus.FormatFolderOcrCancelledAfter(updated)
            : ImageViewerStatus.FormatFolderOcrFinished(updated);
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
            _status.Text = DocumentSaveStatus.SavedFileName(System.IO.Path.GetFileName(_document.Path));
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.SaveFailedPrefix, ex.Message);
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
                ?? throw new InvalidOperationException(MainWindowRequiredMessages.SavePicker);
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add(format.ToString(), [extension]);
            picker.SuggestedFileName = "image" + extension;
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = DocumentExportFormats.CancelledStatus;
                return;
            }

            await _encoder.SaveAsAsync(_document, file.Path, format, options);
            _status.Text = ImageViewerStatus.FormatExported(file.Name);
        }
        catch (Exception ex)
        {
            _status.Text = ImageViewerStatus.FormatFailed(ImageViewerStatus.ExportFailedPrefix, ex.Message);
        }
    }

    private async Task<bool> EnsureMarkupFlattenedAsync()
    {
        if (!ImageMarkupFlattenPolicy.HasPendingMarkup(_markupStrokes.Count, _markupShapes.Count))
        {
            return true;
        }

        if (!NonDestructiveEditPolicy.ShouldPromptFlatten(
                hasPendingMarkup: true,
                targetIsRaster: true))
        {
            return true;
        }

        var total = _markupStrokes.Count + _markupShapes.Count;
        var dialog = new ContentDialog
        {
            Title = ImageMarkupFlattenPolicy.DialogTitle,
            Content = $"{total} markup item(s) will be baked into pixels before saving.",
            PrimaryButtonText = ImageMarkupFlattenPolicy.PrimaryButton,
            CloseButtonText = DialogButtons.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            _status.Text = ImageViewerStatus.SaveCancelledMarkup;
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
            _status.Text = ImageViewerStatus.DrawModeOff;
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
            Header = ImageDialogHeaders.Tool,
            Width = 200,
            ItemsSource = ImageDialogOptions.MarkupTools.ToList(),
            SelectedIndex = 0,
        };
        var widthSlider = new Slider
        {
            Header = ImageDialogHeaders.StrokeWidthPx,
            Minimum = 1,
            Maximum = 32,
            Value = _drawWidthPixels,
            StepFrequency = 1,
            Width = 240,
        };
        var colorBox = new ComboBox
        {
            Header = ImageDialogHeaders.Color,
            Width = 200,
            ItemsSource = ImageDialogOptions.MarkupColors.ToList(),
            SelectedIndex = 0,
        };
        var panel = new StackPanel { Spacing = 8, Children = { toolBox, colorBox, widthSlider } };
        var dialog = new ContentDialog
        {
            Title = ImageDialogTitles.DrawMarkup,
            Content = panel,
            PrimaryButtonText = DialogButtons.StartDrawing,
            CloseButtonText = DialogButtons.Cancel,
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
                Header = isCallout ? ImageDialogHeaders.CalloutText : ImageDialogHeaders.Text,
                Text = isCallout ? "Note" : "Label",
                Width = 280,
            };
            var fontSlider = new Slider
            {
                Header = ImageDialogHeaders.FontSizePx,
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
                CloseButtonText = DialogButtons.Cancel,
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
                ? ImageViewerStatus.CalloutMarkupPrompt
                : ImageViewerStatus.TextMarkupPrompt;
        }
        else
        {
            _pendingText = null;
            var toolName = toolBox.SelectedItem as string ?? "Freehand";
            _status.Text = ImageViewerStatus.FormatMarkupTool(toolName);
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
        var pending = ImageMarkupFlattenPolicy.HasPendingMarkup(_markupStrokes.Count, _markupShapes.Count);
        _flattenMarkupButton.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        if (pending)
        {
            _onEdited?.Invoke();
        }
    }

    private async Task FlattenMarkupAsync()
    {
        if (!ImageMarkupFlattenPolicy.HasPendingMarkup(_markupStrokes.Count, _markupShapes.Count))
        {
            _status.Text = ImageMarkupFlattenPolicy.NothingToFlatten;
            return;
        }

        var layer = new ImageMarkupLayer(_markupStrokes.ToList(), _markupShapes.ToList());
        await MutateAsync(
            () => _processor.FlattenMarkupAsync(_document, layer),
            ImageMarkupFlattenPolicy.Flattened(layer.Count));
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
        _status.Text = ImageViewerStatus.FormatMarkupUndone(remaining);
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
            _status.Text = ImageViewerStatus.FormatTextPlaced(_markupStrokes.Count + _markupShapes.Count);
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
                _status.Text = ImageViewerStatus.FormatMarkupAdded(kind, _markupStrokes.Count + _markupShapes.Count);
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
            _status.Text = ImageViewerStatus.FormatMarkupStrokeAdded(_markupStrokes.Count + _markupShapes.Count);
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
