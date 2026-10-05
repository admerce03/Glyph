using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Imaging.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Glyph.App.Views;

/// <summary>
/// Image viewer/editor: zoom/pan, fit, rotate/flip/crop, and format conversion.
/// </summary>
public sealed class ImageDocumentView : UserControl
{
    private readonly IImageDocument _document;
    private readonly IImageProcessor _processor;
    private readonly IImageEncoder _encoder;
    private readonly DocumentViewState _viewState;
    private readonly ScrollViewer _scrollViewer;
    private readonly Image _image;
    private readonly TextBlock _status;
    private readonly TextBox _cropBox;
    private double _zoom = 1.0;
    private bool _loaded;

    public ImageDocumentView(
        IImageDocument document,
        IImageProcessor processor,
        IImageEncoder encoder,
        DocumentViewState? viewState = null)
    {
        _document = document;
        _processor = processor;
        _encoder = encoder;
        _viewState = viewState ?? new DocumentViewState();
        _zoom = _viewState.Zoom <= 0 ? 1.0 : _viewState.Zoom;

        _image = new Image
        {
            Stretch = Microsoft.UI.Xaml.Media.Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _scrollViewer = new ScrollViewer
        {
            Content = _image,
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

        var zoomOut = new Button { Content = "−", Width = 36 };
        var zoomIn = new Button { Content = "+", Width = 36 };
        var fit = new Button { Content = "Fit" };
        var actual = new Button { Content = "100%" };
        var rotateLeft = new Button { Content = "⟲" };
        var rotateRight = new Button { Content = "⟳" };
        var flipH = new Button { Content = "Flip H" };
        var flipV = new Button { Content = "Flip V" };
        var crop = new Button { Content = "Crop" };
        var resize = new Button { Content = "Resize" };
        var adjust = new Button { Content = "Adjust" };
        var rotate180 = new Button { Content = "180°" };
        var save = new Button { Content = "Save" };
        var exportPng = new Button { Content = "→PNG" };
        var exportJpeg = new Button { Content = "→JPEG" };
        var convert = new Button { Content = "Convert" };

        ToolTipService.SetToolTip(crop, "Crop using x,y,w,h pixels (origin top-left)");
        ToolTipService.SetToolTip(resize, "Resize width/height with optional aspect lock");
        ToolTipService.SetToolTip(adjust, "Brightness / contrast / saturation");
        ToolTipService.SetToolTip(rotate180, "Rotate 180°");
        ToolTipService.SetToolTip(exportPng, "Export as PNG");
        ToolTipService.SetToolTip(exportJpeg, "Export as JPEG");
        ToolTipService.SetToolTip(convert, "Export as WebP, TIFF, BMP, or GIF");

        zoomOut.Click += async (_, _) => await SetZoomAsync(_zoom / 1.25);
        zoomIn.Click += async (_, _) => await SetZoomAsync(_zoom * 1.25);
        fit.Click += async (_, _) => await FitAsync();
        actual.Click += async (_, _) => await SetZoomAsync(1.0);
        rotateLeft.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), "Rotated left.");
        rotateRight.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), "Rotated right.");
        rotate180.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 180), "Rotated 180°.");
        flipH.Click += async (_, _) => await MutateAsync(() => _processor.FlipHorizontalAsync(_document), "Flipped horizontally.");
        flipV.Click += async (_, _) => await MutateAsync(() => _processor.FlipVerticalAsync(_document), "Flipped vertically.");
        crop.Click += async (_, _) => await CropAsync();
        resize.Click += async (_, _) => await ResizeAsync();
        adjust.Click += async (_, _) => await AdjustAsync();
        save.Click += async (_, _) => await SaveAsync();
        exportPng.Click += async (_, _) => await ExportAsync(ImageEncodeFormat.Png, ".png");
        exportJpeg.Click += async (_, _) => await ExportAsync(ImageEncodeFormat.Jpeg, ".jpg");
        convert.Click += async (_, _) => await ConvertAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Thickness(8),
            Children =
            {
                zoomOut, zoomIn, fit, actual, rotateLeft, rotateRight, rotate180, flipH, flipV,
                _cropBox, crop, resize, adjust, save, exportPng, exportJpeg, convert, _status,
            },
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        root.Children.Add(toolbar);
        Grid.SetRow(_scrollViewer, 1);
        root.Children.Add(_scrollViewer);
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
        await RefreshAsync();
        UpdateStatus();
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
        _viewState.Zoom = _zoom;
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
            ItemsSource = new[] { "WebP", "TIFF", "BMP", "GIF" },
            SelectedIndex = 0,
        };
        var panel = new StackPanel { Spacing = 8, Children = { formatBox } };
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

        var (format, extension) = formatBox.SelectedItem as string switch
        {
            "WebP" => (ImageEncodeFormat.Webp, ".webp"),
            "TIFF" => (ImageEncodeFormat.Tiff, ".tif"),
            "BMP" => (ImageEncodeFormat.Bmp, ".bmp"),
            "GIF" => (ImageEncodeFormat.Gif, ".gif"),
            _ => (ImageEncodeFormat.Webp, ".webp"),
        };
        await ExportAsync(format, extension);
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

    private async Task ExportAsync(ImageEncodeFormat format, string extension)
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

            await _encoder.SaveAsAsync(_document, file.Path, format);
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
        _status.Text =
            $"{_document.FormatName} {_document.PixelWidth}×{_document.PixelHeight} · {(_zoom * 100):0}%";
    }
}
