using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// UTF-16LE helpers for PDFiumCore's awkward <c>ref ushort</c> string APIs.
/// </summary>
internal static class PdfiumAnnotStrings
{
    public static unsafe bool SetString(FpdfAnnotationT annot, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(annot);
        ArgumentException.ThrowIfNullOrEmpty(key);
        value ??= string.Empty;

        var bytes = Encoding.Unicode.GetBytes(value + "\0");
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            ref var first = ref Unsafe.AsRef<ushort>((void*)handle.AddrOfPinnedObject());
            return fpdf_annot.FPDFAnnotSetStringValue(annot, key, ref first) != 0;
        }
        finally
        {
            handle.Free();
        }
    }

    public static unsafe string GetString(FpdfAnnotationT annot, string key)
    {
        ArgumentNullException.ThrowIfNull(annot);
        ArgumentException.ThrowIfNullOrEmpty(key);

        ushort unused = 0;
        var length = fpdf_annot.FPDFAnnotGetStringValue(annot, key, ref unused, 0);
        if (length <= 2)
        {
            return string.Empty;
        }

        var bytes = new byte[length];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            ref var first = ref Unsafe.AsRef<ushort>((void*)handle.AddrOfPinnedObject());
            var written = fpdf_annot.FPDFAnnotGetStringValue(annot, key, ref first, length);
            if (written == 0)
            {
                return string.Empty;
            }

            // Buffer is UTF-16LE including trailing NUL.
            var charCount = (int)written / 2;
            if (charCount <= 0)
            {
                return string.Empty;
            }

            var text = Encoding.Unicode.GetString(bytes, 0, charCount * 2);
            return text.TrimEnd('\0');
        }
        finally
        {
            handle.Free();
        }
    }
}
