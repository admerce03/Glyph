namespace Glyph.Core.Pdf;

/// <summary>
/// Resolves clickable PDF link destinations (F05-08).
/// </summary>
public static class PdfLinkAction
{
    public enum Kind
    {
        None,
        GoToPage,
        OpenUri,
    }

    public static Kind Resolve(int? destinationPageIndex, string? uri)
    {
        if (destinationPageIndex is >= 0)
        {
            return Kind.GoToPage;
        }

        return string.IsNullOrWhiteSpace(uri) ? Kind.None : Kind.OpenUri;
    }
}

/// <summary>
/// Outline entry invoke → page navigation (F05-04).
/// </summary>
public static class OutlineNavigation
{
    public static bool TryGetPageIndex(int? destinationPageIndex, out int pageIndex)
    {
        if (destinationPageIndex is >= 0)
        {
            pageIndex = destinationPageIndex.Value;
            return true;
        }

        pageIndex = -1;
        return false;
    }
}
