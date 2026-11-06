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
}
