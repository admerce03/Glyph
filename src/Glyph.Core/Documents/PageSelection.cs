namespace Glyph.Core.Documents;

/// <summary>
/// Thumbnail / page multi-selection with Ctrl (toggle) and Shift (range) semantics.
/// </summary>
public sealed class PageSelection
{
    private readonly SortedSet<int> _selected = [];
    private int? _anchor;

    public IReadOnlyCollection<int> SelectedIndexes => _selected;

    public int Count => _selected.Count;

    public bool Contains(int pageIndex) => _selected.Contains(pageIndex);

    public void Clear()
    {
        _selected.Clear();
        _anchor = null;
    }

    public void SelectOnly(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        _selected.Clear();
        _selected.Add(pageIndex);
        _anchor = pageIndex;
    }

    public void Toggle(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        if (!_selected.Add(pageIndex))
        {
            _selected.Remove(pageIndex);
        }

        _anchor = pageIndex;
    }

    public void SelectRange(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        var start = _anchor ?? pageIndex;
        var low = Math.Min(start, pageIndex);
        var high = Math.Max(start, pageIndex);
        _selected.Clear();
        for (var i = low; i <= high; i++)
        {
            _selected.Add(i);
        }
    }

    public void SelectAll(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        _selected.Clear();
        for (var i = 0; i < pageCount; i++)
        {
            _selected.Add(i);
        }

        _anchor = pageCount > 0 ? 0 : null;
    }

    /// <summary>
    /// Keyboard navigation: move focus/selection to <paramref name="pageIndex"/>,
    /// optionally extending a shift-range from the anchor.
    /// </summary>
    public void ApplyKeyboardMove(int pageIndex, bool extendRange)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        if (extendRange)
        {
            SelectRange(pageIndex);
        }
        else
        {
            SelectOnly(pageIndex);
        }
    }

    /// <summary>
    /// Applies a click with optional modifiers. Returns true when the selection changed.
    /// </summary>
    public bool ApplyClick(int pageIndex, bool ctrlOrMeta, bool shift)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        var before = _selected.ToArray();

        if (shift)
        {
            SelectRange(pageIndex);
        }
        else if (ctrlOrMeta)
        {
            Toggle(pageIndex);
        }
        else
        {
            SelectOnly(pageIndex);
        }

        return !before.SequenceEqual(_selected);
    }

    /// <summary>
    /// Editing scope: selected page indexes when any are selected, otherwise the current page.
    /// </summary>
    public IReadOnlyList<int> SelectedOrFallback(int currentPageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentPageIndex);
        if (_selected.Count > 0)
        {
            return _selected.OrderBy(i => i).ToList();
        }

        return [currentPageIndex];
    }

    /// <summary>
    /// Crop/print-style target resolution: all pages, else selection-or-current.
    /// </summary>
    public static IReadOnlyList<int> ResolveTargets(
        bool allPages,
        PageSelection selection,
        int pageCount,
        int currentPageIndex)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        if (allPages)
        {
            return pageCount == 0 ? [] : Enumerable.Range(0, pageCount).ToList();
        }

        return selection.SelectedOrFallback(currentPageIndex);
    }
}
