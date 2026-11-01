namespace HomeUtilities.App.Views;

/// <summary>
/// Lays the cards of a list out in columns on wide windows (T-028f): <c>ListColumns.MinItemWidth="360"</c> on a
/// <see cref="CollectionView"/> gives as many columns of at least that width as fit, 1 to 3. The control keeps its
/// virtualization (it is still a vertical <see cref="GridItemsLayout"/>).
/// </summary>
public static class ListColumns
{
    private const int MaxColumns = 3;
    private const double Spacing = 8;

    public static readonly BindableProperty MinItemWidthProperty = BindableProperty.CreateAttached(
        "MinItemWidth",
        typeof(double),
        typeof(ListColumns),
        0d,
        propertyChanged: OnMinItemWidthChanged);

    public static double GetMinItemWidth(BindableObject view) => (double)view.GetValue(MinItemWidthProperty);

    public static void SetMinItemWidth(BindableObject view, double value) => view.SetValue(MinItemWidthProperty, value);

    private static void OnMinItemWidthChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not CollectionView view)
        {
            return;
        }

        view.SizeChanged -= OnSizeChanged;
        if ((double)newValue > 0)
        {
            view.SizeChanged += OnSizeChanged;
            Apply(view);
        }
    }

    private static void OnSizeChanged(object? sender, EventArgs e)
    {
        if (sender is CollectionView view)
        {
            Apply(view);
        }
    }

    private static void Apply(CollectionView view)
    {
        var minWidth = GetMinItemWidth(view);
        if (minWidth <= 0 || view.Width <= 0)
        {
            return;
        }

        var span = Math.Clamp((int)Math.Floor((view.Width + Spacing) / (minWidth + Spacing)), 1, MaxColumns);
        var current = view.ItemsLayout is GridItemsLayout grid ? grid.Span : 1;
        if (current == span)
        {
            return;
        }

        view.ItemsLayout = span == 1
            ? new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            : new GridItemsLayout(span, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = Spacing };
    }
}
