namespace Glyph.Core.Documents;

/// <summary>
/// PDF sidebar mode ComboBox labels and indexes (F03-10).
/// Order matches the visible section list in PdfDocumentView.
/// </summary>
public static class SidebarModeCombo
{
    public const int PagesIndex = 0;
    public const int ContentsIndex = 1;
    public const int BookmarksIndex = 2;
    public const int SearchIndex = 3;
    public const int AnnotationsIndex = 4;
    public const int PropertiesIndex = 5;
    public const int AttachmentsIndex = 6;
    public const string ToggleUnavailable = "Sidebar toggle unavailable.";

    public static readonly IReadOnlyList<string> Labels =
    [
        "Pages",
        "Contents",
        "Bookmarks",
        "Search",
        "Annotations",
        "Properties",
        "Attachments",
    ];

    public static int ClampIndex(int selectedIndex) =>
        Math.Clamp(selectedIndex, 0, Labels.Count - 1);

    public static bool IsValidIndex(int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < Labels.Count;

    /// <summary>Maps combo index to the closest <see cref="SidebarMode"/> value.</summary>
    public static SidebarMode ToSidebarMode(int selectedIndex) => ClampIndex(selectedIndex) switch
    {
        ContentsIndex => SidebarMode.TableOfContents,
        BookmarksIndex => SidebarMode.Bookmarks,
        SearchIndex => SidebarMode.SearchResults,
        AnnotationsIndex => SidebarMode.Annotations,
        PropertiesIndex => SidebarMode.Properties,
        AttachmentsIndex => SidebarMode.Attachments,
        _ => SidebarMode.Thumbnails,
    };

    public static int FromSidebarMode(SidebarMode mode) => mode switch
    {
        SidebarMode.TableOfContents => ContentsIndex,
        SidebarMode.Bookmarks => BookmarksIndex,
        SidebarMode.SearchResults => SearchIndex,
        SidebarMode.Annotations => AnnotationsIndex,
        SidebarMode.Properties => PropertiesIndex,
        SidebarMode.Attachments => AttachmentsIndex,
        SidebarMode.ContactSheet => PagesIndex, // contact sheet is layout, not sidebar combo
        SidebarMode.Images => PagesIndex,
        _ => PagesIndex,
    };
}
