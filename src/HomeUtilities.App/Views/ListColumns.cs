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

    private static readonly BindableProperty PendingProperty = BindableProperty.CreateAttached(
        "Pending", typeof(bool), typeof(ListColumns), false);

    // The size changes while the list is being arranged, often in the middle of an items update. Replacing the items layout
    // there makes WinUI throw "Cannot complete a collection modification while another modification is in progress"
    // (owner's error log, 2026-11-02), so the change is posted and done once the current layout pass and update are over.
    private static void Apply(CollectionView view)
    {
        if (TargetSpan(view) is not { } span || span == CurrentSpan(view) || (bool)view.GetValue(PendingProperty))
        {
            return;
        }

        view.SetValue(PendingProperty, true);
        view.Dispatcher.Dispatch(() =>
        {
            view.SetValue(PendingProperty, false);
            if (TargetSpan(view) is not { } target || target == CurrentSpan(view))
            {
                return;
            }

            view.ItemsLayout = target == 1
                ? new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
                : new GridItemsLayout(target, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = Spacing };
        });
    }

    private static int? TargetSpan(CollectionView view)
    {
        var minWidth = GetMinItemWidth(view);
        if (minWidth <= 0 || view.Width <= 0)
        {
            return null;
        }

        return Math.Clamp((int)Math.Floor((view.Width + Spacing) / (minWidth + Spacing)), 1, MaxColumns);
    }

    private static int CurrentSpan(CollectionView view) => view.ItemsLayout is GridItemsLayout grid ? grid.Span : 1;
}
