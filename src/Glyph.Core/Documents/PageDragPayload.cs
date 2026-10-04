namespace Glyph.Core.Documents;

/// <summary>
/// In-app drag payload for PDF page thumbnails (reorder within a document or
/// copy/move insert across documents/tabs). Format is stable for WinUI DataPackage text.
/// </summary>
public sealed record PageDragPayload(string DocumentKey, IReadOnlyList<int> PageIndexes)
{
    public const string Prefix = "glyph-pages:v1:";

    /// <summary>Legacy same-document reorder prefix still accepted on drop.</summary>
    public const string LegacyReorderPrefix = "glyph-page-reorder:";

    public string Format()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DocumentKey);
        ArgumentNullException.ThrowIfNull(PageIndexes);
        var pages = string.Join(',', PageIndexes.Distinct().OrderBy(i => i));
        return Prefix + DocumentKey + "|" + pages;
    }

    public static bool TryParse(string? text, out PageDragPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var body = text[Prefix.Length..];
            var sep = body.IndexOf('|');
            if (sep <= 0)
            {
                return false;
            }

            var key = body[..sep];
            var pagesPart = body[(sep + 1)..];
            if (!TryParseIndexes(pagesPart, out var indexes) || indexes.Count == 0)
            {
                return false;
            }

            payload = new PageDragPayload(key, indexes);
            return true;
        }

        if (text.StartsWith(LegacyReorderPrefix, StringComparison.Ordinal))
        {
            if (!TryParseIndexes(text[LegacyReorderPrefix.Length..], out var indexes) || indexes.Count == 0)
            {
                return false;
            }

            // Empty document key signals "same document as drop target" for legacy payloads.
            payload = new PageDragPayload(string.Empty, indexes);
            return true;
        }

        return false;
    }

    private static bool TryParseIndexes(string csv, out List<int> indexes)
    {
        indexes = [];
        if (string.IsNullOrWhiteSpace(csv))
        {
            return false;
        }

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var index) || index < 0)
            {
                indexes = [];
                return false;
            }

            indexes.Add(index);
        }

        indexes = indexes.Distinct().OrderBy(i => i).ToList();
        return indexes.Count > 0;
    }
}
