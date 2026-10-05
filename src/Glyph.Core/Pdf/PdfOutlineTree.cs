namespace Glyph.Core.Pdf;

/// <summary>
/// Hierarchical PDF outline helpers (F05-02 / F03-02).
/// </summary>
public static class PdfOutlineTree
{
    public static int CountNodes(IEnumerable<OutlineNodeLike> roots) =>
        roots.Sum(CountSubtree);

    public static IReadOnlyList<OutlineNodeLike> Flatten(IEnumerable<OutlineNodeLike> roots)
    {
        var list = new List<OutlineNodeLike>();
        foreach (var root in roots)
        {
            AppendFlattened(root, list);
        }

        return list;
    }

    public static bool HasNavigableDestination(OutlineNodeLike node) =>
        node.DestinationPageIndex is >= 0;

    private static int CountSubtree(OutlineNodeLike node) =>
        1 + node.Children.Sum(CountSubtree);

    private static void AppendFlattened(OutlineNodeLike node, List<OutlineNodeLike> list)
    {
        list.Add(node);
        foreach (var child in node.Children)
        {
            AppendFlattened(child, list);
        }
    }
}

/// <summary>
/// Core-local outline shape so PdfOutlineTree does not reference Glyph.Pdf.
/// </summary>
public sealed record OutlineNodeLike(
    string Title,
    int? DestinationPageIndex,
    IReadOnlyList<OutlineNodeLike> Children);
