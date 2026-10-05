using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Glyph.Pdf.Security;

/// <summary>
/// Minimal PDF Standard Security Handler (V=2, R=3, 128-bit RC4) writer for PDFium SaveAsCopy output.
/// Encrypts stream and string objects and injects an /Encrypt dictionary.
/// </summary>
internal static class PdfRc4StandardSecurity
{
    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>
    /// Permission bits for revision 3+: set bits mean allowed. Bits 1-2 must be 0; bits 7-32 typically 1.
    /// </summary>
    public static int BuildPermissions(bool allowPrint, bool allowModify, bool allowCopy, bool allowAnnotate)
    {
        // Start with all high bits set (typical unrestricted mask) then clear denied operations.
        var p = unchecked((int)0xFFFFF0C0);
        if (allowPrint)
        {
            p |= 1 << 2;
        }

        if (allowModify)
        {
            p |= 1 << 3;
        }

        if (allowCopy)
        {
            p |= 1 << 4;
        }

        if (allowAnnotate)
        {
            p |= 1 << 5;
        }

        return p;
    }

    public static byte[] Encrypt(byte[] clearPdf, string userPassword, string ownerPassword, int permissions)
    {
        ArgumentNullException.ThrowIfNull(clearPdf);
        if (clearPdf.Length < 8 || clearPdf[0] != (byte)'%' || clearPdf[1] != (byte)'P')
        {
            throw new ArgumentException("Input is not a PDF.", nameof(clearPdf));
        }

        userPassword ??= string.Empty;
        ownerPassword ??= userPassword;

        var fileId = MD5.HashData(clearPdf.AsSpan(0, Math.Min(clearPdf.Length, 4096)));
        var oEntry = ComputeO(userPassword, ownerPassword);
        var encryptionKey = ComputeEncryptionKey(userPassword, oEntry, permissions, fileId);
        var uEntry = ComputeU(encryptionKey, fileId);

        var latin1 = Encoding.Latin1;
        var text = latin1.GetString(clearPdf);

        // Strip existing encrypt if somehow present.
        if (text.Contains("/Encrypt", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Document already contains an /Encrypt dictionary.");
        }

        var objectMatches = Regex.Matches(
            text,
            @"(\d+)\s+(\d+)\s+obj\b([\s\S]*?)endobj",
            RegexOptions.CultureInvariant);

        if (objectMatches.Count == 0)
        {
            throw new InvalidOperationException("Could not parse PDF objects for encryption.");
        }

        var maxObj = 0;
        foreach (Match m in objectMatches)
        {
            maxObj = Math.Max(maxObj, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        var encryptObjNum = maxObj + 1;
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        var offsets = new Dictionary<int, long>();
        foreach (Match m in objectMatches)
        {
            var objNum = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var gen = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var body = m.Groups[3].Value;
            var encryptedBody = EncryptObjectBody(body, objNum, gen, encryptionKey);
            offsets[objNum] = sb.Length;
            sb.Append(objNum.ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(gen.ToString(CultureInfo.InvariantCulture));
            sb.Append(" obj\n");
            sb.Append(encryptedBody);
            if (!encryptedBody.EndsWith('\n'))
            {
                sb.Append('\n');
            }

            sb.Append("endobj\n");
        }

        var oHex = Convert.ToHexString(oEntry).ToLowerInvariant();
        var uHex = Convert.ToHexString(uEntry).ToLowerInvariant();
        var idHex = Convert.ToHexString(fileId).ToLowerInvariant();
        var pLiteral = permissions.ToString(CultureInfo.InvariantCulture);

        offsets[encryptObjNum] = sb.Length;
        sb.Append(encryptObjNum.ToString(CultureInfo.InvariantCulture));
        sb.Append(" 0 obj\n<< /Filter /Standard /V 2 /R 3 /Length 128 /P ");
        sb.Append(pLiteral);
        sb.Append(" /O <");
        sb.Append(oHex);
        sb.Append("> /U <");
        sb.Append(uHex);
        sb.Append("> >>\nendobj\n");

        // Find root from original trailer.
        var rootMatch = Regex.Match(text, @"/Root\s+(\d+)\s+(\d+)\s+R");
        if (!rootMatch.Success)
        {
            throw new InvalidOperationException("Could not locate /Root in PDF trailer.");
        }

        var rootRef = rootMatch.Groups[1].Value + " " + rootMatch.Groups[2].Value + " R";
        var xrefOffset = sb.Length;
        var size = encryptObjNum + 1;
        sb.Append("xref\n0 ");
        sb.Append(size.ToString(CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append("0000000000 65535 f \n");
        for (var i = 1; i < size; i++)
        {
            if (offsets.TryGetValue(i, out var off))
            {
                sb.Append(off.ToString("D10", CultureInfo.InvariantCulture));
                sb.Append(" 00000 n \n");
            }
            else
            {
                sb.Append("0000000000 65535 f \n");
            }
        }

        sb.Append("trailer\n<< /Size ");
        sb.Append(size.ToString(CultureInfo.InvariantCulture));
        sb.Append(" /Root ");
        sb.Append(rootRef);
        sb.Append(" /Encrypt ");
        sb.Append(encryptObjNum.ToString(CultureInfo.InvariantCulture));
        sb.Append(" 0 R /ID [<");
        sb.Append(idHex);
        sb.Append("><");
        sb.Append(idHex);
        sb.Append(">] >>\nstartxref\n");
        sb.Append(xrefOffset.ToString(CultureInfo.InvariantCulture));
        sb.Append("\n%%EOF\n");

        return latin1.GetBytes(sb.ToString());
    }

    private static string EncryptObjectBody(string body, int objNum, int gen, byte[] encryptionKey)
    {
        // Encrypt streams: ...stream\nDATA\nendstream
        body = Regex.Replace(
            body,
            @"stream\r?\n([\s\S]*?)\r?\nendstream",
            m =>
            {
                var data = Encoding.Latin1.GetBytes(m.Groups[1].Value);
                var enc = Rc4(BuildObjectKey(encryptionKey, objNum, gen), data);
                return "stream\n" + Encoding.Latin1.GetString(enc) + "\nendstream";
            },
            RegexOptions.CultureInvariant);

        // Encrypt literal strings (simple, non-nested). Skip hex strings and dates already encrypted streams.
        body = Regex.Replace(
            body,
            @"(?<!\\)\((?:\\.|[^\\()])*\)",
            m =>
            {
                var literal = m.Value;
                if (literal.Length < 2)
                {
                    return literal;
                }

                var inner = UnescapePdfLiteral(literal[1..^1]);
                var enc = Rc4(BuildObjectKey(encryptionKey, objNum, gen), Encoding.Latin1.GetBytes(inner));
                return "(" + EscapePdfLiteral(Encoding.Latin1.GetString(enc)) + ")";
            },
            RegexOptions.CultureInvariant);

        return body;
    }

    private static byte[] ComputeO(string userPassword, string ownerPassword)
    {
        var ownerPad = PadPassword(string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword);
        var userPad = PadPassword(userPassword);
        var hash = MD5.HashData(ownerPad);
        // Revision 3: hash 50 times
        for (var i = 0; i < 50; i++)
        {
            hash = MD5.HashData(hash);
        }

        var key = hash.AsSpan(0, 16).ToArray();
        var data = userPad;
        for (var i = 0; i < 20; i++)
        {
            var iterKey = new byte[key.Length];
            for (var j = 0; j < key.Length; j++)
            {
                iterKey[j] = (byte)(key[j] ^ i);
            }

            data = Rc4(iterKey, data);
        }

        return data;
    }

    private static byte[] ComputeEncryptionKey(string userPassword, byte[] oEntry, int permissions, byte[] fileId)
    {
        var userPad = PadPassword(userPassword);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(userPad);
        md5.AppendData(oEntry);
        md5.AppendData(
        [
            (byte)permissions,
            (byte)(permissions >> 8),
            (byte)(permissions >> 16),
            (byte)(permissions >> 24),
        ]);
        md5.AppendData(fileId);
        var hash = md5.GetHashAndReset();
        for (var i = 0; i < 50; i++)
        {
            hash = MD5.HashData(hash.AsSpan(0, 16));
        }

        return hash.AsSpan(0, 16).ToArray();
    }

    private static byte[] ComputeU(byte[] encryptionKey, byte[] fileId)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(Padding);
        md5.AppendData(fileId);
        var hash = md5.GetHashAndReset();
        var data = Rc4(encryptionKey, hash);
        for (var i = 1; i <= 19; i++)
        {
            var iterKey = new byte[encryptionKey.Length];
            for (var j = 0; j < encryptionKey.Length; j++)
            {
                iterKey[j] = (byte)(encryptionKey[j] ^ i);
            }

            data = Rc4(iterKey, data);
        }

        var u = new byte[32];
        Buffer.BlockCopy(data, 0, u, 0, 16);
        // remaining 16 bytes are arbitrary; leave zeros
        return u;
    }

    private static byte[] PadPassword(string password)
    {
        var latin1 = Encoding.Latin1.GetBytes(password);
        var result = new byte[32];
        var n = Math.Min(32, latin1.Length);
        Buffer.BlockCopy(latin1, 0, result, 0, n);
        if (n < 32)
        {
            Buffer.BlockCopy(Padding, 0, result, n, 32 - n);
        }

        return result;
    }

    private static byte[] BuildObjectKey(byte[] encryptionKey, int objNum, int gen)
    {
        var tmp = new byte[encryptionKey.Length + 5];
        Buffer.BlockCopy(encryptionKey, 0, tmp, 0, encryptionKey.Length);
        tmp[encryptionKey.Length] = (byte)objNum;
        tmp[encryptionKey.Length + 1] = (byte)(objNum >> 8);
        tmp[encryptionKey.Length + 2] = (byte)(objNum >> 16);
        tmp[encryptionKey.Length + 3] = (byte)gen;
        tmp[encryptionKey.Length + 4] = (byte)(gen >> 8);
        var hash = MD5.HashData(tmp);
        var keyLen = Math.Min(16, encryptionKey.Length + 5);
        return hash.AsSpan(0, keyLen).ToArray();
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var s = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            s[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 255;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var output = new byte[data.Length];
        var x = 0;
        var y = 0;
        for (var n = 0; n < data.Length; n++)
        {
            x = (x + 1) & 255;
            y = (y + s[x]) & 255;
            (s[x], s[y]) = (s[y], s[x]);
            var k = s[(s[x] + s[y]) & 255];
            output[n] = (byte)(data[n] ^ k);
        }

        return output;
    }

    private static string UnescapePdfLiteral(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                var n = s[++i];
                sb.Append(n switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'b' => '\b',
                    'f' => '\f',
                    _ => n,
                });
            }
            else
            {
                sb.Append(s[i]);
            }
        }

        return sb.ToString();
    }

    private static string EscapePdfLiteral(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '(':
                    sb.Append(@"\(");
                    break;
                case ')':
                    sb.Append(@"\)");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
