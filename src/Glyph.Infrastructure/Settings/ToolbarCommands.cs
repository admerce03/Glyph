namespace Glyph.Infrastructure.Settings;

/// <summary>
/// Stable ids for Preferences → toolbar customization (F54).
/// </summary>
public static class ToolbarCommands
{
    public const string Sidebar = "sidebar";
    public const string Previous = "previous";
    public const string Next = "next";
    public const string PageNumber = "page-number";
    public const string Zoom = "zoom";
    public const string FitPage = "fit-page";
    public const string FitWidth = "fit-width";
    public const string Search = "search";
    public const string Markup = "markup";
    public const string Highlight = "highlight";
    public const string Rotate = "rotate";
    public const string Crop = "crop";
    public const string Signature = "signature";
    public const string Print = "print";
    public const string Inspector = "inspector";
    public const string Share = "share";
    public const string Ocr = "ocr";

    /// <summary>User-facing catalog for Preferences checkboxes (show = not hidden).</summary>
    public static IReadOnlyList<(string Id, string Label)> Catalog { get; } =
    [
        (Sidebar, "Sidebar"),
        (Previous, "Previous"),
        (Next, "Next"),
        (PageNumber, "Page number"),
        (Zoom, "Zoom"),
        (FitPage, "Fit page"),
        (FitWidth, "Fit width"),
        (Search, "Search"),
        (Markup, "Markup / draw"),
        (Highlight, "Highlight"),
        (Rotate, "Rotate"),
        (Crop, "Crop"),
        (Signature, "Signature"),
        (Print, "Print"),
        (Inspector, "Inspector"),
        (Share, "Share"),
        (Ocr, "OCR"),
    ];
}
