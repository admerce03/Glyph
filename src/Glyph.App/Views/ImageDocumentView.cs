using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Documents;
using Glyph.Imaging.Abstractions;
using Glyph.Ocr.Abstractions;
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
    private readonly IOcrEngine _ocr;
    private readonly IImageMetadataService? _metadata;
    private readonly IImageColorProfileService? _color;
    private readonly IImageBatchService? _batch;
    private readonly IScannerService? _scanner;
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
        IOcrEngine ocr,
        DocumentViewState? viewState = null,
        IImageMetadataService? metadata = null,
        IImageColorProfileService? color = null,
        IImageBatchService? batch = null,
        IScannerService? scanner = null)
    {
        _document = document;
        _processor = processor;
        _encoder = encoder;
        _ocr = ocr;
        _metadata = metadata;
        _color = color;
        _batch = batch;
        _scanner = scanner;
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
        var save = new Button { Content = "Save" };
        var exportPng = new Button { Content = "→PNG" };
        var exportJpeg = new Button { Content = "→JPEG" };
        var ocrButton = new Button { Content = "OCR" };
        var meta = new Button { Content = "Meta" };
        var srgb = new Button { Content = "→sRGB" };
        var stripGps = new Button { Content = "Strip GPS" };
        var scan = new Button { Content = "Scan" };

        ToolTipService.SetToolTip(crop, "Crop using x,y,w,h pixels (origin top-left)");
        ToolTipService.SetToolTip(exportPng, "Export as PNG");
        ToolTipService.SetToolTip(exportJpeg, "Export as JPEG");
        ToolTipService.SetToolTip(ocrButton, "Offline OCR of the current image");
        ToolTipService.SetToolTip(meta, "View / edit image metadata");
        ToolTipService.SetToolTip(srgb, "Convert embedded color profile to sRGB");
        ToolTipService.SetToolTip(stripGps, "Remove GPS EXIF tags");
        ToolTipService.SetToolTip(scan, "Scan via emulated flatbed (hardware WIA later)");

        zoomOut.Click += async (_, _) => await SetZoomAsync(_zoom / 1.25);
        zoomIn.Click += async (_, _) => await SetZoomAsync(_zoom * 1.25);
        fit.Click += async (_, _) => await FitAsync();
        actual.Click += async (_, _) => await SetZoomAsync(1.0);
        rotateLeft.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, -90), "Rotated left.");
        rotateRight.Click += async (_, _) => await MutateAsync(() => _processor.RotateAsync(_document, 90), "Rotated right.");
        flipH.Click += async (_, _) => await MutateAsync(() => _processor.FlipHorizontalAsync(_document), "Flipped horizontally.");
        flipV.Click += async (_, _) => await MutateAsync(() => _processor.FlipVerticalAsync(_document), "Flipped vertically.");
        crop.Click += async (_, _) => await CropAsync();
        save.Click += async (_, _) => await SaveAsync();
        exportPng.Click += async (_, _) => await ExportAsync(ImageEncodeFormat.Png, ".png");
        exportJpeg.Click += async (_, _) => await ExportAsync(ImageEncodeFormat.Jpeg, ".jpg");
        ocrButton.Click += async (_, _) => await RunOcrAsync();
        meta.Click += async (_, _) => await EditMetadataAsync();
        srgb.Click += async (_, _) => await ConvertSrgbAsync();
        stripGps.Click += async (_, _) => await StripGpsAsync();
        scan.Click += async (_, _) => await ScanEmulatedAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Padding = new Thickness(8),
            Children =
            {
                zoomOut, zoomIn, fit, actual, rotateLeft, rotateRight, flipH, flipV,
                _cropBox, crop, save, exportPng, exportJpeg, ocrButton, meta, srgb, stripGps, scan, _status,
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

    private async Task RunOcrAsync()
    {
        if (!_ocr.IsAvailable)
        {
            _status.Text = "OCR engine unavailable (install tesseract).";
            return;
        }

        try
        {
            _status.Text = "Running OCR…";
            var progress = new Progress<OcrProgress>(p => _status.Text = $"OCR {p.Fraction:P0}: {p.Status}");
            var buffer = await _document.GetPixelsAsync(maxEdge: 2400);
            var result = await _ocr.RecognizeAsync(
                new OcrRequest(buffer.Width, buffer.Height, buffer.BgraPixels, Progress: progress));
            var entitySummary = result.Entities.Count == 0
                ? "no entities"
                : string.Join(", ", result.Entities.Take(4).Select(e => $"{e.Kind}:{e.Value}"));
            var preview = string.IsNullOrWhiteSpace(result.Text)
                ? "(no text)"
                : (result.Text.Length > 80 ? result.Text[..80] + "…" : result.Text.Replace('\n', ' '));
            _status.Text = $"OCR: {preview} · {entitySummary}";
        }
        catch (Exception ex)
        {
            _status.Text = "OCR failed: " + ex.Message;
        }
    }

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
            var description = new TextBox { Header = "Description", Text = current.Description ?? string.Empty };
            var keywords = new TextBox { Header = "Keywords", Text = current.Keywords ?? string.Empty };
            var copyright = new TextBox { Header = "Copyright", Text = current.Copyright ?? string.Empty };
            var info = new TextBlock
            {
                Text =
                    $"{current.PixelWidth}×{current.PixelHeight} · {current.FormatName} · " +
                    $"DPI {current.DensityX:0}/{current.DensityY:0} · " +
                    $"Camera {current.CameraMake ?? "-"} {current.CameraModel ?? ""} · " +
                    $"GPS {(current.GpsLatitude is null ? "none" : $"{current.GpsLatitude:F5},{current.GpsLongitude:F5}")}",
                TextWrapping = TextWrapping.WrapWholeWords,
            };
            var dialog = new ContentDialog
            {
                Title = "Image metadata",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children = { info, title, description, keywords, copyright },
                },
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await _metadata.SetAsync(_document, title.Text, description.Text, keywords.Text, copyright.Text);
            _status.Text = "Image metadata updated (save to persist).";
        }
        catch (Exception ex)
        {
            _status.Text = "Metadata failed: " + ex.Message;
        }
    }

    private async Task ConvertSrgbAsync()
    {
        if (_color is null)
        {
            _status.Text = "Color profile service unavailable.";
            return;
        }

        await MutateAsync(() => _color.ConvertToSrgbAsync(_document), "Converted to sRGB.");
    }

    private async Task StripGpsAsync()
    {
        if (_metadata is null)
        {
            _status.Text = "Metadata service unavailable.";
            return;
        }

        await MutateAsync(() => _metadata.StripGpsAsync(_document), "GPS tags removed.");
    }

    private async Task ScanEmulatedAsync()
    {
        if (_scanner is null)
        {
            _status.Text = "Scanner service unavailable.";
            return;
        }

        try
        {
            var devices = await _scanner.ListDevicesAsync();
            var device = devices.FirstOrDefault();
            if (device is null)
            {
                _status.Text = "No scanners found.";
                return;
            }

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var window = App.CurrentApp.MainWindowInstance
                ?? throw new InvalidOperationException("Main window unavailable.");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedFileName = "scan.png";
            picker.FileTypeChoices.Add("PNG", [".png"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                _status.Text = "Scan cancelled.";
                return;
            }

            _status.Text = $"Scanning via {device.Name}…";
            await _scanner.ScanAsync(new ScanRequest(device.Id, file.Path, Dpi: 150));
            _status.Text = device.IsEmulated
                ? $"Emulated scan saved to {file.Name}."
                : $"Scan saved to {file.Name}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Scan failed: " + ex.Message;
        }
    }

    private void UpdateStatus()
    {
        _status.Text =
            $"{_document.FormatName} {_document.PixelWidth}×{_document.PixelHeight} · {(_zoom * 100):0}%";
    }
}
