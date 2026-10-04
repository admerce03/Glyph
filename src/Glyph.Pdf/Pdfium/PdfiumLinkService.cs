using System.Runtime.InteropServices;
using Glyph.Pdf.Abstractions;
using PDFiumCore;

namespace Glyph.Pdf.Pdfium;

public sealed class PdfiumLinkService : IPdfLinkService
{
    private const uint ActionTypeUri = 3;
    private const uint ActionTypeGoto = 1;

    public Task<IReadOnlyList<PdfLink>> GetPageLinksAsync(
        IPdfDocument document,
        int pageIndex,
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
                    var pageInfo = (PdfiumPage)pdfium.GetPage(pageIndex);
                    var page = pageInfo.LoadNativePage();
                    try
                    {
                        var links = new List<PdfLink>();
                        var startPos = 0;
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var found = PdfiumNative.LinkEnumerate(page.__Instance, ref startPos, out var link);
                            if (found == 0 || link == IntPtr.Zero)
                            {
                                break;
                            }

                            if (PdfiumNative.LinkGetAnnotRect(link, out var rect) == 0)
                            {
                                continue;
                            }

                            int? destPage = null;
                            string? uri = null;
                            var dest = PdfiumNative.LinkGetDest(pdfium.Handle.__Instance, link);
                            if (dest != IntPtr.Zero)
                            {
                                var index = PdfiumNative.DestGetDestPageIndex(pdfium.Handle.__Instance, dest);
                                if (index >= 0)
                                {
                                    destPage = index;
                                }
                            }

                            var action = PdfiumNative.LinkGetAction(link);
                            if (action != IntPtr.Zero)
                            {
                                var type = PdfiumNative.ActionGetType(action);
                                if (type == ActionTypeGoto && destPage is null)
                                {
                                    var actionDest = PdfiumNative.ActionGetDest(pdfium.Handle.__Instance, action);
                                    if (actionDest != IntPtr.Zero)
                                    {
                                        var index = PdfiumNative.DestGetDestPageIndex(pdfium.Handle.__Instance, actionDest);
                                        if (index >= 0)
                                        {
                                            destPage = index;
                                        }
                                    }
                                }
                                else if (type == ActionTypeUri)
                                {
                                    uri = ReadUri(pdfium.Handle.__Instance, action);
                                }
                            }

                            // PDFium FS_RECTF uses left/bottom/right/top in page space for link annots.
                            links.Add(new PdfLink(
                                new PdfRect(rect.Left, rect.Bottom, rect.Right, rect.Top),
                                destPage,
                                uri));
                        }

                        return (IReadOnlyList<PdfLink>)links;
                    }
                    finally
                    {
                        fpdfview.FPDF_ClosePage(page);
                    }
                }
            },
            cancellationToken);
    }

    private static string? ReadUri(IntPtr document, IntPtr action)
    {
        var bytes = PdfiumNative.ActionGetURIPath(document, action, IntPtr.Zero, 0);
        if (bytes == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)bytes);
        try
        {
            PdfiumNative.ActionGetURIPath(document, action, buffer, bytes);
            return Marshal.PtrToStringAnsi(buffer)?.TrimEnd('\0');
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
