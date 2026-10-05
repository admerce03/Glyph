namespace Glyph.Core.Ocr;

/// <summary>
/// Image folder OCR scope chooser (F08-09).
/// </summary>
public static class ImageOcrFolderChooser
{
    public const string Title = "OCR";
    public const string PrimaryButton = "This image";
    public const string Cancelled = "OCR cancelled.";

    public static string Prompt(int folderCount) =>
        $"OCR this image, or all {folderCount} images in the folder?";

    public static string SecondaryButton(int folderCount) => $"Folder ({folderCount})";

    public static bool ShouldOfferFolder(int siblingCount) => siblingCount > 1;
}
