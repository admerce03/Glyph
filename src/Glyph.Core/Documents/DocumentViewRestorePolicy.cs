namespace Glyph.Core.Documents;

/// <summary>
/// Applies per-document view restore under F55-07 (remember last page) and F55-08 (remember zoom).
/// </summary>
public static class DocumentViewRestorePolicy
{
    public const double FallbackDefaultZoom = 1.25;

    /// <summary>Effective F55-07: missing/null prefs mean remember (default ON).</summary>
    public static bool EffectiveRememberLastPage(bool? rememberLastPage) =>
        rememberLastPage ?? true;

    /// <summary>Effective F55-08: missing/null prefs mean remember (default ON).</summary>
    public static bool EffectiveRememberZoom(bool? rememberZoom) =>
        rememberZoom ?? true;

    public static double ResolveDefaultZoom(double defaultZoom) =>
        defaultZoom > 0 ? defaultZoom : FallbackDefaultZoom;

    public static PageLayoutMode ResolveDefaultPageLayout(string? layoutName) =>
        layoutName switch
        {
            "Single" => PageLayoutMode.SinglePage,
            "TwoPage" => PageLayoutMode.TwoPage,
            "TwoPageWithCover" => PageLayoutMode.TwoPageWithCover,
            _ => PageLayoutMode.Continuous,
        };

    /// <summary>
    /// Applies saved PDF view state gated by remember prefs. When <paramref name="saved"/> is null,
    /// applies default zoom/layout only.
    /// </summary>
    public static void ApplyPdf(
        DocumentViewState target,
        DocumentViewState? saved,
        int pageCount,
        bool rememberLastPage,
        bool rememberZoom,
        double defaultZoom,
        PageLayoutMode defaultLayout)
    {
        ArgumentNullException.ThrowIfNull(target);
        var zoom = ResolveDefaultZoom(defaultZoom);

        if (saved is null)
        {
            target.Zoom = zoom;
            target.PageLayout = defaultLayout;
            target.CurrentPageIndex = 0;
            return;
        }

        target.PageLayout = saved.PageLayout;
        target.Zoom = rememberZoom ? saved.Zoom : zoom;
        target.CurrentPageIndex = rememberLastPage
            ? Math.Clamp(saved.CurrentPageIndex, 0, Math.Max(0, pageCount - 1))
            : 0;
    }

    /// <summary>
    /// Applies saved image zoom when remember-zoom is on; otherwise leaves target zoom unchanged.
    /// </summary>
    public static void ApplyImage(
        DocumentViewState target,
        DocumentViewState? saved,
        bool rememberZoom)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (rememberZoom && saved is not null && saved.Zoom > 0)
        {
            target.Zoom = saved.Zoom;
        }
    }
}
