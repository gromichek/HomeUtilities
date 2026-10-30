using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// The charts of a report, one block each: title, drawing, legend with the colours of the series, the explanation of the muted
/// points and, after a tap on a bar or a point, its period and value.
/// </summary>
public sealed class ReportChartsView : ContentView
{
    private const double ChartHeight = 240;

    public static readonly BindableProperty ChartsProperty = BindableProperty.Create(
        nameof(Charts),
        typeof(IReadOnlyList<ChartModel>),
        typeof(ReportChartsView),
        null,
        propertyChanged: (view, _, value) => ((ReportChartsView)view).Rebuild(value as IReadOnlyList<ChartModel>));

    public static readonly BindableProperty HintProperty = BindableProperty.Create(
        nameof(Hint),
        typeof(string),
        typeof(ReportChartsView),
        string.Empty,
        propertyChanged: (view, _, _) => ((ReportChartsView)view).Rebuild(((ReportChartsView)view).Charts));

    public ReportChartsView()
    {
        // The drawing uses the colours of the theme at the time of the build: build again when the theme changes.
        Loaded += (_, _) =>
        {
            if (Application.Current is { } app)
            {
                app.RequestedThemeChanged += OnThemeChanged;
            }
        };
        Unloaded += (_, _) =>
        {
            if (Application.Current is { } app)
            {
                app.RequestedThemeChanged -= OnThemeChanged;
            }
        };
    }

    public IReadOnlyList<ChartModel>? Charts
    {
        get => (IReadOnlyList<ChartModel>?)GetValue(ChartsProperty);
        set => SetValue(ChartsProperty, value);
    }

    /// <summary>"Tap a bar or a point to see its value."</summary>
    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Rebuild(Charts);

    private static Color ResourceColor(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : fallback;

    private static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    private void Rebuild(IReadOnlyList<ChartModel>? charts)
    {
        if (charts is null || charts.Count == 0)
        {
            Content = null;
            return;
        }

        var stack = new VerticalStackLayout { Spacing = 16 };
        foreach (var chart in charts)
        {
            stack.Children.Add(Block(chart));
        }

        Content = stack;
    }

    private View Block(ChartModel model)
    {
        var text = ResourceColor(IsDark ? "TextDark" : "TextLight", Colors.Gray);
        var muted = ResourceColor(IsDark ? "MutedDark" : "MutedLight", Colors.Gray);
        var drawable = new ChartDrawable(model)
        {
            TextColor = muted,
            GridColor = ResourceColor(IsDark ? "OutlineDark" : "OutlineLight", Colors.LightGray),
            BackgroundColor = ResourceColor(IsDark ? "CardDark" : "CardLight", Colors.White),
        };
        var tooltip = new Label { FontAttributes = FontAttributes.Bold, MinimumHeightRequest = 20, TextColor = text };
        var graphics = new GraphicsView { Drawable = drawable, HeightRequest = ChartHeight, HorizontalOptions = LayoutOptions.Fill };
        graphics.StartInteraction += (_, args) =>
        {
            var hit = args.Touches.Length > 0 ? drawable.HitTest(args.Touches[0]) : null;
            drawable.Select(hit?.Period);
            tooltip.Text = hit is { } found ? model.Series[found.Series].Points[found.Period].Tooltip : string.Empty;
            graphics.Invalidate();
        };

        var block = new VerticalStackLayout { Spacing = 6 };
        block.Children.Add(new Label { Text = model.Title, FontAttributes = FontAttributes.Bold, TextColor = text });
        block.Children.Add(graphics);
        block.Children.Add(tooltip);
        block.Children.Add(Legend(model, text));
        if (model.PeriodNote is { } periodNote)
        {
            block.Children.Add(new Label { Text = periodNote, FontSize = 12, TextColor = muted });
        }

        if (model.ApproximateNote is { } note)
        {
            block.Children.Add(new Label { Text = note, FontSize = 12, TextColor = muted });
        }

        if (Hint.Length > 0)
        {
            block.Children.Add(new Label { Text = Hint, FontSize = 12, TextColor = muted });
        }

        return block;
    }

    private static View Legend(ChartModel model, Color text)
    {
        var legend = new VerticalStackLayout { Spacing = 4 };
        foreach (var series in model.Series)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };
            row.Children.Add(new BoxView
            {
                WidthRequest = 12,
                HeightRequest = 12,
                CornerRadius = 6,
                VerticalOptions = LayoutOptions.Center,
                Color = Color.TryParse(series.Color, out var color) ? color : Colors.Gray,
            });
            row.Children.Add(new Label { Text = series.Name, TextColor = text, VerticalOptions = LayoutOptions.Center });
            legend.Children.Add(row);
        }

        return legend;
    }
}
