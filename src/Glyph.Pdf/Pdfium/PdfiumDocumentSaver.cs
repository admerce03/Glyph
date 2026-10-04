using System.Runtime.InteropServices;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

internal static class PdfiumDocumentSaver
{
    public static void SaveToPath(FpdfDocumentT handle, string path)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(handle, stream);
        stream.Flush();
    }

    public static byte[] SaveToBytes(FpdfDocumentT handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        using var stream = new MemoryStream();
        Write(handle, stream);
        return stream.ToArray();
    }

    private static void Write(FpdfDocumentT handle, Stream stream)
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

        var ok = fpdf_save.FPDF_SaveAsCopy(handle, writer, 0);
        if (ok == 0)
        {
            throw new InvalidOperationException("Failed to serialize PDF document.");
        }
    }
}
