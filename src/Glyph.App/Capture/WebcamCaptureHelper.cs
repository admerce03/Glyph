using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Glyph.App.Capture;

public sealed record WebcamCaptureResult(byte[] BgraPixels, int Width, int Height);

/// <summary>
/// Shared webcam photo capture for document import and signatures (F43).
/// </summary>
public static class WebcamCaptureHelper
{
    public static async Task<WebcamCaptureResult?> CaptureAsync(
        XamlRoot xamlRoot,
        string title = "Camera",
        string? hint = null)
    {
        MediaCapture? capture = null;
        try
        {
            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.Auto,
            });
        }
        catch
        {
            capture?.Dispose();
            return null;
        }

        try
        {
            var preview = new Border
            {
                Width = 480,
                Height = 320,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                Child = new TextBlock
                {
                    Text = "Camera ready — capture a frame",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            await capture.StartPreviewAsync();

            var dialog = new ContentDialog
            {
                Title = title,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = hint ?? "Frame the document or subject, then Capture.",
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 480,
                        },
                        preview,
                    },
                },
                PrimaryButtonText = "Capture",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
            };

            var result = await dialog.ShowAsync();
            try
            {
                await capture.StopPreviewAsync();
            }
            catch
            {
                // ignore
            }

            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            using var stream = new InMemoryRandomAccessStream();
            await capture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
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
                return null;
            }

            return new WebcamCaptureResult(pixels, width, height);
        }
        finally
        {
            try
            {
                if (capture is not null)
                {
                    await capture.StopPreviewAsync();
                }
            }
            catch
            {
                // ignore
            }

            capture?.Dispose();
        }
    }
}
