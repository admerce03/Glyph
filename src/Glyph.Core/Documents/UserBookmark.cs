namespace Glyph.Core.Documents;

/// <summary>User-created PDF bookmark (separate from embedded outline / TOC).</summary>
public sealed class UserBookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public int PageIndex { get; set; }
}
