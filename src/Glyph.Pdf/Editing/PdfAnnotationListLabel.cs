using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Editing;

/// <summary>
/// Sidebar / list labels for annotations (F03-05).
/// </summary>
public static class PdfAnnotationListLabel
{
    public static string Format(PdfAnnotationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var group = string.IsNullOrEmpty(info.GroupId) ? string.Empty : "[G] ";
        if (info.IsCallout)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimPreview(info.Contents);
            var ul = info.IsUnderlined ? " · U" : string.Empty;
            return $"{group}Callout{ul} · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsStickyNote)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimPreview(info.Contents);
            var author = string.IsNullOrWhiteSpace(info.Author) ? string.Empty : $" · {info.Author}";
            return $"{group}Note{author} · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsTextBox)
        {
            var preview = string.IsNullOrWhiteSpace(info.Contents)
                ? "(empty)"
                : TrimPreview(info.Contents);
            var ul = info.IsUnderlined ? " · U" : string.Empty;
            return $"{group}Text{ul} · p.{info.PageIndex + 1}: {preview}";
        }

        if (info.IsStamp)
        {
            return $"{group}Signature · p.{info.PageIndex + 1}";
        }

        if (info.ShapeKind is { } shape)
        {
            var shapeName = shape switch
            {
                PdfShapeKind.Rectangle => "Rect",
                PdfShapeKind.RoundedRectangle => "Round",
                PdfShapeKind.HighlightRectangle => "Area",
                PdfShapeKind.Ellipse => "Ellipse",
                PdfShapeKind.Line => "Line",
                PdfShapeKind.Arrow => "Arrow",
                PdfShapeKind.Freeform => "Freeform",
                PdfShapeKind.Star => "Star",
                PdfShapeKind.Polygon => "Polygon",
                PdfShapeKind.SpeechBubble => "Bubble",
                PdfShapeKind.Loupe => "Loupe",
                _ => "Shape",
            };
            return $"{group}{shapeName} · p.{info.PageIndex + 1}";
        }

        if (info.IsInk)
        {
            return $"{group}Ink · p.{info.PageIndex + 1}";
        }

        var kind = info.TextMarkupKind switch
        {
            PdfTextMarkupKind.Highlight => "Highlight",
            PdfTextMarkupKind.Underline => "Underline",
            PdfTextMarkupKind.StrikeOut => "Strike",
            _ => "Markup",
        };
        return $"{group}{kind} · p.{info.PageIndex + 1}";
    }

    public static string TrimPreview(string text)
    {
        var flat = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flat.Length <= 42 ? flat : flat[..42] + "…";
    }
}
