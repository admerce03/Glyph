namespace Glyph.Core.Documents;

/// <summary>
/// Per-document UI state that should survive tab switches.
/// </summary>
public sealed class DocumentViewState
{
    public double Zoom { get; set; } = 1.0;

    public PageLayoutMode PageLayout { get; set; } = PageLayoutMode.Continuous;

    public int CurrentPageIndex { get; set; }

    public SidebarMode SidebarMode { get; set; } = SidebarMode.Thumbnails;

    public bool IsSidebarVisible { get; set; } = true;
}
