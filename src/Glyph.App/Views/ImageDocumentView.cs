using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Imaging.Abstractions;
using Glyph.Ocr.Abstractions;
using Microsoft.UI.Xaml;
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
    private readonly IOcrEngine? _ocr;
    private readonly DocumentViewState _viewState;
    private readonly Func<string, Task>? _openSibling;
    private readonly ScrollViewer _scrollViewer;
    private readonly Grid _imageSurface;
    private readonly Image _image;
    private readonly Canvas _cropOverlay;
    private readonly Rectangle _cropRect;
    private readonly Ellipse _selectionEllipse;
    private readonly ComboBox _selectionKindBox;
    private readonly TextBlock _status;
    private readonly TextBox _cropBox;
    private readonly ListView _siblingList;
    private readonly Button _prevButton;
    private readonly Button _nextButton;
    private readonly Button _slideshowButton;
    private readonly Button _undoButton;
    private readonly Button _interactiveCropButton;
    private readonly Button _applyCropButton;
    private readonly Button _cancelCropButton;
    private readonly ComboBox _cropAspectBox;
    private readonly Button _selectButton;
    private readonly Button _selectAllButton;
    private readonly Button _deselectButton;
    private readonly Button _copySelButton;
    private readonly Button _cutSelButton;
    private readonly Button _pasteSelButton;
    private readonly Button _deleteSelButton;
    private readonly Button _cropSelButton;
    private ImageSelectionKind _selectionKind = ImageSelectionKind.Rectangle;
    private IReadOnlyList<string> _siblings = Array.Empty<string>();
    private DispatcherTimer? _slideshowTimer;
    private readonly List<IImageEditCheckpoint> _editUndoStack = [];
    private const int MaxEditUndo = 12;
    private double _zoom = 1.0;
    private bool _loaded;
    private bool _syncingList;
    private bool _cropMode;
    private bool _selectionMode;
    private bool _cropDragging;
    private bool _selectionMoving;
    private Windows.Foundation.Point _cropStart;
    private Windows.Foundation.Point _moveStart;
    private double _moveOriginLeft;
    private double _moveOriginTop;
    private ImageRect? _moveSourcePixels;
    private bool _navDragging;
    private Windows.Foundation.Point _navStart;
    private ImageRect? _pixelSelection;
    private ImagePixelBuffer? _selectionClipboard;
    private int _displayWidth;
    private int _displayHeight;

    public ImageDocumentView(
        IImageDocument document,
        IImageProcessor processor,
        IImageEncoder encoder,
        DocumentViewState? viewState = null,
        Func<string, Task>? openSibling = null,
        IOcrEngine? ocr = null)
    {
        _document = document;
        _processor = processor;
        _encoder = encoder;
        _viewState = viewState ?? new DocumentViewState();
        _openSibling = openSibling;
        _ocr = ocr;
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
        _imageSurface = new Grid();
        _imageSurface.Children.Add(_image);
        _imageSurface.Children.Add(_cropOverlay);
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
            ItemsSource = new[] { "Rect", "Ellipse" },
            SelectedIndex = 0,
        };
        _selectAllButton = new Button { Content = "All", Visibility = Visibility.Collapsed };
        _deselectButton = new Button { Content = "Deselect", Visibility = Visibility.Collapsed };
        _copySelButton = new Button { Content = "Copy sel", Visibility = Visibility.Collapsed };
        _cutSelButton = new Button { Content = "Cut sel", Visibility = Visibility.Collapsed };
        _pasteSelButton = new Button { Content = "Paste", Visibility = Visibility.Collapsed };
        _deleteSelButton = new Button { Content = "Del sel", Visibility = Visibility.Collapsed };
        _cropSelButton = new Button { Content = "Crop sel", Visibility = Visibility.Collapsed };
        ToolTipService.SetToolTip(_selectButton, "Pixel selection (drag on image; drag inside to move; arrow keys nudge)");
        ToolTipService.SetToolTip(_selectionKindBox, "Selection shape: rectangle or ellipse");
        ToolTipService.SetToolTip(_selectAllButton, "Select entire image");
        ToolTipService.SetToolTip(_deselectButton, "Clear selection");
        ToolTipService.SetToolTip(_copySelButton, "Copy selection to clipboard as PNG");
        ToolTipService.SetToolTip(_cutSelButton, "Cut selection (copy + clear)");
        ToolTipService.SetToolTip(_pasteSelButton, "Paste at selection top-left (or 0,0)");
        ToolTipService.SetToolTip(_deleteSelButton, "Clear selection to transparent");
        ToolTipService.SetToolTip(_cropSelButton, "Crop image to selection");
        var resize = new Button { Content = "Resize" };
        var adjust = new Button { Content = "Adjust" };
        var meta = new Button { Content = "Meta" };
        var ocrButton = new Button { Content = "OCR" };
        var rotate180 = new Button { Content = "180°" };
        var orient = new Button { Content = "Orient" };
        var fullscreen = new Button { Content = "Fullscreen" };
        var save = new Button { Content = "Save" };
        var exportPng = new Button { Content = "→PNG" };
        var exportJpeg = new Button { Content = "→JPEG" };
        var convert = new Button { Content = "Convert" };
        _prevButton = new Button { Content = "◀", Width = 36 };
        _nextButton = new Button { Content = "▶", Width = 36 };
        _slideshowButton = new Button { Content = "Slideshow" };
        _undoButton = new Button { Content = "Undo", IsEnabled = false };

        ToolTipService.SetToolTip(crop, "Crop using x,y,w,h pixels (origin top-left)");
        ToolTipService.SetToolTip(_interactiveCropButton, "Drag a rectangle on the image to crop");
        ToolTipService.SetToolTip(_applyCropButton, "Apply the dragged crop rectangle");
        ToolTipService.SetToolTip(_cancelCropButton, "Cancel interactive crop");
        ToolTipService.SetToolTip(resize, "Resize width/height with optional aspect lock");
        ToolTipService.SetToolTip(adjust, "Brightness / contrast / saturation");
        ToolTipService.SetToolTip(meta, "Image metadata, EXIF, and GPS");
        ToolTipService.SetToolTip(ocrButton, "Run offline OCR on this image");
        ToolTipService.SetToolTip(rotate180, "Rotate 180°");
        ToolTipService.SetToolTip(orient, "Apply EXIF orientation into pixels");
        ToolTipService.SetToolTip(fullscreen, "Toggle window fullscreen");
        ToolTipService.SetToolTip(exportPng, "Export as PNG");
        ToolTipService.SetToolTip(exportJpeg, "Export as JPEG");
        ToolTipService.SetToolTip(convert, "Export as WebP, TIFF, BMP, or GIF");
        ToolTipService.SetToolTip(_prevButton, "Previous image in folder");
        ToolTipService.SetToolTip(_nextButton, "Next image in folder");
        ToolTipService.SetToolTip(_slideshowButton, "Play/stop folder slideshow (3s, loops; Esc stops)");
        ToolTipService.SetToolTip(_undoButton, "Undo last crop/resize/rotate/adjust (Ctrl+Z)");

        zoomOut.Click += async (_, _) => await SetZoomAsync(_zoom / 1.25);
        zoomIn.Click += async (_, _) => await SetZoomAsync(_zoom * 1.25);
        fit.Click += async (_, _) => await FitAsync();
        actual.Click += async (_, _) => await SetZoomAsync(1.0);
        rotateLeft.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), "Rotated left.");
        rotateRight.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), "Rotated right.");
        rotate180.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 180), "Rotated 180°.");
        orient.Click += async (_, _) => await MutateAsync(() => _processor.NormalizeOrientationAsync(_document), "Orientation normalized.");
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
            _selectionKind = _selectionKindBox.SelectedIndex == 1
                ? ImageSelectionKind.Ellipse
                : ImageSelectionKind.Rectangle;
            SyncSelectionOverlayShape();
        };
        _selectAllButton.Click += (_, _) => SelectAllPixels();
        _deselectButton.Click += (_, _) => ClearPixelSelection();
        _copySelButton.Click += async (_, _) => await CopySelectionAsync();
        _cutSelButton.Click += async (_, _) => await CutSelectionAsync();
        _pasteSelButton.Click += async (_, _) => await PasteSelectionAsync();
        _deleteSelButton.Click += async (_, _) => await DeleteSelectionAsync();
        _cropSelButton.Click += async (_, _) => await CropToSelectionAsync();
        resize.Click += async (_, _) => await ResizeAsync();
        adjust.Click += async (_, _) => await AdjustAsync();
        meta.Click += async (_, _) => await ShowMetadataAsync();
        ocrButton.Click += async (_, _) => await RunOcrAsync();
        save.Click += async (_, _) => await SaveAsync();
        exportPng.Click += async (_, _) => await ExportAsync(ImageEncodeFormat.Png, ".png");
        exportJpeg.Click += async (_, _) => await ExportJpegAsync();
        convert.Click += async (_, _) => await ConvertAsync();
        _prevButton.Click += async (_, _) => await NavigateSiblingAsync(-1);
        _nextButton.Click += async (_, _) => await NavigateSiblingAsync(1);
        _slideshowButton.Click += (_, _) => ToggleSlideshow();
        _undoButton.Click += async (_, _) => await UndoEditAsync();
        KeyDown += ImageDocumentView_KeyDown;
        Unloaded += (_, _) =>
        {
            StopSlideshowTimerOnly();
            ClearEditUndoStack();
        };

        _cropOverlay.PointerPressed += CropOverlay_PointerPressed;
        _cropOverlay.PointerMoved += CropOverlay_PointerMoved;
        _cropOverlay.PointerReleased += CropOverlay_PointerReleased;
        _cropOverlay.PointerCaptureLost += (_, _) =>
        {
            _cropDragging = false;
            _selectionMoving = false;
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Thickness(8),
            Children =
            {
                _prevButton, _nextButton, _slideshowButton, _undoButton, zoomOut, zoomIn, fit, actual, rotateLeft, rotateRight, rotate180, orient, fullscreen, flipH, flipV,
                _cropBox, crop, _interactiveCropButton, _cropAspectBox, _applyCropButton, _cancelCropButton,
                _selectButton, _selectionKindBox, _selectAllButton, _deselectButton, _copySelButton, _cutSelButton, _pasteSelButton, _deleteSelButton, _cropSelButton,
                resize, adjust, meta, ocrButton, save, exportPng, exportJpeg, convert, _status,
            },
        };

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
            Text = "Folder",
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
        await RefreshAsync();
        UpdateStatus();
        if (_viewState.IsSlideshowActive)
        {
            StartSlideshow(resume: true);
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
        if (_cropMode || _selectionMode || e.GetCurrentPoint(_scrollViewer).Properties.IsRightButtonPressed)
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
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrl && e.Key == Windows.System.VirtualKey.Z)
        {
            _ = UndoEditAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Windows.System.VirtualKey.C && _pixelSelection is not null)
        {
            _ = CopySelectionAsync();
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
            _ = PasteSelectionAsync();
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
        var destX = Math.Clamp(sel.X + dx, 0, Math.Max(0, _document.PixelWidth - sel.Width));
        var destY = Math.Clamp(sel.Y + dy, 0, Math.Max(0, _document.PixelHeight - sel.Height));
        if (destX == sel.X && destY == sel.Y)
        {
            return;
        }

        await MutateAsync(
            () => _processor.MoveRectAsync(_document, sel, destX, destY, _selectionKind),
            $"Moved selection to ({destX},{destY}).");
        SetPixelSelection(new ImageRect(destX, destY, sel.Width, sel.Height));
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
            var buffer = await _processor.ExtractRectAsync(_document, sel, _selectionKind);
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
                _status.Text = $"Copied selection {sel.Width}×{sel.Height}.";
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
            () => _processor.ClearRectAsync(_document, sel, transparent: true, _selectionKind),
            $"Cut selection {sel.Width}×{sel.Height}.");
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
            () => _processor.ClearRectAsync(_document, sel, transparent: true, _selectionKind),
            $"Cleared selection {sel.Width}×{sel.Height}.");
    }

    private async Task CropToSelectionAsync()
    {
        if (_pixelSelection is not { } sel || sel.Width < 1 || sel.Height < 1)
        {
            _status.Text = "Make a selection first.";
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
            checkpoint = _document.CaptureCheckpoint();
            await mutation();
            PushUndo(checkpoint);
            checkpoint = null;
            await RefreshAsync();
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
            return;
        }

        _status.Text = "Fullscreen unavailable.";
    }

    private async Task RefreshAsync()
    {
        // Cap decode edge for very large images so the viewer stays responsive.
        var maxEdge = (int)Math.Clamp(Math.Max(_document.PixelWidth, _document.PixelHeight) * _zoom, 256, 8192);
        var buffer = await _document.GetPixelsAsync(maxEdge);
        var bitmap = new WriteableBitmap(buffer.Width, buffer.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            await stream.WriteAsync(buffer.BgraPixels, 0, buffer.BgraPixels.Length);
        }

        bitmap.Invalidate();
        _image.Source = bitmap;
        _image.Width = buffer.Width;
        _image.Height = buffer.Height;
        _displayWidth = buffer.Width;
        _displayHeight = buffer.Height;
        _cropOverlay.Width = buffer.Width;
        _cropOverlay.Height = buffer.Height;
        _imageSurface.Width = buffer.Width;
        _imageSurface.Height = buffer.Height;
        _viewState.Zoom = _zoom;
        if (_cropMode)
        {
            ClearCropSelection();
        }
    }

    private async Task SetZoomAsync(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.05, 8.0);
        await RefreshAsync();
        UpdateStatus();
    }

    private async Task FitAsync()
    {
        var availableW = Math.Max(1, _scrollViewer.ActualWidth - 24);
        var availableH = Math.Max(1, _scrollViewer.ActualHeight - 24);
        var scaleW = availableW / _document.PixelWidth;
        var scaleH = availableH / _document.PixelHeight;
        await SetZoomAsync(Math.Min(scaleW, scaleH));
    }

    private async Task CropAsync()
    {
        var parts = (_cropBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4
            || !int.TryParse(parts[0], out var x)
            || !int.TryParse(parts[1], out var y)
            || !int.TryParse(parts[2], out var w)
            || !int.TryParse(parts[3], out var h))
        {
            _status.Text = "Crop needs x,y,w,h integers.";
            return;
        }

        await MutateAsync(() => _processor.CropAsync(_document, new ImageRect(x, y, w, h)), $"Cropped to {w}×{h}.");
    }

    private void EnterCropMode()
    {
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

        _selectionMode = true;
        _cropOverlay.IsHitTestVisible = true;
        _selectButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 140, 0));
        _selectionKindBox.Visibility = Visibility.Visible;
        _selectAllButton.Visibility = Visibility.Visible;
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

    private void SelectAllPixels()
    {
        if (!_selectionMode)
        {
            ToggleSelectionMode();
        }

        SetPixelSelection(new ImageRect(0, 0, _document.PixelWidth, _document.PixelHeight));
        _status.Text = $"Selected all {_document.PixelWidth}×{_document.PixelHeight}.";
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
        }
        else
        {
            _cropRect.Visibility = Visibility.Visible;
            _selectionEllipse.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearPixelSelection()
    {
        _pixelSelection = null;
        _selectionMoving = false;
        _moveSourcePixels = null;
        ClearCropSelection();
        if (_selectionMode)
        {
            _status.Text = "Selection cleared.";
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
            var cx = left + (w / 2.0);
            var cy = top + (h / 2.0);
            var rx = w / 2.0;
            var ry = h / 2.0;
            if (rx <= 0 || ry <= 0)
            {
                return false;
            }

            var nx = (point.X - cx) / rx;
            var ny = (point.Y - cy) / ry;
            return (nx * nx) + (ny * ny) <= 1.0;
        }

        return point.X >= left
            && point.Y >= top
            && point.X <= left + w
            && point.Y <= top + h;
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
        _cropOverlay.CapturePointer(e.Pointer);
        UpdateCropRect(_cropStart, _cropStart);
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
            e.Handled = true;
            return;
        }

        if (!_cropDragging)
        {
            return;
        }

        UpdateCropRect(_cropStart, e.GetCurrentPoint(_cropOverlay).Position);
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
            SyncSelectionOverlayShape();
            var shape = _selectionKind == ImageSelectionKind.Ellipse ? "ellipse" : "rect";
            _status.Text = $"Selected {_pixelSelection.Value.Width}×{_pixelSelection.Value.Height} px ({shape})";
        }

        e.Handled = true;
    }

    private async Task CommitSelectionMoveAsync(ImageRect source, int destX, int destY)
    {
        var w = source.Width;
        var h = source.Height;
        destX = Math.Clamp(destX, 0, Math.Max(0, _document.PixelWidth - w));
        destY = Math.Clamp(destY, 0, Math.Max(0, _document.PixelHeight - h));
        await MutateAsync(
            () => _processor.MoveRectAsync(_document, source, destX, destY, _selectionKind),
            $"Moved selection to ({destX},{destY}).");
        SetPixelSelection(new ImageRect(destX, destY, w, h));
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
                var shape = _selectionKind == ImageSelectionKind.Ellipse ? "ellipse" : "rect";
                _status.Text = $"Selection ({shape}) → {mapped.Width}×{mapped.Height} px";
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
            SelectedIndex = 0,
        };
        var preview = new TextBlock
        {
            Text = $"Result: {srcW}×{srcH} px · ~{EstimateRawMb(srcW, srcH):0.##} MB raw",
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

            preview.Text = $"Result: {w}×{h} px @ {ActiveDpi():0.#} DPI · ~{EstimateRawMb(w, h):0.##} MB raw BGRA";
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

        var filter = (ImageResizeFilter)Math.Clamp(filterBox.SelectedIndex, 0, 3);
        var options = new ImageResizeOptions(Filter: filter, DensityDpi: ActiveDpi());
        await MutateAsync(
            () => _processor.ResizeAsync(_document, width, height, options),
            $"Resized to {width}×{height} @ {ActiveDpi():0.#} DPI.");
    }

    private static double EstimateRawMb(int width, int height) =>
        width * (double)height * 4.0 / (1024.0 * 1024.0);

    private async Task AdjustAsync()
    {
        Slider MakeSlider(string header, double min, double max, double value)
        {
            return new Slider
            {
                Header = header,
                Minimum = min,
                Maximum = max,
                Value = value,
                StepFrequency = 1,
                Width = 280,
            };
        }

        var brightness = MakeSlider("Brightness (−100…100)", -100, 100, 0);
        var contrast = MakeSlider("Contrast (−100…100)", -100, 100, 0);
        var saturation = MakeSlider("Saturation (−100…100)", -100, 100, 0);
        var highlights = MakeSlider("Highlights (−100 recover…100)", -100, 100, 0);
        var shadows = MakeSlider("Shadows (−100 crush…100 lift)", -100, 100, 0);
        var temperature = MakeSlider("Temperature (−100 cold…100 warm)", -100, 100, 0);
        var tint = MakeSlider("Tint (−100 green…100 magenta)", -100, 100, 0);
        var sharpness = MakeSlider("Sharpness (0…100)", 0, 100, 0);
        var autoLevels = new CheckBox { Content = "Auto Levels", IsChecked = false };
        var sepia = new CheckBox { Content = "Sepia", IsChecked = false };
        var reset = new Button { Content = "Reset", HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            brightness.Value = 0;
            contrast.Value = 0;
            saturation.Value = 0;
            highlights.Value = 0;
            shadows.Value = 0;
            temperature.Value = 0;
            tint.Value = 0;
            sharpness.Value = 0;
            autoLevels.IsChecked = false;
            sepia.IsChecked = false;
        };

        var panel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Values apply on OK (Undo / Ctrl+Z available; Save to keep on disk).",
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap,
                },
                autoLevels,
                brightness,
                contrast,
                highlights,
                shadows,
                saturation,
                temperature,
                tint,
                sharpness,
                sepia,
                reset,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Color adjustments",
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

        var useAuto = autoLevels.IsChecked == true;
        var useSepia = sepia.IsChecked == true;
        if (!useAuto
            && !useSepia
            && Math.Abs(brightness.Value) < 0.0001
            && Math.Abs(contrast.Value) < 0.0001
            && Math.Abs(saturation.Value) < 0.0001
            && Math.Abs(highlights.Value) < 0.0001
            && Math.Abs(shadows.Value) < 0.0001
            && Math.Abs(temperature.Value) < 0.0001
            && Math.Abs(tint.Value) < 0.0001
            && Math.Abs(sharpness.Value) < 0.0001)
        {
            _status.Text = "No adjustments to apply.";
            return;
        }

        var adjustments = new ImageAdjustments(
            Brightness: brightness.Value,
            Contrast: contrast.Value,
            Saturation: saturation.Value,
            AutoLevels: useAuto,
            Sharpness: sharpness.Value,
            Sepia: useSepia,
            Temperature: temperature.Value,
            Tint: tint.Value,
            Highlights: highlights.Value,
            Shadows: shadows.Value);
        await MutateAsync(
            () => _processor.AdjustAsync(_document, adjustments),
            "Color adjustments applied.");
    }

    private async Task ConvertAsync()
    {
        var formatBox = new ComboBox
        {
            Header = "Format",
            Width = 200,
            ItemsSource = new[] { "WebP", "TIFF", "BMP", "GIF", "AVIF", "JPEG 2000", "HEIC" },
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
        void SyncWebpOptions()
        {
            var selected = formatBox.SelectedItem as string;
            var isWebp = selected == "WebP";
            var needsQuality = selected is "WebP" or "AVIF" or "HEIC";
            quality.Visibility = needsQuality && !(isWebp && lossless.IsChecked == true)
                ? Visibility.Visible
                : Visibility.Collapsed;
            lossless.Visibility = isWebp ? Visibility.Visible : Visibility.Collapsed;
        }

        formatBox.SelectionChanged += (_, _) => SyncWebpOptions();
        lossless.Checked += (_, _) => SyncWebpOptions();
        lossless.Unchecked += (_, _) => SyncWebpOptions();
        SyncWebpOptions();

        var panel = new StackPanel { Spacing = 8, Children = { formatBox, lossless, quality } };
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
            _ => (ImageEncodeFormat.Webp, ".webp"),
        };

        ImageEncodeOptions? options = null;
        if (format == ImageEncodeFormat.Webp)
        {
            options = lossless.IsChecked == true
                ? new ImageEncodeOptions(Lossless: true)
                : new ImageEncodeOptions(Quality: (int)quality.Value);
        }
        else if (format is ImageEncodeFormat.Avif or ImageEncodeFormat.Heic)
        {
            options = new ImageEncodeOptions(Quality: (int)quality.Value);
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
                    new TextBlock
                    {
                        Text = $"{info.FormatName} · {info.PixelWidth}×{info.PixelHeight}"
                            + (info.Make is null ? string.Empty : $" · {info.Make} {info.Model}".TrimEnd()),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    list,
                    actions,
                },
            };

            var dialog = new ContentDialog
            {
                Title = "Image metadata",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Metadata failed: " + ex.Message;
        }
    }

    private async Task RunOcrAsync()
    {
        if (_ocr is null)
        {
            _status.Text = "OCR engine unavailable.";
            return;
        }

        try
        {
            _status.Text = "Running OCR…";
            var buffer = await _document.GetPixelsAsync(maxEdge: 4096);
            var result = await _ocr.RecognizeAsync(
                new OcrRequest(buffer.Width, buffer.Height, buffer.BgraPixels));

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

    private async Task SaveAsync()
    {
        try
        {
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

    private void UpdateStatus()
    {
        var baseStatus =
            $"{_document.FormatName} {_document.PixelWidth}×{_document.PixelHeight} · {(_zoom * 100):0}%";
        if (!string.IsNullOrWhiteSpace(_document.Path) && _siblings.Count > 0)
        {
            var index = ImageFolderNavigator.IndexOf(_siblings, _document.Path);
            if (index >= 0)
            {
                baseStatus += $" · {index + 1}/{_siblings.Count}";
            }
        }

        _status.Text = baseStatus;
    }
}
