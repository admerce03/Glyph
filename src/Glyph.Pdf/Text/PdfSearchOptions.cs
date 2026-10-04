namespace Glyph.Pdf.Text;

/// <summary>
/// Options for offline PDF text-layer search.
/// Exact phrase matching is the default (substring match of the full query).
/// </summary>
public sealed record PdfSearchOptions(
    bool CaseSensitive = false,
    bool ExactPhrase = true);
