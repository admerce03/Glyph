using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Glyph.App.Views;

/// <summary>
/// Minimal Milestone 2 PDF viewer: continuous page stack, zoom, page nav, thumbnails.
/// Pages are rendered on demand; distant bitmaps are not retained beyond the cache.
/// </summary>
public sealed class PdfDocumentView : UserControl
{
    private readonly IPdfDocument _document;
    private readonly IPdfRenderer _renderer;
    private readonly PageRenderCache _cache;
    private readonly string _documentKey;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _pageHost;
    private readonly ListView _thumbnailList;
    private readonly TextBlock _status;
    private readonly Dictionary<int, Image> _pageImages = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private double _scale = 1.25;
    private int _renderGeneration;
    private bool _loaded;

    public PdfDocumentView(IPdfDocument document, IPdfRenderer renderer, PageRenderCache cache)
    {
        _document = document;
        _renderer = renderer;
        _cache = cache;
        _documentKey = document.Path ?? document.GetHashCode().ToString("X");

        _pageHost = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
        _scrollViewer = new ScrollViewer
        {
            Content = _pageHost,
            ZoomMode = ZoomMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;

        _thumbnailList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Width = 140,
        };
        _thumbnailList.SelectionChanged += ThumbnailList_SelectionChanged;

        _status = new TextBlock { Opacity = 0.75, FontSize = 12, Margin = new Thickness(8, 0, 8, 0) };

        var zoomOut = new Button { Content = "−", Width = 36 };
        var zoomIn = new Button { Content = "+", Width = 36 };
        var fitWidth = new Button { Content = "Fit width" };
        var prev = new Button { Content = "Prev" };
        var next = new Button { Content = "Next" };
        zoomOut.Click += async (_, _) => await SetScaleAsync(_scale / 1.25);
        zoomIn.Click += async (_, _) => await SetScaleAsync(_scale * 1.25);
        fitWidth.Click += async (_, _) => await FitWidthAsync();
        prev.Click += async (_, _) => await GoToPageAsync(CurrentPageIndex - 1);
        next.Click += async (_, _) => await GoToPageAsync(CurrentPageIndex + 1);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Padding = new Thickness(8),
            Children = { prev, next, zoomOut, zoomIn, fitWidth, _status },
        };

        var body = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(150) }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } } };
        body.Children.Add(_thumbnailList);
        Grid.SetColumn(_scrollViewer, 1);
        body.Children.Add(_scrollViewer);

        var root = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } } };
        root.Children.Add(toolbar);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Content = root;

        Loaded += PdfDocumentView_Loaded;
        Unloaded += PdfDocumentView_Unloaded;
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
        PopulateThumbnailLabels();
        UpdateStatus();
        await RenderVisibleAsync();
    }

    private void PdfDocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        _cache.ClearDocument(_documentKey);
    }

    private void BuildPagePlaceholders()
    {
        _pageHost.Children.Clear();
        _pageImages.Clear();

        for (var i = 0; i < _document.PageCount; i++)
        {
            var page = _document.GetPage(i);
            var width = Math.Max(1, page.WidthPoints * _scale);
            var height = Math.Max(1, page.HeightPoints * _scale);
            var image = new Image
            {
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
            };
            _pageImages[i] = image;

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Child = image,
                Tag = i,
            };
            _pageHost.Children.Add(border);
        }
    }

    private void PopulateThumbnailLabels()
    {
        _thumbnailList.Items.Clear();
        for (var i = 0; i < _document.PageCount; i++)
        {
            _thumbnailList.Items.Add($"Page {i + 1}");
        }
    }

    private async Task SetScaleAsync(double scale)
    {
        _scale = Math.Clamp(scale, 0.25, 4.0);
        _cache.ClearDocument(_documentKey);
        BuildPagePlaceholders();
        UpdateStatus();
        await RenderVisibleAsync();
    }

    private async Task FitWidthAsync()
    {
        if (_document.PageCount == 0)
        {
            return;
        }

        var viewportWidth = Math.Max(100, _scrollViewer.ViewportWidth - 24);
        var pageWidth = _document.GetPage(CurrentPageIndex).WidthPoints;
        await SetScaleAsync(viewportWidth / pageWidth);
    }

    private async Task GoToPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _document.PageCount)
        {
            return;
        }

        CurrentPageIndex = pageIndex;
        _thumbnailList.SelectedIndex = pageIndex;
        if (_pageHost.Children[pageIndex] is FrameworkElement element)
        {
            element.StartBringIntoView();
        }

        UpdateStatus();
        await RenderVisibleAsync();
    }

    private async void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_thumbnailList.SelectedIndex >= 0)
        {
            await GoToPageAsync(_thumbnailList.SelectedIndex);
        }
    }

    private async void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (!e.IsIntermediate)
        {
            UpdateCurrentPageFromScroll();
            await RenderVisibleAsync();
        }
    }

    private void UpdateCurrentPageFromScroll()
    {
        var offset = _scrollViewer.VerticalOffset;
        double accumulated = 0;
        for (var i = 0; i < _pageHost.Children.Count; i++)
        {
            if (_pageHost.Children[i] is FrameworkElement fe)
            {
                var next = accumulated + fe.ActualHeight + 12;
                if (offset < next || i == _pageHost.Children.Count - 1)
                {
                    CurrentPageIndex = i;
                    if (_thumbnailList.SelectedIndex != i)
                    {
                        _thumbnailList.SelectedIndex = i;
                    }

                    UpdateStatus();
                    return;
                }

                accumulated = next;
            }
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

            var first = Math.Max(0, CurrentPageIndex - 1);
            var last = Math.Min(_document.PageCount - 1, CurrentPageIndex + 2);

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

    private void UpdateStatus()
    {
        _status.Text = $"Page {CurrentPageIndex + 1} / {_document.PageCount}    Zoom {(int)Math.Round(_scale * 100)}%";
    }
}
