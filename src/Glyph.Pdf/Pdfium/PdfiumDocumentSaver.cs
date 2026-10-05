using System.Runtime.InteropServices;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

internal static class PdfiumDocumentSaver
{
    public static void SaveToPath(FpdfDocumentT handle, string path, uint flags = 0)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(handle, stream, flags);
        stream.Flush();
    }

    public static byte[] SaveToBytes(FpdfDocumentT handle, uint flags = 0)
    {
        ArgumentNullException.ThrowIfNull(handle);
        using var stream = new MemoryStream();
        Write(handle, stream, flags);
        return stream.ToArray();
    }

    private static void Write(FpdfDocumentT handle, Stream stream, uint flags)
    {
        using var writer = new FPDF_FILEWRITE_();
        writer.Version = 1;
        writer.WriteBlock = (_, data, size) =>
        {
            if (data == IntPtr.Zero || size == 0)
            {
                return 1;
            }

            var buffer = new byte[size];
            Marshal.Copy(data, buffer, 0, (int)size);
            stream.Write(buffer, 0, (int)size);
            return 1;
        };

        var ok = fpdf_save.FPDF_SaveAsCopy(handle, writer, flags);
        if (ok == 0)
        {
            throw new InvalidOperationException("Failed to serialize PDF document.");
        }
    }
}
