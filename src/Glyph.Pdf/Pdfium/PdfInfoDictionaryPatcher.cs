using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Appends a new Info dictionary via an incremental PDF update (no PdfSharp dependency).
/// </summary>
internal static partial class PdfInfoDictionaryPatcher
{
    public static byte[] Apply(byte[] pdfBytes, PdfInfoFields fields)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
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
            throw new InvalidOperationException("Could not locate startxref for Info patch.");
        }

        var trailerMatch = TrailerRegex().Match(text);
        if (!trailerMatch.Success)
        {
            throw new InvalidOperationException("Could not locate trailer dictionary for Info patch.");
        }

        var trailerBody = trailerMatch.Groups[1].Value;
        var rootMatch = RootRegex().Match(trailerBody);
        if (!rootMatch.Success)
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

        var newObjNum = Math.Max(oldSize, 1);
        var infoDict = BuildInfoDictionary(fields);
        var objBody = $"{newObjNum} 0 obj\n{infoDict}\nendobj\n";

        // Strip trailing EOF so we can append an incremental update.
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

        using var ms = new MemoryStream(capacity: trimmedLen + objBody.Length + 256);
        ms.Write(pdfBytes, 0, trimmedLen);
        if (trimmedLen == 0 || pdfBytes[trimmedLen - 1] != (byte)'\n')
        {
            ms.WriteByte((byte)'\n');
        }

        var objOffset = (int)ms.Length;
        var objBytes = latin1.GetBytes(objBody);
        ms.Write(objBytes);

        var xrefOffset = (int)ms.Length;
        var xref =
            "xref\n" +
            "0 1\n" +
            "0000000000 65535 f \n" +
            $"{newObjNum} 1\n" +
            $"{objOffset.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n";
        ms.Write(latin1.GetBytes(xref));

        var newSize = newObjNum + 1;
        var trailer =
            "trailer\n" +
            $"<< /Size {newSize} /Root {rootMatch.Groups[1].Value} 0 R /Info {newObjNum} 0 R /Prev {prevXref}{idPart} >>\n" +
            "startxref\n" +
            $"{xrefOffset}\n" +
            "%%EOF\n";
        ms.Write(latin1.GetBytes(trailer));
        return ms.ToArray();
    }

    public static string BuildInfoDictionary(PdfInfoFields fields)
    {
        var sb = new StringBuilder();
        sb.Append("<<");
        Append(sb, "Title", fields.Title);
        Append(sb, "Author", fields.Author);
        Append(sb, "Subject", fields.Subject);
        Append(sb, "Keywords", fields.Keywords);
        Append(sb, "Creator", fields.Creator);
        Append(sb, "Producer", fields.Producer);
        Append(sb, "CreationDate", fields.CreationDate);
        Append(sb, "ModDate", fields.ModDate);
        sb.Append(" >>");
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string key, string? value)
    {
        if (value is null)
        {
            return;
        }

        sb.Append(" /").Append(key).Append(' ').Append(Encode(value));
    }

    internal static string Encode(string value)
    {
        var needsUnicode = false;
        foreach (var ch in value)
        {
            if (ch is < ' ' or > '~' or '(' or ')' or '\\')
            {
                needsUnicode = true;
                break;
            }
        }

        if (!needsUnicode)
        {
            return "(" + value + ")";
        }

        var utf16 = Encoding.BigEndianUnicode.GetBytes(value);
        var hex = new StringBuilder(4 + utf16.Length * 2);
        hex.Append("<FEFF");
        foreach (var b in utf16)
        {
            hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        hex.Append('>');
        return hex.ToString();
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
}

internal sealed record PdfInfoFields(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator = null,
    string? Producer = null,
    string? CreationDate = null,
    string? ModDate = null);
