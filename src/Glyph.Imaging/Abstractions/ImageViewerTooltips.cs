namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Image viewer toolbar / chrome tooltips.
/// </summary>
public static class ImageViewerTooltips
{
    public const string CropAspectFreeOriginalImageRatio = "Crop aspect: free, original image ratio, or common presets";
    public const string PixelSelectionDragOnImageDrag = "Pixel selection (drag on image; drag inside to move; arrow keys nudge)";
    public const string SelectionShapeRectangleEllipseFreeformLasso = "Selection shape: rectangle, ellipse, freeform lasso, or smart (edge-snapping) lasso";
    public const string SelectEntireImage = "Select entire image";
    public const string InvertSelectionOperationsApplyToOutside = "Invert selection (operations apply to outside)";
    public const string ClearSelection = "Clear selection";
    public const string CopySelectionToClipboardAsPng = "Copy selection to clipboard as PNG";
    public const string CutSelectionCopyClear = "Cut selection (copy + clear)";
    public const string PasteAtSelectionTopLeftOr = "Paste at selection top-left (or 0,0)";
    public const string ClearSelectionToTransparent = "Clear selection to transparent";
    public const string CropImageToSelection = "Crop image to selection";
    public const string FreehandMarkupNonDestructiveOverlayUntil = "Freehand markup (non-destructive overlay until Flatten/Save)";
    public const string BakeMarkupStrokesIntoPixels = "Bake markup strokes into pixels";
    public const string CropUsingXYWH = "Crop using x,y,w,h pixels (origin top-left)";
    public const string DragARectangleOnTheImage = "Drag a rectangle on the image to crop";
    public const string ApplyTheDraggedCropRectangle = "Apply the dragged crop rectangle";
    public const string CancelInteractiveCrop = "Cancel interactive crop";
    public const string ResizeWidthHeightWithOptionalAspect = "Resize width/height with optional aspect lock";
    public const string BrightnessContrastSaturationLevels = "Brightness / contrast / saturation / levels";
    public const string RemoveSolidBackgroundExtractSubjectCorner = "Remove solid background / extract subject (corner flood-fill)";
    public const string StampASignatureFromTheLibrary = "Stamp a signature from the library onto the image";
    public const string ImageMetadataExifIptcXmpAnd = "Image metadata, EXIF/IPTC/XMP, and GPS";
    public const string RunOfflineOcrOnThisImage = "Run offline OCR and select text over the image";
    public const string CopySelectedOcrWordsOrAll = "Copy selected OCR words (or all recognized text)";
    public const string ReviewDetectedOcrEntities = "Review detected URLs, emails, phones, addresses, dates, and times";
    public const string HideOcrWordOverlays = "Hide OCR word overlays";
    public const string SearchRecognizedOcrText = "Search recognized OCR text";
    public const string HighlightOcrWordsMatchingQuery = "Highlight OCR words matching the query";
    public const string JumpToNextOcrSearchHit = "Jump to next OCR search hit";
    public const string CancelInFlightOcrJob = "Cancel the in-flight OCR job";
    public const string SearchWebForSelectedOcrText = "Search the web for selected OCR text";
    public const string ExportSearchableOcrPdf = "Export a searchable PDF with this image and an invisible OCR text layer";
    public const string Rotate180 = "Rotate 180°";
    public const string ApplyExifOrientationIntoPixels = "Apply EXIF orientation into pixels";
    public const string DeskewStraightenScannedPageMagick = "Deskew / straighten scanned page (Magick)";
    public const string BatchFolderRotateFlipOrientConvert = "Batch folder: rotate/flip/orient, convert/export, or strip metadata";
    public const string ToggleWindowFullscreen = "Toggle window fullscreen";
    public const string ExportAsPng = "Export as PNG";
    public const string ExportAsJpeg = "Export as JPEG";
    public const string ExportAsWebpTiffBmpGif = "Export as WebP, TIFF, BMP, GIF, AVIF, JP2, or HEIC";
    public const string PrintThisImageCtrlP = "Print this image (Ctrl+P)";
    public const string CopyWholeImageToClipboardCtrl = "Copy whole image to clipboard (Ctrl+C; selection copies when active)";
    public const string PasteImageFromClipboardCtrlV = "Paste image from clipboard (Ctrl+V)";
    public const string PreviousImageInFolder = "Previous image in folder";
    public const string NextImageInFolder = "Next image in folder";
    public const string PlayStopFolderSlideshow3sLoops = "Play/stop folder slideshow (3s, loops; Esc stops)";
    public const string PlayPauseAnimatedFramesGifWebp = "Play/pause animated frames (GIF/WebP)";
    public const string PreviousAnimationFrame = "Previous animation frame";
    public const string NextAnimationFrame = "Next animation frame";
    public const string RestartAnimationFromFirstFrame = "Restart animation from first frame";
    public const string SaveCurrentFrameAsPng = "Save current frame as PNG";
    public const string LoopAnimationPlayback = "Loop animation playback";
    public const string UndoLastCropResizeRotateAdjust = "Undo last crop/resize/rotate/adjust (Ctrl+Z)";
    public const string ZoomOut = "Zoom out";
    public const string ZoomIn = "Zoom in";
    public const string FitImageInView = "Fit image in view";
    public const string ZoomTo100 = "Zoom to 100%";
    public const string RotateLeft90 = "Rotate left 90°";
    public const string RotateRight90 = "Rotate right 90°";
    public const string FlipHorizontal = "Flip horizontal";
    public const string FlipVertical = "Flip vertical";
    public const string SaveImage = "Save image";
    public const string AppliesScaleToEveryImageIn = "Applies Scale % to every image in this folder (overwrites files on disk). Current image is resized in memory until Save.";
    public const string ResetThisAdjustment = "Reset this adjustment";
}
