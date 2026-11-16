namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// The arithmetic of the list layouts (UI_GUIDELINES §3.1), kept free of UI types so it can be tested: how many columns a catalog
/// list gets, and how a list ordered by date is centred.
/// </summary>
public static class ListLayoutRules
{
    /// <summary>Catalog lists (Properties, Suppliers…) have at most this many columns.</summary>
    public const int MaxColumns = 3;

    /// <summary>Space between two columns, in dp.</summary>
    public const double ColumnSpacing = 8;

    /// <summary>
    /// The number of columns of at least <paramref name="minItemWidth"/> that fit into <paramref name="width"/>, between 1 and
    /// <see cref="MaxColumns"/>. Unknown sizes (zero) give one column.
    /// </summary>
    public static int SpanFor(double width, double minItemWidth) =>
        width <= 0 || minItemWidth <= 0
            ? 1
            : Math.Clamp((int)Math.Floor((width + ColumnSpacing) / (minItemWidth + ColumnSpacing)), 1, MaxColumns);

    /// <summary>
    /// The margin on each side that centres content of at most <paramref name="maxWidth"/> in <paramref name="width"/> (lists ordered
    /// by date: one column, centred); 0 when the content is not wider than the limit or there is no limit.
    /// </summary>
    public static double SideMarginFor(double width, double maxWidth) =>
        maxWidth <= 0 || width <= maxWidth ? 0 : Math.Floor((width - maxWidth) / 2);

    /// <summary>
    /// The layout of an editor page (<c>AdaptiveFieldGrid</c> with a minimum column width): the number of columns and the margin on
    /// each side of the content. As many columns of at least <paramref name="minColumnWidth"/> (plus <paramref name="columnSpacing"/>)
    /// fit as the width allows, at most <paramref name="maxColumns"/>. One column is centred with <paramref name="singleColumnMaxWidth"/>.
    /// When <paramref name="wideSingleColumnMaxWidth"/> is above 0 and the width would give two or more columns, the page uses one
    /// column of at most that width instead (Windows, T-058); with 0 the result is the one without this option.
    /// </summary>
    public static (int Columns, double SideMargin) EditorLayoutFor(
        double available,
        double minColumnWidth,
        int maxColumns,
        double columnSpacing,
        double singleColumnMaxWidth,
        double wideSingleColumnMaxWidth)
    {
        if (available <= 0 || minColumnWidth <= 0)
        {
            return (1, 0);
        }

        var fit = (int)Math.Floor((available + columnSpacing) / (minColumnWidth + columnSpacing));
        var columns = Math.Clamp(fit, 1, Math.Max(1, maxColumns));
        if (columns >= 2 && wideSingleColumnMaxWidth > 0)
        {
            return (1, SideMarginFor(available, wideSingleColumnMaxWidth));
        }

        return columns == 1 ? (1, SideMarginFor(available, singleColumnMaxWidth)) : (columns, 0);
    }
}
