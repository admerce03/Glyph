using System.Runtime.InteropServices;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumOutlineService : IPdfOutlineService
{
    public Task<IReadOnlyList<PdfOutlineNode>> GetOutlineAsync(
        IPdfDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not PdfiumDocument pdfium)
        {
            throw new ArgumentException("Document must be opened by PdfiumDocumentFactory.", nameof(document));
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfiumLibrary.EnsureInitialized();
                lock (PdfiumSync.Gate)
                {
                    pdfium.ThrowIfDisposed();
                    var roots = new List<PdfOutlineNode>();
                    var child = PdfiumNative.BookmarkGetFirstChild(pdfium.Handle.__Instance, IntPtr.Zero);
                    while (child != IntPtr.Zero)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        roots.Add(ReadNode(pdfium.Handle.__Instance, child));
                        child = PdfiumNative.BookmarkGetNextSibling(pdfium.Handle.__Instance, child);
                    }

                    return (IReadOnlyList<PdfOutlineNode>)roots;
                }
            },
            cancellationToken);
    }

    private static PdfOutlineNode ReadNode(IntPtr document, IntPtr bookmark)
    {
        var title = ReadTitle(bookmark);
        var pageIndex = ReadDestinationPage(document, bookmark);
        var children = new List<PdfOutlineNode>();
        var child = PdfiumNative.BookmarkGetFirstChild(document, bookmark);
        while (child != IntPtr.Zero)
        {
            children.Add(ReadNode(document, child));
            child = PdfiumNative.BookmarkGetNextSibling(document, child);
        }

        return new PdfOutlineNode(title, pageIndex, children);
    }

    private static int? ReadDestinationPage(IntPtr document, IntPtr bookmark)
    {
        var dest = PdfiumNative.BookmarkGetDest(document, bookmark);
        if (dest == IntPtr.Zero)
        {
            var action = PdfiumNative.BookmarkGetAction(bookmark);
            if (action != IntPtr.Zero)
            {
                dest = PdfiumNative.ActionGetDest(document, action);
            }
        }

        if (dest == IntPtr.Zero)
        {
            return null;
        }

        var index = PdfiumNative.DestGetDestPageIndex(document, dest);
        return index >= 0 ? index : null;
    }

    private static string ReadTitle(IntPtr bookmark)
    {
        var bytes = PdfiumNative.BookmarkGetTitle(bookmark, IntPtr.Zero, 0);
        if (bytes == 0)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal((int)bytes);
        try
        {
            PdfiumNative.BookmarkGetTitle(bookmark, buffer, bytes);
            return Marshal.PtrToStringUni(buffer)?.TrimEnd('\0') ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
