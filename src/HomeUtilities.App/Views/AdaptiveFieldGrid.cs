namespace HomeUtilities.App.Views;

/// <summary>
/// A grid that lays its visible children out in <see cref="Columns"/> equal columns, row after row, and closes the gap of a
/// hidden child (a plain <see cref="Grid"/> would leave an empty cell). The number of columns comes from the view model
/// (3 / 2 / 1 for expanded / medium / compact layouts). Never scrolls sideways.
/// </summary>
public sealed class AdaptiveFieldGrid : Grid
{
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(int),
        typeof(AdaptiveFieldGrid),
        1,
        propertyChanged: (grid, _, _) => ((AdaptiveFieldGrid)grid).Arrange());

    private bool _arranging;

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

    private void Arrange()
    {
        if (_arranging)
        {
            return;
        }

        _arranging = true;
        try
        {
            var columns = Math.Max(1, Columns);
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
