namespace Glyph.Infrastructure.Settings;

/// <summary>
/// Toolbar command order helpers (F54 add/remove/reorder).
/// Empty persisted order means catalog default.
/// </summary>
public static class ToolbarOrderPolicy
{
    /// <summary>
    /// Full catalog order: known ids from <paramref name="order"/> first (deduped),
    /// then any catalog ids not listed.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? order)
    {
        var catalogIds = ToolbarCommands.Catalog.Select(c => c.Id).ToList();
        var known = new HashSet<string>(catalogIds, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(catalogIds.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (order is not null)
        {
            foreach (var raw in order)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var id = raw.Trim().ToLowerInvariant();
                if (!known.Contains(id) || !seen.Add(id))
                {
                    continue;
                }

                result.Add(id);
            }
        }

        foreach (var id in catalogIds)
        {
            if (seen.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Moves <paramref name="id"/> by <paramref name="delta"/> steps (−1 up, +1 down).
    /// Returns a new normalized order list.
    /// </summary>
    public static IReadOnlyList<string> Move(IEnumerable<string>? order, string id, int delta)
    {
        var list = Normalize(order).ToList();
        if (string.IsNullOrWhiteSpace(id) || delta == 0)
        {
            return list;
        }

        var key = id.Trim().ToLowerInvariant();
        var index = list.FindIndex(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return list;
        }

        var target = Math.Clamp(index + delta, 0, list.Count - 1);
        if (target == index)
        {
            return list;
        }

        list.RemoveAt(index);
        list.Insert(target, key);
        return list;
    }

    /// <summary>
    /// True when <paramref name="order"/> matches the catalog default sequence.
    /// </summary>
    public static bool IsDefault(IEnumerable<string>? order)
    {
        var normalized = Normalize(order);
        var catalog = ToolbarCommands.Catalog.Select(c => c.Id).ToList();
        if (normalized.Count != catalog.Count)
        {
            return false;
        }

        for (var i = 0; i < catalog.Count; i++)
        {
            if (!string.Equals(normalized[i], catalog[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Empty persisted list is also "default".
        if (order is null)
        {
            return true;
        }

        var persisted = order.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return persisted.Count == 0
            || persisted.Select(x => x.Trim().ToLowerInvariant())
                .SequenceEqual(catalog, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reorders tagged items into free slots (untagged keep absolute indices).
    /// <paramref name="getTag"/> returns the command id for an item, or null if untagged.
    /// </summary>
    public static IReadOnlyList<T> ApplyOrderToItems<T>(
        IReadOnlyList<T> items,
        Func<T, string?> getTag,
        IEnumerable<string>? order)
    {
        var normalized = Normalize(order);
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < normalized.Count; i++)
        {
            rank[normalized[i]] = i;
        }

        var result = new T[items.Count];
        var freeSlots = new List<int>();
        var tagged = new List<(T Item, int Rank, int OriginalIndex)>();

        for (var i = 0; i < items.Count; i++)
        {
            var tag = getTag(items[i]);
            if (string.IsNullOrWhiteSpace(tag))
            {
                result[i] = items[i];
                continue;
            }

            freeSlots.Add(i);
            var key = tag.Trim().ToLowerInvariant();
            var r = rank.TryGetValue(key, out var value) ? value : int.MaxValue;
            tagged.Add((items[i], r, i));
        }

        tagged.Sort((a, b) =>
        {
            var cmp = a.Rank.CompareTo(b.Rank);
            return cmp != 0 ? cmp : a.OriginalIndex.CompareTo(b.OriginalIndex);
        });

        for (var i = 0; i < freeSlots.Count; i++)
        {
            result[freeSlots[i]] = tagged[i].Item;
        }

        return result;
    }
}
