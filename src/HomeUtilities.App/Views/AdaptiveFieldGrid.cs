using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// A grid that lays its visible children out in <see cref="Columns"/> equal columns, row after row, and closes the gap of a
/// hidden child (a plain <see cref="Grid"/> would leave an empty cell). The number of columns comes either from the view model
/// (<see cref="Columns"/>: 3 / 2 / 1 for expanded / medium / compact layouts) or, when <see cref="MinColumnWidth"/> is set, from the
/// width the grid gets (as many columns of at least that width as fit, up to <see cref="MaxColumns"/>), so an editor page uses
/// two columns of cards on a wide window and one on a phone. Never scrolls sideways.
/// </summary>
public sealed class AdaptiveFieldGrid : Grid
{
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(int),
        typeof(AdaptiveFieldGrid),
        1,
        propertyChanged: (grid, _, _) => ((AdaptiveFieldGrid)grid).Arrange());

    public static readonly BindableProperty MinColumnWidthProperty = BindableProperty.Create(
        nameof(MinColumnWidth),
        typeof(double),
        typeof(AdaptiveFieldGrid),
        0d,
        propertyChanged: (grid, _, _) => ((AdaptiveFieldGrid)grid).Arrange());

    public static readonly BindableProperty MaxColumnsProperty = BindableProperty.Create(
        nameof(MaxColumns),
        typeof(int),
        typeof(AdaptiveFieldGrid),
        3,
        propertyChanged: (grid, _, _) => ((AdaptiveFieldGrid)grid).Arrange());

    /// <summary>
    /// With <see cref="MinColumnWidth"/> set (an editor page) and only one column, the content is at most this wide and centred, so a
    /// landscape phone does not stretch fields to 800 dp (UI_GUIDELINES §3.3, R-27). 0 = no limit.
    /// </summary>
    public static readonly BindableProperty SingleColumnMaxWidthProperty = BindableProperty.Create(
        nameof(SingleColumnMaxWidth),
        typeof(double),
        typeof(AdaptiveFieldGrid),
        600d,
        propertyChanged: (grid, _, _) => ((AdaptiveFieldGrid)grid).CenterSingleColumn());

    /// <summary>
    /// A child with <c>FullRow="True"</c> starts a new row and spans all columns (buttons and notes under the cards: on a wide
    /// window they must not become a cell next to a card and stretch to its height — owner, 2026-11-02).
    /// </summary>
    public static readonly BindableProperty FullRowProperty = BindableProperty.CreateAttached(
        "FullRow",
        typeof(bool),
        typeof(AdaptiveFieldGrid),
        false,
        propertyChanged: (child, _, _) => ((child as Element)?.Parent as AdaptiveFieldGrid)?.Arrange());

    public static bool GetFullRow(BindableObject view) => (bool)view.GetValue(FullRowProperty);

    public static void SetFullRow(BindableObject view, bool value) => view.SetValue(FullRowProperty, value);

    private bool _arranging;
    private int _arrangedColumns;
    private Thickness? _designPadding;

    public AdaptiveFieldGrid()
    {
        ColumnSpacing = 12;
        RowSpacing = 8;
        ChildAdded += (_, e) =>
        {
            if (e.Element is View view)
            {
                view.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(IsVisible))
                    {
                        Arrange();
                    }
                };
            }

            Arrange();
        };
        ChildRemoved += (_, _) => Arrange();
    }

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>When above zero the number of columns follows the width: as many as fit with this minimum width each.</summary>
    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>Maximum content width of the single-column layout of an editor page; 0 = no limit.</summary>
    public double SingleColumnMaxWidth
    {
        get => (double)GetValue(SingleColumnMaxWidthProperty);
        set => SetValue(SingleColumnMaxWidthProperty, value);
    }

    /// <summary>Upper limit of the columns chosen from <see cref="MinColumnWidth"/>.</summary>
    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (MinColumnWidth > 0 && EffectiveColumns() != _arrangedColumns)
        {
            Arrange();
        }

        CenterSingleColumn();
    }

    // The padding the page asked for, before the centring below added a side margin.
    private Thickness DesignPadding => _designPadding ??= Padding;

    private void CenterSingleColumn()
    {
        if (MinColumnWidth <= 0 || Width <= 0)
        {
            return;
        }

        var design = DesignPadding;
        var side = EffectiveColumns() == 1 ? ListLayoutRules.SideMarginFor(Width - design.HorizontalThickness, SingleColumnMaxWidth) : 0;
        var wanted = new Thickness(design.Left + side, design.Top, design.Right + side, design.Bottom);
        if (Padding != wanted)
        {
            Padding = wanted;
        }
    }

    private int EffectiveColumns()
    {
        if (MinColumnWidth <= 0)
        {
            return Math.Max(1, Columns);
        }

        var available = Width - DesignPadding.HorizontalThickness;
        if (available <= 0)
        {
            return 1;
        }

        var fit = (int)Math.Floor((available + ColumnSpacing) / (MinColumnWidth + ColumnSpacing));
        return Math.Clamp(fit, 1, Math.Max(1, MaxColumns));
    }

    private void Arrange()
    {
        if (_arranging)
        {
            return;
        }

        _arranging = true;
        try
        {
            var columns = EffectiveColumns();
            _arrangedColumns = columns;
            var visible = Children.OfType<View>().Where(v => v.IsVisible).ToList();
            ColumnDefinitions.Clear();
            for (var c = 0; c < columns; c++)
            {
                ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            RowDefinitions.Clear();
            var row = 0;
            var column = 0;
            foreach (var view in visible)
            {
                // Buttons in a cell fill it, so stacked buttons have the same (full) width (T-028h). A FullRow button (Delete …,
                // Save as template under the cards) keeps its natural width: on a wide window it would span the whole page.
                if (!GetFullRow(view) && view is Button { HorizontalOptions.Alignment: not LayoutAlignment.Fill } button)
                {
                    button.HorizontalOptions = LayoutOptions.Fill;
                }

                // Cards in a row align to the top: a short card is not stretched to the height of its neighbour (R-27).
                if (view is Border { VerticalOptions.Alignment: LayoutAlignment.Fill } card)
                {
                    card.VerticalOptions = LayoutOptions.Start;
                }

                if (GetFullRow(view))
                {
                    if (column > 0)
                    {
                        row++;
                        column = 0;
                    }

                    SetRow((IView)view, row);
                    SetColumn((IView)view, 0);
                    SetColumnSpan((IView)view, columns);
                    row++;
                    continue;
                }

                SetRow((IView)view, row);
                SetColumn((IView)view, column);
                SetColumnSpan((IView)view, 1);
                column++;
                if (column == columns)
                {
                    row++;
                    column = 0;
                }
            }

            var rows = column > 0 ? row + 1 : row;
            for (var r = 0; r < rows; r++)
            {
                RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
        }
        finally
        {
            _arranging = false;
        }
    }
}
