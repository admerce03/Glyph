using System.Runtime.InteropServices;

namespace Glyph.Pdf.Pdfium;

/// <summary>
/// Direct PDFium exports used where PDFiumCore's CppSharp bindings cannot express
/// null bookmark roots or out-link handles cleanly.
/// </summary>
internal static partial class PdfiumNative
{
    private const string LibraryName = "pdfium";

    [LibraryImport(LibraryName, EntryPoint = "FPDFBookmark_GetFirstChild")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr BookmarkGetFirstChild(IntPtr document, IntPtr bookmark);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBookmark_GetNextSibling")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr BookmarkGetNextSibling(IntPtr document, IntPtr bookmark);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBookmark_GetTitle")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial uint BookmarkGetTitle(IntPtr bookmark, IntPtr buffer, uint buflen);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBookmark_GetDest")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr BookmarkGetDest(IntPtr document, IntPtr bookmark);

    [LibraryImport(LibraryName, EntryPoint = "FPDFBookmark_GetAction")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr BookmarkGetAction(IntPtr bookmark);

    [LibraryImport(LibraryName, EntryPoint = "FPDFAction_GetDest")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr ActionGetDest(IntPtr document, IntPtr action);

    [LibraryImport(LibraryName, EntryPoint = "FPDFAction_GetType")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial uint ActionGetType(IntPtr action);

    [LibraryImport(LibraryName, EntryPoint = "FPDFAction_GetURIPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial uint ActionGetURIPath(IntPtr document, IntPtr action, IntPtr buffer, uint buflen);

    [LibraryImport(LibraryName, EntryPoint = "FPDFDest_GetDestPageIndex")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int DestGetDestPageIndex(IntPtr document, IntPtr dest);

    [LibraryImport(LibraryName, EntryPoint = "FPDFLink_Enumerate")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int LinkEnumerate(IntPtr page, ref int startPos, out IntPtr linkAnnot);

    [LibraryImport(LibraryName, EntryPoint = "FPDFLink_GetAnnotRect")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int LinkGetAnnotRect(IntPtr linkAnnot, out FsRectF rect);

    [LibraryImport(LibraryName, EntryPoint = "FPDFLink_GetDest")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr LinkGetDest(IntPtr document, IntPtr linkAnnot);

    [LibraryImport(LibraryName, EntryPoint = "FPDFLink_GetAction")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr LinkGetAction(IntPtr linkAnnot);

    [StructLayout(LayoutKind.Sequential)]
    public struct FsRectF
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
    }
}
