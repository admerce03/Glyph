namespace Glyph.Core.Documents;

/// <summary>
/// Maps <see cref="PageLayoutMode"/> to the PDF viewer layout ComboBox indexes
/// (0 Continuous, 1 Single, 2 Two-page, 3 Two-page+cover; 4 Contact sheet is UI-only).
/// </summary>
public static class PageLayoutCombo
{
    public const int ContinuousIndex = 0;
    public const int SinglePageIndex = 1;
    public const int TwoPageIndex = 2;
    public const int TwoPageWithCoverIndex = 3;
    public const int ContactSheetIndex = 4;

    public static PageLayoutMode FromComboIndex(int selectedIndex) => selectedIndex switch
    {
        SinglePageIndex => PageLayoutMode.SinglePage,
        TwoPageIndex => PageLayoutMode.TwoPage,
        TwoPageWithCoverIndex => PageLayoutMode.TwoPageWithCover,
        _ => PageLayoutMode.Continuous,
    };

    public static int ToComboIndex(PageLayoutMode mode) => mode switch
    {
        PageLayoutMode.SinglePage => SinglePageIndex,
        PageLayoutMode.TwoPage => TwoPageIndex,
        PageLayoutMode.TwoPageWithCover => TwoPageWithCoverIndex,
        _ => ContinuousIndex,
    };
}
