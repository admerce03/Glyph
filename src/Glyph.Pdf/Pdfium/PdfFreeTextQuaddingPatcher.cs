using System.Text;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Inserts or replaces FreeText <c>/Q</c> (quadding) on annot dicts identified by <c>/NM</c>.
/// PDFium exposes <c>FPDFAnnot_GetNumberValue</c> but not Set, so we patch after save.
/// </summary>
internal static class PdfFreeTextQuaddingPatcher
{
    public static byte[] Apply(byte[] pdfBytes, string nameMarker, int quadding)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameMarker);
        if (quadding is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(quadding), "Quadding must be 0 (left), 1 (center), or 2 (right).");
        }

        // Markers are hex-only from callers; reject anything that could break PDF literal syntax.
        foreach (var ch in nameMarker)
        {
            if (!char.IsAsciiLetterOrDigit(ch))
            {
                throw new ArgumentException("Name marker must be alphanumeric.", nameof(nameMarker));
            }
        }

        var latin1 = Encoding.Latin1;
        var text = latin1.GetString(pdfBytes);
        var nmToken = "/NM(" + nameMarker + ")";
        var nmIndex = text.IndexOf(nmToken, StringComparison.Ordinal);
        if (nmIndex < 0)
        {
            throw new InvalidOperationException("Could not locate FreeText name marker for quadding patch.");
        }

        if (!TryFindDictionaryBounds(text, nmIndex, out var dictStart, out var dictEnd))
        {
            throw new InvalidOperationException("Could not parse annotation dictionary around name marker.");
        }

        var dict = text[dictStart..dictEnd];
        if (!dict.Contains("/Subtype/FreeText", StringComparison.Ordinal)
            && !dict.Contains("/Subtype /FreeText", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Name marker is not on a FreeText annotation.");
        }

        var qToken = "/Q " + quadding;
        string patchedDict;
        var existingQ = IndexOfQuaddingKey(dict);
        if (existingQ >= 0)
        {
            var valueStart = existingQ + 2;
            while (valueStart < dict.Length && char.IsWhiteSpace(dict[valueStart]))
            {
                valueStart++;
            }

            var valueEnd = valueStart;
            while (valueEnd < dict.Length && (char.IsDigit(dict[valueEnd]) || dict[valueEnd] == '+' || dict[valueEnd] == '-'))
            {
                valueEnd++;
            }

            patchedDict = dict[..existingQ] + qToken + dict[valueEnd..];
        }
        else
        {
            // Prefer inserting just before /Subtype so /Q sits with other annot keys.
            var subtype = dict.IndexOf("/Subtype", StringComparison.Ordinal);
            if (subtype > 0)
            {
                patchedDict = dict[..subtype] + qToken + dict[subtype..];
            }
            else
            {
                // Before closing >>
                patchedDict = dict[..^2] + qToken + ">>";
            }
        }

        var rebuilt = text[..dictStart] + patchedDict + text[dictEnd..];
        return latin1.GetBytes(rebuilt);
    }

    private static int IndexOfQuaddingKey(string dict)
    {
        // Match /Q as a key (not part of another name): /Q followed by whitespace or digit/sign.
        for (var i = 0; i < dict.Length - 1; i++)
        {
            if (dict[i] != '/' || dict[i + 1] != 'Q')
            {
                continue;
            }

            if (i + 2 < dict.Length && IsNameChar(dict[i + 2]))
            {
                continue; // longer name like /QuadPoints
            }

            return i;
        }

        return -1;
    }

    private static bool IsNameChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '_';

    private static bool TryFindDictionaryBounds(string text, int insideIndex, out int start, out int end)
    {
        start = -1;
        end = -1;
        var depth = 0;
        for (var i = insideIndex; i >= 1; i--)
        {
            if (text[i] == '>' && text[i - 1] == '>')
            {
                depth++;
                i--;
            }
            else if (text[i] == '<' && text[i - 1] == '<')
            {
                if (depth == 0)
                {
                    start = i - 1;
                    break;
                }

                depth--;
                i--;
            }
        }

        if (start < 0)
        {
            return false;
        }

        depth = 0;
        for (var i = start; i < text.Length - 1; i++)
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
                    end = i + 1;
                    return true;
                }
            }
        }

        return false;
    }
}
