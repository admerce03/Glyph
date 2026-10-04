using Glyph.Core.Documents;

namespace Glyph.Infrastructure.Documents;

public sealed class DocumentViewStateEntry
{
    public string Path { get; set; } = string.Empty;

    public double Zoom { get; set; } = 1.0;

    public PageLayoutMode PageLayout { get; set; } = PageLayoutMode.Continuous;

    public int CurrentPageIndex { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
