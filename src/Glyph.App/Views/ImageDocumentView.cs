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
    private readonly TextBlock _status;
    private readonly TextBox _cropBox;
    private readonly ListView _siblingList;
    private readonly Button _prevButton;
    private readonly Button _nextButton;
    private readonly Button _interactiveCropButton;
    private readonly Button _applyCropButton;
    private readonly Button _cancelCropButton;
    private IReadOnlyList<string> _siblings = Array.Empty<string>();
    private double _zoom = 1.0;
    private bool _loaded;
    private bool _syncingList;
    private bool _cropMode;
    private bool _cropDragging;
    private Windows.Foundation.Point _cropStart;
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
        _cropOverlay = new Canvas
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
            IsHitTestVisible = false,
        };
        _cropOverlay.Children.Add(_cropRect);
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

        ToolTipService.SetToolTip(crop, "Crop using x,y,w,h pixels (origin top-left)");
        ToolTipService.SetToolTip(_interactiveCropButton, "Drag a rectangle on the image to crop");
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

        _cropOverlay.PointerPressed += CropOverlay_PointerPressed;
        _cropOverlay.PointerMoved += CropOverlay_PointerMoved;
        _cropOverlay.PointerReleased += CropOverlay_PointerReleased;
        _cropOverlay.PointerCaptureLost += (_, _) => _cropDragging = false;

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Thickness(8),
            Children =
            {
                _prevButton, _nextButton, zoomOut, zoomIn, fit, actual, rotateLeft, rotateRight, rotate180, orient, fullscreen, flipH, flipV,
                _cropBox, crop, _interactiveCropButton, _applyCropButton, _cancelCropButton, resize, adjust, meta, ocrButton, save, exportPng, exportJpeg, convert, _status,
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
        _cropMode = true;
        _cropOverlay.IsHitTestVisible = true;
        _interactiveCropButton.Visibility = Visibility.Collapsed;
        _applyCropButton.Visibility = Visibility.Visible;
        _cancelCropButton.Visibility = Visibility.Visible;
        ClearCropSelection();
        _status.Text = "Drag on the image to select a crop region.";
    }

    private void ExitCropMode()
    {
        _cropMode = false;
        _cropDragging = false;
        _cropOverlay.IsHitTestVisible = false;
        _interactiveCropButton.Visibility = Visibility.Visible;
        _applyCropButton.Visibility = Visibility.Collapsed;
        _cancelCropButton.Visibility = Visibility.Collapsed;
        ClearCropSelection();
        UpdateStatus();
    }

    private void ClearCropSelection()
    {
        _cropRect.Visibility = Visibility.Collapsed;
        _cropRect.Width = 0;
        _cropRect.Height = 0;
        Canvas.SetLeft(_cropRect, 0);
        Canvas.SetTop(_cropRect, 0);
    }

    private void CropOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_cropMode)
        {
            return;
        }

        _cropDragging = true;
        _cropStart = e.GetCurrentPoint(_cropOverlay).Position;
        _cropOverlay.CapturePointer(e.Pointer);
        UpdateCropRect(_cropStart, _cropStart);
        e.Handled = true;
    }

    private void CropOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_cropDragging)
        {
            return;
        }

        UpdateCropRect(_cropStart, e.GetCurrentPoint(_cropOverlay).Position);
        e.Handled = true;
    }

    private void CropOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_cropDragging)
        {
            return;
        }

        _cropDragging = false;
        _cropOverlay.ReleasePointerCapture(e.Pointer);
        UpdateCropRect(_cropStart, e.GetCurrentPoint(_cropOverlay).Position);
        e.Handled = true;
    }

    private void UpdateCropRect(Windows.Foundation.Point a, Windows.Foundation.Point b)
    {
        var x = Math.Max(0, Math.Min(a.X, b.X));
        var y = Math.Max(0, Math.Min(a.Y, b.Y));
        var right = Math.Min(_displayWidth, Math.Max(a.X, b.X));
        var bottom = Math.Min(_displayHeight, Math.Max(a.Y, b.Y));
        var w = Math.Max(0, right - x);
        var h = Math.Max(0, bottom - y);
        Canvas.SetLeft(_cropRect, x);
        Canvas.SetTop(_cropRect, y);
        _cropRect.Width = w;
        _cropRect.Height = h;
        _cropRect.Visibility = w > 0 && h > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (w > 0 && h > 0 && _displayWidth > 0 && _displayHeight > 0)
        {
            var mapped = ImageCropMapper.ToDocumentPixels(
                x, y, w, h, _displayWidth, _displayHeight, _document.PixelWidth, _document.PixelHeight);
            _status.Text = $"Crop selection → {mapped.Width}×{mapped.Height} px";
            _cropBox.Text = $"{mapped.X},{mapped.Y},{mapped.Width},{mapped.Height}";
        }
    }

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

        var aspect = (double)srcW / srcH;
        var updating = false;
        var widthBox = new TextBox { Text = srcW.ToString(), Width = 96, Header = "Width (px)" };
        var heightBox = new TextBox { Text = srcH.ToString(), Width = 96, Header = "Height (px)" };
        var percentBox = new TextBox { Text = "100", Width = 96, Header = "Scale %" };
        var lockAspect = new CheckBox { Content = "Lock aspect ratio", IsChecked = true };
        var preview = new TextBlock
        {
            Text = $"Result: {srcW}×{srcH}",
            Opacity = 0.8,
            Margin = new Thickness(0, 8, 0, 0),
        };

        void SyncFromWidth()
        {
            if (updating || !int.TryParse(widthBox.Text, out var w) || w <= 0)
            {
                return;
            }

            updating = true;
            try
            {
                if (lockAspect.IsChecked == true)
                {
                    var h = Math.Max(1, (int)Math.Round(w / aspect));
                    heightBox.Text = h.ToString();
                    percentBox.Text = Math.Round(100.0 * w / srcW).ToString("0");
                    preview.Text = $"Result: {w}×{h}";
                }
                else if (int.TryParse(heightBox.Text, out var h) && h > 0)
                {
                    percentBox.Text = Math.Round(100.0 * w / srcW).ToString("0");
                    preview.Text = $"Result: {w}×{h}";
                }
            }
            finally
            {
                updating = false;
            }
        }

        void SyncFromHeight()
        {
            if (updating || !int.TryParse(heightBox.Text, out var h) || h <= 0)
            {
                return;
            }

            updating = true;
            try
            {
                if (lockAspect.IsChecked == true)
                {
                    var w = Math.Max(1, (int)Math.Round(h * aspect));
                    widthBox.Text = w.ToString();
                    percentBox.Text = Math.Round(100.0 * h / srcH).ToString("0");
                    preview.Text = $"Result: {w}×{h}";
                }
                else if (int.TryParse(widthBox.Text, out var w) && w > 0)
                {
                    percentBox.Text = Math.Round(100.0 * h / srcH).ToString("0");
                    preview.Text = $"Result: {w}×{h}";
                }
            }
            finally
            {
                updating = false;
            }
        }

        void SyncFromPercent()
        {
            if (updating || !double.TryParse(percentBox.Text, out var pct) || pct <= 0)
            {
                return;
            }

            updating = true;
            try
            {
                var w = Math.Max(1, (int)Math.Round(srcW * pct / 100.0));
                var h = lockAspect.IsChecked == true
                    ? Math.Max(1, (int)Math.Round(w / aspect))
                    : Math.Max(1, (int)Math.Round(srcH * pct / 100.0));
                widthBox.Text = w.ToString();
                heightBox.Text = h.ToString();
                preview.Text = $"Result: {w}×{h}";
            }
            finally
            {
                updating = false;
            }
        }

        widthBox.TextChanged += (_, _) => SyncFromWidth();
        heightBox.TextChanged += (_, _) => SyncFromHeight();
        percentBox.TextChanged += (_, _) => SyncFromPercent();
        lockAspect.Checked += (_, _) => SyncFromWidth();
        lockAspect.Unchecked += (_, _) => SyncFromWidth();

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"Current: {srcW}×{srcH} px" },
                widthBox,
                heightBox,
                percentBox,
                lockAspect,
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

        if (!int.TryParse(widthBox.Text, out var width) || width <= 0
            || !int.TryParse(heightBox.Text, out var height) || height <= 0)
        {
            _status.Text = "Resize needs positive width and height.";
            return;
        }

        await MutateAsync(
            () => _processor.ResizeAsync(_document, width, height),
            $"Resized to {width}×{height}.");
    }

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
        var reset = new Button { Content = "Reset", HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            brightness.Value = 0;
            contrast.Value = 0;
            saturation.Value = 0;
        };

        var panel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Values apply destructively on OK (save to keep).",
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap,
                },
                brightness,
                contrast,
                saturation,
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

        if (Math.Abs(brightness.Value) < 0.0001
            && Math.Abs(contrast.Value) < 0.0001
            && Math.Abs(saturation.Value) < 0.0001)
        {
            _status.Text = "No adjustments to apply.";
            return;
        }

        var adjustments = new ImageAdjustments(brightness.Value, contrast.Value, saturation.Value);
        await MutateAsync(
            () => _processor.AdjustAsync(_document, adjustments),
            $"Adjusted B{brightness.Value:0}/C{contrast.Value:0}/S{saturation.Value:0}.");
    }

    private async Task ConvertAsync()
    {
        var formatBox = new ComboBox
        {
            Header = "Format",
            Width = 200,
            ItemsSource = new[] { "WebP", "TIFF", "BMP", "GIF", "AVIF", "JPEG 2000" },
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
            var needsQuality = selected is "WebP" or "AVIF";
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
            _ => (ImageEncodeFormat.Webp, ".webp"),
        };

        ImageEncodeOptions? options = null;
        if (format == ImageEncodeFormat.Webp)
        {
            options = lossless.IsChecked == true
                ? new ImageEncodeOptions(Lossless: true)
                : new ImageEncodeOptions(Quality: (int)quality.Value);
        }
        else if (format == ImageEncodeFormat.Avif)
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

    private async Task MutateAsync(Func<Task> mutation, string okStatus)
    {
        try
        {
            await mutation();
            await RefreshAsync();
            UpdateStatus();
            _status.Text = okStatus;
        }
        catch (Exception ex)
        {
            _status.Text = "Edit failed: " + ex.Message;
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
