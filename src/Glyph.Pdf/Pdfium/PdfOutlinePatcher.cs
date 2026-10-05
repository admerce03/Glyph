using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Writes a flat PDF outline (/Outlines) from title+page-index entries via an incremental update.
/// PDFium has no public bookmark-write API (F09-08).
/// </summary>
internal static partial class PdfOutlinePatcher
{
    public sealed record Entry(string Title, int PageIndex);

    public static byte[] Apply(byte[] pdfBytes, IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("At least one outline entry is required.", nameof(entries));
        }

        if (pdfBytes.Length < 8)
        {
            throw new InvalidOperationException("PDF buffer is too small to patch.");
        }

        var latin1 = Encoding.Latin1;
        var text = latin1.GetString(pdfBytes);

        var startxrefMatch = StartxrefRegex().Match(text);
        if (!startxrefMatch.Success
            || !int.TryParse(startxrefMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prevXref))
        {
            throw new InvalidOperationException("Could not locate startxref for outline patch.");
        }

        var trailerMatch = TrailerRegex().Match(text);
        if (!trailerMatch.Success)
        {
            throw new InvalidOperationException("Could not locate trailer dictionary for outline patch.");
        }

        var trailerBody = trailerMatch.Groups[1].Value;
        var rootMatch = RootRegex().Match(trailerBody);
        if (!rootMatch.Success
            || !int.TryParse(rootMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rootObj))
        {
            throw new InvalidOperationException("Trailer is missing /Root.");
        }

        var sizeMatch = SizeRegex().Match(trailerBody);
        var oldSize = 1;
        if (sizeMatch.Success)
        {
            _ = int.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out oldSize);
        }

        var idMatch = IdRegex().Match(trailerBody);
        var idPart = idMatch.Success ? " /ID " + idMatch.Groups[1].Value.Trim() : string.Empty;

        var pageObjs = CollectPageObjectNumbers(text, rootObj);
        if (pageObjs.Count == 0)
        {
            throw new InvalidOperationException("Could not locate page objects for outline destinations.");
        }

        foreach (var entry in entries)
        {
            if (entry.PageIndex < 0 || entry.PageIndex >= pageObjs.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    $"Outline entry '{entry.Title}' targets page {entry.PageIndex}, but the PDF has {pageObjs.Count} page(s).");
            }
        }

        var catalogBody = ReadObjectDictionary(text, rootObj)
            ?? throw new InvalidOperationException("Could not read Catalog dictionary.");

        var nextObj = Math.Max(oldSize, 1);
        var outlinesObj = nextObj++;
        var itemObjs = new int[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            itemObjs[i] = nextObj++;
        }

        var newCatalogObj = nextObj++;

        var sb = new StringBuilder(entries.Count * 128 + 256);
        // Outline root
        sb.Append(outlinesObj).Append(" 0 obj\n")
            .Append("<< /Type /Outlines /First ").Append(itemObjs[0]).Append(" 0 R /Last ")
            .Append(itemObjs[^1]).Append(" 0 R /Count ").Append(entries.Count).Append(" >>\nendobj\n");

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var title = string.IsNullOrWhiteSpace(entry.Title) ? $"Page {entry.PageIndex + 1}" : entry.Title.Trim();
            sb.Append(itemObjs[i]).Append(" 0 obj\n<< /Title ").Append(PdfInfoDictionaryPatcher.Encode(title))
                .Append(" /Parent ").Append(outlinesObj).Append(" 0 R");
            if (i > 0)
            {
                sb.Append(" /Prev ").Append(itemObjs[i - 1]).Append(" 0 R");
            }

            if (i + 1 < entries.Count)
            {
                sb.Append(" /Next ").Append(itemObjs[i + 1]).Append(" 0 R");
            }

            sb.Append(" /Dest [ ").Append(pageObjs[entry.PageIndex]).Append(" 0 R /Fit ] >>\nendobj\n");
        }

        // New Catalog: copy old keys, replace/add /Outlines.
        var catalogWithoutOutlines = StripKey(catalogBody, "Outlines").TrimEnd();
        if (!catalogWithoutOutlines.EndsWith(">>", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Catalog dictionary is malformed.");
        }

        var catalogCore = catalogWithoutOutlines[..^2].TrimEnd();
        sb.Append(newCatalogObj).Append(" 0 obj\n")
            .Append(catalogCore)
            .Append(" /Outlines ").Append(outlinesObj).Append(" 0 R >>\nendobj\n");

        var objBody = sb.ToString();

        var trimmedLen = pdfBytes.Length;
        while (trimmedLen > 0
               && (pdfBytes[trimmedLen - 1] == (byte)'\n'
                   || pdfBytes[trimmedLen - 1] == (byte)'\r'
                   || pdfBytes[trimmedLen - 1] == (byte)' '))
        {
            trimmedLen--;
        }

        var eof = "%%EOF"u8;
        if (trimmedLen >= eof.Length
            && pdfBytes.AsSpan(trimmedLen - eof.Length, eof.Length).SequenceEqual(eof))
        {
            trimmedLen -= eof.Length;
            while (trimmedLen > 0
                   && (pdfBytes[trimmedLen - 1] == (byte)'\n' || pdfBytes[trimmedLen - 1] == (byte)'\r'))
            {
                trimmedLen--;
            }
        }

        using var ms = new MemoryStream(capacity: trimmedLen + objBody.Length + 512);
        ms.Write(pdfBytes, 0, trimmedLen);
        if (trimmedLen == 0 || pdfBytes[trimmedLen - 1] != (byte)'\n')
        {
            ms.WriteByte((byte)'\n');
        }

        var firstObjOffset = (int)ms.Length;
        var objBytes = latin1.GetBytes(objBody);
        ms.Write(objBytes);

        // Build xref for new objects: outlines, items..., new catalog.
        var newObjCount = 1 + entries.Count + 1; // root + items + catalog
        var offsets = new List<int>(newObjCount);
        // Approximate offsets by scanning written objBody for "N 0 obj"
        var written = latin1.GetString(objBytes);
        var searchFrom = 0;
        for (var n = 0; n < newObjCount; n++)
        {
            var marker = (outlinesObj + n).ToString(CultureInfo.InvariantCulture) + " 0 obj";
            var at = written.IndexOf(marker, searchFrom, StringComparison.Ordinal);
            if (at < 0)
            {
                throw new InvalidOperationException("Failed to locate written outline object for xref.");
            }

            offsets.Add(firstObjOffset + at);
            searchFrom = at + marker.Length;
        }

        var xrefOffset = (int)ms.Length;
        var xref = new StringBuilder();
        xref.Append("xref\n0 1\n0000000000 65535 f \n");
        xref.Append(outlinesObj).Append(' ').Append(newObjCount).Append('\n');
        foreach (var off in offsets)
        {
            xref.Append(off.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        ms.Write(latin1.GetBytes(xref.ToString()));

        var newSize = outlinesObj + newObjCount;
        var trailer =
            "trailer\n" +
            $"<< /Size {newSize} /Root {newCatalogObj} 0 R /Prev {prevXref}{idPart} >>\n" +
            "startxref\n" +
            $"{xrefOffset}\n" +
            "%%EOF\n";
        ms.Write(latin1.GetBytes(trailer));
        return ms.ToArray();
    }

    internal static List<int> CollectPageObjectNumbers(string text, int catalogObj)
    {
        var catalog = ReadObjectDictionary(text, catalogObj)
            ?? throw new InvalidOperationException("Catalog object missing.");
        var pagesMatch = PagesRefRegex().Match(catalog);
        if (!pagesMatch.Success
            || !int.TryParse(pagesMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pagesObj))
        {
            throw new InvalidOperationException("Catalog is missing /Pages.");
        }

        var pages = new List<int>();
        WalkPages(text, pagesObj, pages, depth: 0);
        return pages;
    }

    private static void WalkPages(string text, int objNum, List<int> pages, int depth)
    {
        if (depth > 32)
        {
            throw new InvalidOperationException("Page tree nesting exceeds limit.");
        }

        var dict = ReadObjectDictionary(text, objNum);
        if (dict is null)
        {
            return;
        }

        if (TypeIs(dict, "Page"))
        {
            pages.Add(objNum);
            return;
        }

        var kidsMatch = KidsRegex().Match(dict);
        if (!kidsMatch.Success)
        {
            return;
        }

        foreach (Match m in RefRegex().Matches(kidsMatch.Groups[1].Value))
        {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kid))
            {
                WalkPages(text, kid, pages, depth + 1);
            }
        }
    }

    private static bool TypeIs(string dict, string typeName)
    {
        // Prefer delimited matches so "/Type/Pages" does not satisfy typeName "Page".
        var compact = "/Type/" + typeName;
        var spaced = "/Type /" + typeName;
        for (var i = 0; i < dict.Length;)
        {
            var atCompact = dict.IndexOf(compact, i, StringComparison.Ordinal);
            var atSpaced = dict.IndexOf(spaced, i, StringComparison.Ordinal);
            var at = atCompact < 0 ? atSpaced : atSpaced < 0 ? atCompact : Math.Min(atCompact, atSpaced);
            if (at < 0)
            {
                return false;
            }

            var token = at == atCompact ? compact : spaced;
            var end = at + token.Length;
            if (end >= dict.Length || !IsNameContinue(dict[end]))
            {
                return true;
            }

            i = end;
        }

        return false;
    }

    private static bool IsNameContinue(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '_';

    private static string? ReadObjectDictionary(string text, int objNum)
    {
        var marker = objNum.ToString(CultureInfo.InvariantCulture) + " 0 obj";
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var dictStart = text.IndexOf("<<", at, StringComparison.Ordinal);
        if (dictStart < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = dictStart; i < text.Length - 1; i++)
        {
            if (text[i] == '<' && text[i + 1] == '<')
            {
                depth++;
                i++;
            }
            else if (text[i] == '>' && text[i + 1] == '>')
            {
                depth--;
                i++;
                if (depth == 0)
                {
                    return text[dictStart..(i + 1)];
                }
            }
        }

        return null;
    }

    private static string StripKey(string dict, string key)
    {
        // Remove "/Key <value>" where value is `N 0 R` or a nested dict/array — for Outlines only refs.
        var pattern = @"/\s*" + Regex.Escape(key) + @"\s+\d+\s+0\s+R";
        return Regex.Replace(dict, pattern, string.Empty);
    }

    [GeneratedRegex(@"startxref\s+(\d+)\s*%%EOF\s*\z", RegexOptions.Singleline)]
    private static partial Regex StartxrefRegex();

    [GeneratedRegex(@"trailer\s*<<(.*?)>>\s*startxref", RegexOptions.Singleline)]
    private static partial Regex TrailerRegex();

    [GeneratedRegex(@"\/Root\s+(\d+)\s+0\s+R")]
    private static partial Regex RootRegex();

    [GeneratedRegex(@"\/Size\s+(\d+)")]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"\/ID\s*(\[[^\]]*\])")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"\/Pages\s+(\d+)\s+0\s+R")]
    private static partial Regex PagesRefRegex();

    [GeneratedRegex(@"\/Kids\s*\[([^\]]*)\]")]
    private static partial Regex KidsRegex();

    [GeneratedRegex(@"(\d+)\s+0\s+R")]
    private static partial Regex RefRegex();
}
