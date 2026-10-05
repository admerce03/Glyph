using System.Globalization;
using System.Text;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Builds a plain-text notes listing suitable for printing or saving beside a PDF.
/// </summary>
public static class PdfNotesExport
{
    /// <summary>
    /// Formats sticky-note annotations into a printable document.
    /// Non-note annotations are ignored. Notes are ordered by page, then annotation index.
    /// </summary>
    public static string Format(
        IEnumerable<PdfAnnotationInfo> annotations,
        string? documentTitle = null,
        DateTimeOffset? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        var notes = annotations
            .Where(a => a.IsStickyNote)
            .OrderBy(a => a.PageIndex)
            .ThenBy(a => a.AnnotIndex)
            .ToList();

        var when = generatedAt ?? DateTimeOffset.Now;
        var sb = new StringBuilder();
        sb.AppendLine("Glyph — PDF notes");
        if (!string.IsNullOrWhiteSpace(documentTitle))
        {
            sb.Append("Document: ").AppendLine(documentTitle.Trim());
        }

        sb.Append("Generated: ")
            .AppendLine(when.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture));
        sb.Append("Notes: ").AppendLine(notes.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        if (notes.Count == 0)
        {
            sb.AppendLine("(No sticky notes.)");
            return sb.ToString();
        }

        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            sb.Append("Note ").Append((i + 1).ToString(CultureInfo.InvariantCulture))
                .Append(" — page ").Append((note.PageIndex + 1).ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(note.Author))
            {
                sb.Append(" — ").Append(note.Author.Trim());
            }

            sb.AppendLine();

            var body = string.IsNullOrWhiteSpace(note.Contents)
                ? "(empty)"
                : note.Contents.Trim();
            sb.AppendLine(body);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
