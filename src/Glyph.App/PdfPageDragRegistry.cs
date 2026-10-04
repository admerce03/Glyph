using System.Collections.Concurrent;
using Glyph.Pdf.Abstractions;

namespace Glyph.App;

/// <summary>
/// Process-wide registry so thumbnail drops can resolve the source <see cref="IPdfDocument"/>
/// for cross-tab / cross-document page drags.
/// </summary>
internal static class PdfPageDragRegistry
{
    private static readonly ConcurrentDictionary<string, WeakReference<IPdfDocument>> Documents = new(StringComparer.Ordinal);

    public static void Register(string documentKey, IPdfDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentKey);
        ArgumentNullException.ThrowIfNull(document);
        Documents[documentKey] = new WeakReference<IPdfDocument>(document);
    }

    public static void Unregister(string documentKey)
    {
        if (!string.IsNullOrWhiteSpace(documentKey))
        {
            Documents.TryRemove(documentKey, out _);
        }
    }

    public static bool TryGet(string documentKey, out IPdfDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(documentKey))
        {
            return false;
        }

        if (!Documents.TryGetValue(documentKey, out var weak))
        {
            return false;
        }

        if (!weak.TryGetTarget(out var target))
        {
            Documents.TryRemove(documentKey, out _);
            return false;
        }

        document = target;
        return true;
    }
}
