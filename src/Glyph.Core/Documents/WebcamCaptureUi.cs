namespace Glyph.Core.Documents;

/// <summary>
/// Webcam / camera import labels (F43).
/// </summary>
public static class WebcamCaptureUi
{
    public const string CaptureButton = "Camera";
    public const string DefaultDeviceNote = "Uses the default MediaCapture video device.";
    public const string CropAfterCaptureHint = "Crop… available after capture in the image view.";
    public const string StartingCamera = "Starting camera…";
    public const string StartingWebcam = "Starting webcam…";
    public const string CaptureCancelledOrUnavailable =
        "Camera capture cancelled or unavailable.";
    public const string CameraUiUnavailable = "Camera UI unavailable.";
    public const string CaptureDiscarded = "Camera capture discarded.";
    public const string CaptureDialogTitle = "Camera capture";
    public const string CaptureDialogBody =
        "Open the photo now. Use Crop… in the image toolbar if you want to trim it.";
    public const string OpenNowButton = "Open";
    public const string DiscardButton = "Discard";
    public const string CaptureFailedPrefix = "Camera capture failed: ";
    public const bool InsertsIntoPdfAsStamp = true;
    public const bool OpensAsImageTab = true;

    public static string FormatOpenedCapture(string fileName) =>
        $"Opened camera capture: {fileName}";

    public static string FormatCaptureFailed(string message) =>
        CaptureFailedPrefix + message;
}
