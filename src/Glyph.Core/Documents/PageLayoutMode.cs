namespace Glyph.Core.Documents;

public enum PageLayoutMode
{
    Continuous = 0,
    SinglePage = 1,
    TwoPage = 2,
    /// <summary>Facing pages with page 1 alone as the cover, then 2-3, 4-5, …</summary>
    TwoPageWithCover = 3,
}
