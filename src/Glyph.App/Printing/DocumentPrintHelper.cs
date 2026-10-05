using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Core.Printing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using Windows.Graphics.Printing;
using Windows.Graphics.Printing.OptionDetails;

namespace Glyph.App.Printing;

/// <summary>App alias for <see cref="PrintScaleMode"/> (F44-13–16).</summary>
public enum DocumentPrintScaleMode
{
    Fit = PrintScaleMode.Fit,
    Fill = PrintScaleMode.Fill,
    ActualSize = PrintScaleMode.ActualSize,
}

/// <summary>
/// WinUI print session: builds preview pages from bitmaps and shows the system print UI
/// (printer selection, copies, duplex, collate, paper via Windows printer properties — F44).
/// Supports 1-up and 2-up (pages per sheet).
/// </summary>
public sealed class DocumentPrintHelper : IDisposable
{
    private readonly Window _window;
    private readonly string _jobName;
    private readonly DocumentPrintScaleMode _scaleMode;
    private readonly bool _center;
    private readonly bool _autoRotate;
    private readonly int _pagesPerSheet;
    private PrintDocument? _printDocument;
    private IPrintDocumentSource? _printDocumentSource;
    private List<UIElement> _previewPages = [];
    private List<WriteableBitmap> _bitmaps = [];
    private PrintManager? _printManager;
    private bool _subscribed;
    private TaskCompletionSource<bool>? _completion;

    public DocumentPrintHelper(
        Window window,
        string jobName,
        DocumentPrintScaleMode scaleMode = DocumentPrintScaleMode.Fit,
        bool center = true,
        bool autoRotate = true,
        int pagesPerSheet = 1)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _jobName = string.IsNullOrWhiteSpace(jobName) ? "Glyph" : jobName;
        _scaleMode = scaleMode;
        _center = center;
        _autoRotate = autoRotate;
        _pagesPerSheet = PrintSheetLayout.NormalizePagesPerSheet(pagesPerSheet);
    }

    public async Task PrintAsync(IReadOnlyList<WriteableBitmap> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        cancellationToken.ThrowIfCancellationRequested();
        _bitmaps = pages.ToList();
        _printDocument = new PrintDocument();
        _printDocumentSource = _printDocument.DocumentSource;
        _printDocument.Paginate += OnPaginate;
        _printDocument.GetPreviewPage += OnGetPreviewPage;
        _printDocument.AddPages += OnAddPages;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _printManager = PrintManagerInterop.GetForWindow(hwnd);
        _printManager.PrintTaskRequested += OnPrintTaskRequested;
        _subscribed = true;

        _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
            using var reg = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
            await _completion.Task;
        }
        finally
        {
            Unsubscribe();
        }
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        var deferral = args.Request.GetDeferral();
        try
        {
            var task = args.Request.CreatePrintTask(_jobName, sourceRequested =>
            {
                sourceRequested.SetSource(_printDocumentSource);
            });
            task.Completed += (_, completeArgs) =>
            {
                var ok = completeArgs.Completion == PrintTaskCompletion.Submitted;
                _completion?.TrySetResult(ok);
            };

            // Expose standard Windows options (copies/collate/duplex/media) via option details.
            try
            {
                var details = PrintTaskOptionDetails.GetFromPrintTaskOptions(task.Options);
                _ = details; // Presence enables system printer property sheet integration.
            }
            catch
            {
                // Option details unavailable on some printers — UI still works.
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnPaginate(object sender, PaginateEventArgs e)
    {
        if (_printDocument is null)
        {
            return;
        }

        _previewPages.Clear();
        var pageDesc = e.PrintTaskOptions.GetPageDescription(0);
        var printable = pageDesc.ImageableRect;
        var pageWidth = printable.Width;
        var pageHeight = printable.Height;

        var cells = PrintSheetLayout.Cells(pageWidth, pageHeight, _pagesPerSheet);
        if (_pagesPerSheet <= 1)
        {
            foreach (var bitmap in _bitmaps)
            {
                _previewPages.Add(BuildSinglePage(bitmap, pageWidth, pageHeight));
            }
        }
        else
        {
            for (var i = 0; i < _bitmaps.Count; i += cells.Count)
            {
                var canvas = new Canvas { Width = pageWidth, Height = pageHeight };
                for (var c = 0; c < cells.Count && i + c < _bitmaps.Count; c++)
                {
                    var cell = cells[c];
                    PlaceInCell(canvas, _bitmaps[i + c], cell.Left, cell.Top, cell.Width, cell.Height);
                }

                _previewPages.Add(canvas);
            }
        }

        _printDocument.SetPreviewPageCount(_previewPages.Count, PreviewPageCountType.Final);
    }

    private void PlaceInCell(Canvas sheet, WriteableBitmap bitmap, double left, double top, double cellW, double cellH)
    {
        var content = BuildContent(bitmap, cellW, cellH, out var targetW, out var targetH);
        var (x, y) = PrintSheetLayout.PlaceInCell(left, top, cellW, cellH, targetW, targetH, _center);
        Canvas.SetLeft(content, x);
        Canvas.SetTop(content, y);
        sheet.Children.Add(content);
    }

    private Canvas BuildSinglePage(WriteableBitmap bitmap, double pageWidth, double pageHeight)
    {
        var canvas = new Canvas
        {
            Width = pageWidth,
            Height = pageHeight,
        };
        var content = BuildContent(bitmap, pageWidth, pageHeight, out var targetW, out var targetH);
        var (left, top) = PrintSheetLayout.PlaceInCell(0, 0, pageWidth, pageHeight, targetW, targetH, _center);
        Canvas.SetLeft(content, left);
        Canvas.SetTop(content, top);
        canvas.Children.Add(content);
        return canvas;
    }

    private UIElement BuildContent(
        WriteableBitmap bitmap,
        double availW,
        double availH,
        out double targetW,
        out double targetH)
    {
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
        };

        var contentW = (double)bitmap.PixelWidth;
        var contentH = (double)bitmap.PixelHeight;
        var coreMode = _scaleMode switch
        {
            DocumentPrintScaleMode.ActualSize => PrintScaleMode.ActualSize,
            DocumentPrintScaleMode.Fill => PrintScaleMode.Fill,
            _ => PrintScaleMode.Fit,
        };
        var layout = PrintSheetLayout.ComputeTarget(
            contentW, contentH, availW, availH, coreMode, _autoRotate);

        image.Stretch = coreMode switch
        {
            PrintScaleMode.ActualSize => Stretch.None,
            PrintScaleMode.Fill => Stretch.UniformToFill,
            _ => Stretch.Uniform,
        };
        image.Width = layout.ImageWidth;
        image.Height = layout.ImageHeight;

        UIElement content = image;
        if (layout.Rotate)
        {
            content = new Grid
            {
                Width = layout.OccupiedWidth,
                Height = layout.OccupiedHeight,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = 90 },
                Children = { image },
            };
        }

        targetW = layout.OccupiedWidth;
        targetH = layout.OccupiedHeight;
        return content;
    }

    private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        if (_printDocument is null || e.PageNumber < 1 || e.PageNumber > _previewPages.Count)
        {
            return;
        }

        _printDocument.SetPreviewPage(e.PageNumber, _previewPages[e.PageNumber - 1]);
    }

    private void OnAddPages(object sender, AddPagesEventArgs e)
    {
        if (_printDocument is null)
        {
            return;
        }

        foreach (var page in _previewPages)
        {
            _printDocument.AddPage(page);
        }

        _printDocument.AddPagesComplete();
    }

    private void Unsubscribe()
    {
        if (_subscribed && _printManager is not null)
        {
            _printManager.PrintTaskRequested -= OnPrintTaskRequested;
            _subscribed = false;
        }

        if (_printDocument is not null)
        {
            _printDocument.Paginate -= OnPaginate;
            _printDocument.GetPreviewPage -= OnGetPreviewPage;
            _printDocument.AddPages -= OnAddPages;
        }
    }

    public void Dispose()
    {
        Unsubscribe();
        _completion?.TrySetResult(false);
        _previewPages.Clear();
        _bitmaps.Clear();
    }

    /// <summary>Convert BGRA32 buffer to approximate luminance grayscale in place.</summary>
    public static void ApplyGrayscale(byte[] bgra) =>
        Glyph.Imaging.Abstractions.ImagePixelOps.ApplyGrayscale(bgra);

    public static async Task<WriteableBitmap> ToWriteableBitmapAsync(int width, int height, byte[] bgra)
    {
        var bitmap = new WriteableBitmap(width, height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            await stream.WriteAsync(bgra, 0, bgra.Length);
        }

        bitmap.Invalidate();
        return bitmap;
    }
}
