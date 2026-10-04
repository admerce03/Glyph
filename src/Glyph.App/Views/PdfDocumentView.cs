using System.Runtime.InteropServices.WindowsRuntime;
using Glyph.Pdf.Abstractions;
using Glyph.Pdf.Rendering;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Glyph.App.Views;

/// <summary>
/// Milestone 2 PDF viewer: continuous page stack, zoom, page nav, bitmap thumbnails.
/// Pages are rendered on demand; distant bitmaps are not retained beyond the cache.
/// </summary>
public sealed class PdfDocumentView : UserControl
{
    private const double ThumbnailWidth = 108;

    private readonly IPdfDocument _document;
    private readonly IPdfRenderer _renderer;
    private readonly PageRenderCache _cache;
    private readonly string _documentKey;
    private readonly string _thumbnailKey;
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _pageHost;
    private readonly StackPanel _thumbnailHost;
    private readonly ScrollViewer _thumbnailScroll;
    private readonly TextBlock _status;
    private readonly Dictionary<int, Image> _pageImages = new();
    private readonly Dictionary<int, Image> _thumbnailImages = new();
    private readonly Dictionary<int, Border> _thumbnailBorders = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private double _scale = 1.25;
    private int _renderGeneration;
    private bool _loaded;
    private bool _suppressThumbnailNav;

    public PdfDocumentView(IPdfDocument document, IPdfRenderer renderer, PageRenderCache cache)
    {
        _document = document;
        _renderer = renderer;
        _cache = cache;
        _documentKey = document.Path ?? document.GetHashCode().ToString("X");
        _thumbnailKey = _documentKey + "|thumb";

        _pageHost = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
        _scrollViewer = new ScrollViewer
        {
            Content = _pageHost,
            ZoomMode = ZoomMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;

        _thumbnailHost = new StackPanel { Spacing = 8, Padding = new Thickness(8) };
        _thumbnailScroll = new ScrollViewer
        {
            Content = _thumbnailHost,
            Width = 140,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

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

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(150) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        body.Children.Add(_thumbnailScroll);
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
        BuildThumbnailPlaceholders();
        UpdateStatus();
        HighlightThumbnail(CurrentPageIndex);
        await RenderVisibleAsync();
        _ = RenderThumbnailsAsync();
    }

    private void PdfDocumentView_Unloaded(object sender, RoutedEventArgs e)
    {
        _cache.ClearDocument(_documentKey);
        _cache.ClearDocument(_thumbnailKey);
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
            };
            border.PointerPressed += Thumbnail_PointerPressed;
            _thumbnailBorders[i] = border;
            _thumbnailHost.Children.Add(border);
        }
    }

    private async void Thumbnail_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border { Tag: int index })
        {
            await GoToPageAsync(index);
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
        HighlightThumbnail(pageIndex);
        if (_pageHost.Children[pageIndex] is FrameworkElement element)
        {
            element.StartBringIntoView();
        }

        UpdateStatus();
        await RenderVisibleAsync();
    }

    private void HighlightThumbnail(int pageIndex)
    {
        _suppressThumbnailNav = true;
        try
        {
            foreach (var (index, border) in _thumbnailBorders)
            {
                border.BorderBrush = new SolidColorBrush(index == pageIndex ? Colors.DodgerBlue : Colors.Transparent);
            }

            if (_thumbnailBorders.TryGetValue(pageIndex, out var selected))
            {
                selected.StartBringIntoView();
            }
        }
        finally
        {
            _suppressThumbnailNav = false;
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
        if (_suppressThumbnailNav)
        {
            return;
        }

        var offset = _scrollViewer.VerticalOffset;
        double accumulated = 0;
        for (var i = 0; i < _pageHost.Children.Count; i++)
        {
            if (_pageHost.Children[i] is FrameworkElement fe)
            {
                var next = accumulated + fe.ActualHeight + 12;
                if (offset < next || i == _pageHost.Children.Count - 1)
                {
                    if (CurrentPageIndex != i)
                    {
                        CurrentPageIndex = i;
                        HighlightThumbnail(i);
                        UpdateStatus();
                    }

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

    private void UpdateStatus()
    {
        _status.Text = $"Page {CurrentPageIndex + 1} / {_document.PageCount}    Zoom {(int)Math.Round(_scale * 100)}%";
    }
}
