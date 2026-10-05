using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// UTF-16LE helpers for form-fill <c>ref ushort</c> string APIs.
/// </summary>
internal static class PdfiumFormStrings
{
    public static unsafe string GetFormFieldName(FpdfFormHandleT form, FpdfAnnotationT annot)
    {
        ushort unused = 0;
        var length = fpdf_annot.FPDFAnnotGetFormFieldName(form, annot, ref unused, 0);
        return ReadUtf16(length, (ref ushort first, uint len) =>
            fpdf_annot.FPDFAnnotGetFormFieldName(form, annot, ref first, len));
    }

    public static unsafe string GetFormFieldValue(FpdfFormHandleT form, FpdfAnnotationT annot)
    {
        ushort unused = 0;
        var length = fpdf_annot.FPDFAnnotGetFormFieldValue(form, annot, ref unused, 0);
        return ReadUtf16(length, (ref ushort first, uint len) =>
            fpdf_annot.FPDFAnnotGetFormFieldValue(form, annot, ref first, len));
    }

    private delegate uint ReadBuffer(ref ushort first, uint length);

    private static unsafe string ReadUtf16(uint lengthInBytes, ReadBuffer reader)
    {
        if (lengthInBytes <= 2)
        {
            return string.Empty;
        }

        var bytes = new byte[lengthInBytes];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            ref var first = ref Unsafe.AsRef<ushort>((void*)handle.AddrOfPinnedObject());
            var written = reader(ref first, lengthInBytes);
            if (written == 0)
            {
                return string.Empty;
            }

            var charCount = (int)written / 2;
            if (charCount <= 0)
            {
                return string.Empty;
            }

            return Encoding.Unicode.GetString(bytes, 0, charCount * 2).TrimEnd('\0');
        }
        finally
        {
            handle.Free();
        }
    }
}
