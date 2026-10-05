namespace Glyph.Core.Documents;

/// <summary>
/// Grid placement for PDF contact-sheet layout (F03-07).
/// </summary>
public static class ContactSheetLayout
{
    public const int DefaultColumns = 4;
    public const double DefaultCellWidth = 140;
    public const double DefaultSpacing = 12;
    public const string OpenHint = "Contact sheet — click a page to open it.";

    public static (int Row, int Column) Cell(int pageIndex, int columns = DefaultColumns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        return (pageIndex / columns, pageIndex % columns);
    }

    public static int RowCount(int pageCount, int columns = DefaultColumns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        if (pageCount == 0)
        {
            return 0;
        }

        return (pageCount + columns - 1) / columns;
    }

    /// <summary>True when <paramref name="pageIndex"/> starts a new horizontal row.</summary>
    public static bool StartsRow(int pageIndex, int columns = DefaultColumns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        return pageIndex % columns == 0;
    }
}
