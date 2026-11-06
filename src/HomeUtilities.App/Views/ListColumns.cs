using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Lays the cards of a list out in columns on wide windows (T-028f): <c>ListColumns.MinItemWidth="360"</c> on a
/// <see cref="CollectionView"/> gives as many columns of at least that width as fit, 1 to 3. The control keeps its
/// virtualization (it is still a vertical <see cref="GridItemsLayout"/>). The view gets **one** grid layout for its whole life and
/// only its <see cref="GridItemsLayout.Span"/> changes: WinUI ignored a replaced <see cref="CollectionView.ItemsLayout"/> after the first
/// layout, so Properties stayed in one column in a maximised window (R-21).
/// <c>ListColumns.MaxContentWidth</c> on a layout centres its content and limits it to that width (lists ordered by date).
/// </summary>
public static class ListColumns
{
    private const double Spacing = ListLayoutRules.ColumnSpacing;

    public static readonly BindableProperty MinItemWidthProperty = BindableProperty.CreateAttached(
        "MinItemWidth",
        typeof(double),
        typeof(ListColumns),
        0d,
        propertyChanged: OnMinItemWidthChanged);

    public static double GetMinItemWidth(BindableObject view) => (double)view.GetValue(MinItemWidthProperty);

    public static void SetMinItemWidth(BindableObject view, double value) => view.SetValue(MinItemWidthProperty, value);

    public static readonly BindableProperty MaxContentWidthProperty = BindableProperty.CreateAttached(
        "MaxContentWidth",
        typeof(double),
        typeof(ListColumns),
        0d,
        propertyChanged: OnMaxContentWidthChanged);

    public static double GetMaxContentWidth(BindableObject view) => (double)view.GetValue(MaxContentWidthProperty);

    public static void SetMaxContentWidth(BindableObject view, double value) => view.SetValue(MaxContentWidthProperty, value);

    private static void OnMaxContentWidthChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Layout layout)
        {
            return;
        }

        layout.SizeChanged -= OnLayoutSizeChanged;
        if ((double)newValue > 0)
        {
            layout.SizeChanged += OnLayoutSizeChanged;
        }
    }

    // The padding does not change the layout's own width, so this cannot feed back into itself.
    private static void OnLayoutSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Layout layout && layout.Width > 0)
        {
            var side = ListLayoutRules.SideMarginFor(layout.Width, GetMaxContentWidth(layout));
            if (layout.Padding.Left != side)
            {
                layout.Padding = new Thickness(side, 0);
            }
        }
    }

    private static void OnMinItemWidthChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not CollectionView view)
        {
            return;
        }

        view.SizeChanged -= OnSizeChanged;
        if ((double)newValue > 0)
        {
            // One grid layout for the view's whole life; only the span changes afterwards.
            if (view.ItemsLayout is not GridItemsLayout)
            {
                view.ItemsLayout = new GridItemsLayout(1, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = Spacing, VerticalItemSpacing = 0 };
            }

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

            if (view.ItemsLayout is GridItemsLayout grid)
            {
                grid.Span = target;
            }
        });
    }

    private static int? TargetSpan(CollectionView view)
    {
        var minWidth = GetMinItemWidth(view);
        if (minWidth <= 0 || view.Width <= 0)
        {
            return null;
        }

        return ListLayoutRules.SpanFor(view.Width, minWidth);
    }

    private static int CurrentSpan(CollectionView view) => view.ItemsLayout is GridItemsLayout grid ? grid.Span : 1;
}
