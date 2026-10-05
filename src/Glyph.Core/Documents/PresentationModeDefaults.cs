namespace Glyph.Core.Documents;

/// <summary>
/// Defaults for PDF presentation / slideshow mode (F04-31).
/// </summary>
public static class PresentationModeDefaults
{
    /// <summary>Auto-advance interval while presenting.</summary>
    public static readonly TimeSpan AutoAdvanceInterval = TimeSpan.FromSeconds(8);

    public static string StatusMessage =>
        "Presentation — ←/→ or Page keys; auto-advance 8s; Esc to exit.";
}
