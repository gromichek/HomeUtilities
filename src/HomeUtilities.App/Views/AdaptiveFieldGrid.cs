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

    private bool _arranging;
    private int _arrangedColumns;

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
    }

    private int EffectiveColumns()
    {
        if (MinColumnWidth <= 0)
        {
            return Math.Max(1, Columns);
        }

        var available = Width - Padding.HorizontalThickness;
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
            for (var column = 0; column < columns; column++)
            {
                ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            RowDefinitions.Clear();
            for (var row = 0; row < (visible.Count + columns - 1) / columns; row++)
            {
                RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (var i = 0; i < visible.Count; i++)
            {
                SetRow((IView)visible[i], i / columns);
                SetColumn((IView)visible[i], i % columns);
            }
        }
        finally
        {
            _arranging = false;
        }
    }
}
