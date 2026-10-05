using System.Text.Json;
using System.Text.Json.Serialization;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Annotations;

/// <summary>
/// JSON serialization for annotation snapshots (sidebar export / unit tests).
/// </summary>
public static class PdfAnnotationModelSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(IEnumerable<PdfAnnotation> annotations) =>
        JsonSerializer.Serialize(annotations, Options);

    public static IReadOnlyList<PdfAnnotation> Deserialize(string json)
    {
        var list = JsonSerializer.Deserialize<List<PdfAnnotation>>(json, Options);
        return list ?? [];
    }
}
