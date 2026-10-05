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
    public const bool InsertsIntoPdfAsStamp = true;
    public const bool OpensAsImageTab = true;
}
