namespace Glyph.Core.Documents;

/// <summary>
/// Pure helpers for thumbnail drag-reorder permutations.
/// </summary>
public static class PageReorder
{
    /// <summary>
    /// Moves the selected indexes as a block so the first selected page lands at
    /// <paramref name="insertBeforeIndex"/> in the pre-removal sequence.
    /// </summary>
    public static IReadOnlyList<int> MoveSelection(
        int pageCount,
        IReadOnlyList<int> selectedIndexes,
        int insertBeforeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        ArgumentNullException.ThrowIfNull(selectedIndexes);
        if (selectedIndexes.Count == 0)
        {
            return Enumerable.Range(0, pageCount).ToArray();
        }

        var selected = selectedIndexes.Distinct().OrderBy(i => i).ToList();
        if (selected.Any(i => i < 0 || i >= pageCount))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndexes));
        }

        insertBeforeIndex = Math.Clamp(insertBeforeIndex, 0, pageCount);

        var remaining = Enumerable.Range(0, pageCount).Where(i => !selected.Contains(i)).ToList();
        var removedBefore = selected.Count(i => i < insertBeforeIndex);
        var target = Math.Clamp(insertBeforeIndex - removedBefore, 0, remaining.Count);

        var result = new List<int>(pageCount);
        result.AddRange(remaining.Take(target));
        result.AddRange(selected);
        result.AddRange(remaining.Skip(target));
        return result;
    }
}
