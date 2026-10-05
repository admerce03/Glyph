using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glyph.App.Sharing;

/// <summary>
/// Windows Share sheet + filesystem helpers (F46 / F47).
/// Keep an instance alive while the share UI is open so DataRequested stays wired.
/// </summary>
public sealed class DocumentShareHelper : IDisposable
{
    private readonly nint _hwnd;
    private DataTransferManager? _manager;
    private string? _title;
    private string? _filePath;
    private bool _subscribed;

    public DocumentShareHelper(nint hwnd)
    {
        _hwnd = hwnd;
    }

    public void ShowShareUi(string? title, string? filePath)
    {
        _title = title;
        _filePath = filePath;
        _manager ??= DataTransferManagerInterop.GetForWindow(_hwnd);
        if (!_subscribed)
        {
            _manager.DataRequested += OnDataRequested;
            _subscribed = true;
        }

        DataTransferManagerInterop.ShowShareUIForWindow(_hwnd);
    }

    private async void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        var request = args.Request;
        request.Data.Properties.Title = string.IsNullOrWhiteSpace(_title) ? "Glyph" : _title!;
        if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath))
        {
            request.Data.SetText(_title ?? "Glyph document");
            return;
        }

        var deferral = request.GetDeferral();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(_filePath);
            request.Data.SetStorageItems(new IStorageItem[] { file });
        }
        catch (Exception ex)
        {
            request.FailWithDisplayText("Could not share file: " + ex.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }

    public static async Task OpenContainingFolderAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var parent = await file.GetParentAsync();
            if (parent is not null)
            {
                await Windows.System.Launcher.LaunchFolderAsync(parent);
                return;
            }
        }
        catch
        {
            // Fall through.
        }

        var folder = System.IO.Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("No containing folder.");
        await Windows.System.Launcher.LaunchFolderPathAsync(folder);
    }

    public static void CopyPathToClipboard(string path)
    {
        var package = new DataPackage();
        package.SetText(path);
        Clipboard.SetContent(package);
    }

    public static async Task CopyFileToClipboardAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var package = new DataPackage();
        package.SetStorageItems(new IStorageItem[] { file });
        Clipboard.SetContent(package);
    }

    public static async Task OpenWithDefaultAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        await Windows.System.Launcher.LaunchFileAsync(file);
    }

    public static async Task SendMailtoAsync(string? subject, string? body, string? attachmentPath = null)
    {
        var sub = Uri.EscapeDataString(subject ?? "Glyph document");
        var note = string.IsNullOrWhiteSpace(attachmentPath)
            ? body ?? string.Empty
            : (body ?? string.Empty) + "\n\nAttachment: " + attachmentPath;
        var bod = Uri.EscapeDataString(note);
        await Windows.System.Launcher.LaunchUriAsync(new Uri($"mailto:?subject={sub}&body={bod}"));
    }

    public void Dispose()
    {
        if (_subscribed && _manager is not null)
        {
            _manager.DataRequested -= OnDataRequested;
            _subscribed = false;
        }
    }
}
