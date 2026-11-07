using System.Windows.Input;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// The "this year by month" chart of the Overview: legend, grouped bars (<see cref="ChartDrawable"/>) and a hint. A tap on a month
/// runs <see cref="TappedCommand"/> with the month number 1–12. The series colours <c>accent</c> and <c>muted</c> are theme
/// resources, resolved here; the chart is read-only.
/// </summary>
public sealed class OverviewChartView : ContentView
{
    private const double ChartHeight = 190;

    public static readonly BindableProperty ChartProperty = BindableProperty.Create(
        nameof(Chart), typeof(ChartModel), typeof(OverviewChartView), null,
        propertyChanged: (view, _, _) => ((OverviewChartView)view).Rebuild());

    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(
        nameof(Summary), typeof(string), typeof(OverviewChartView), string.Empty,
        propertyChanged: (view, _, value) => SemanticProperties.SetDescription((OverviewChartView)view, value as string));

    public static readonly BindableProperty HintProperty = BindableProperty.Create(
        nameof(Hint), typeof(string), typeof(OverviewChartView), string.Empty,
        propertyChanged: (view, _, _) => ((OverviewChartView)view).Rebuild());

    public static readonly BindableProperty TappedCommandProperty = BindableProperty.Create(
        nameof(TappedCommand), typeof(ICommand), typeof(OverviewChartView), null);

    public OverviewChartView()
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

    public ChartModel? Chart
    {
        get => (ChartModel?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    /// <summary>Text summary for screen readers.</summary>
    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>"Tap a month to open its bills."</summary>
    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Runs with the tapped month (1–12) as parameter.</summary>
    public ICommand? TappedCommand
    {
        get => (ICommand?)GetValue(TappedCommandProperty);
        set => SetValue(TappedCommandProperty, value);
    }

    private static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    private static Color ResourceColor(string light, string dark, Color fallback) =>
        Application.Current?.Resources.TryGetValue(IsDark ? dark : light, out var value) == true && value is Color color ? color : fallback;

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        if (Chart is not { } model || model.Series.Count == 0)
        {
            Content = null;
            return;
        }

        var text = ResourceColor("TextLight", "TextDark", Colors.Gray);
        var muted = ResourceColor("MutedLight", "MutedDark", Colors.Gray);
        var accent = ResourceColor("PrimaryLight", "PrimaryDark", Colors.SteelBlue);

        // Resolve the colour tokens of the series (data colours, hex, stay as they are).
        var resolved = model with
        {
            Series =
            [
                .. model.Series.Select(s => s with
                {
                    Color = s.Color switch
                    {
                        OverviewViewModel.AccentColor => accent.ToHex(),
                        OverviewViewModel.MutedColor => muted.ToHex(),
                        _ => s.Color,
                    },
                }),
            ],
        };

        var drawable = new ChartDrawable(resolved)
        {
            TextColor = muted,
            GridColor = ResourceColor("OutlineLight", "OutlineDark", Colors.LightGray),
            BackgroundColor = ResourceColor("CardLight", "CardDark", Colors.White),
        };
        var graphics = new GraphicsView { Drawable = drawable, HeightRequest = ChartHeight, HorizontalOptions = LayoutOptions.Fill };
        graphics.StartInteraction += (_, args) =>
        {
            drawable.Select(args.Touches.Length > 0 ? drawable.HitTest(args.Touches[0])?.Period : null);
            graphics.Invalidate();
        };
        graphics.CancelInteraction += (_, _) =>
        {
            drawable.Select(null);
            graphics.Invalidate();
        };
        graphics.EndInteraction += (_, args) =>
        {
            var hit = args.Touches.Length > 0 ? drawable.HitTest(args.Touches[0]) : null;
            drawable.Select(null);
            graphics.Invalidate();
            if (hit is { } found && TappedCommand is { } command && command.CanExecute(found.Period + 1))
            {
                command.Execute(found.Period + 1);
            }
        };

        var legend = new HorizontalStackLayout { Spacing = 14 };
        for (var i = 0; i < resolved.Series.Count; i++)
        {
            var series = resolved.Series[i];
            var item = new HorizontalStackLayout { Spacing = 6 };
            item.Children.Add(new BoxView
            {
                WidthRequest = 12,
                HeightRequest = 12,
                CornerRadius = 3,
                VerticalOptions = LayoutOptions.Center,
                Color = Color.TryParse(series.Color, out var color) ? color : Colors.Gray,
            });
            item.Children.Add(new Label { Text = series.Name, TextColor = text, FontSize = 12, VerticalOptions = LayoutOptions.Center });
            legend.Children.Add(item);
        }

        var block = new VerticalStackLayout { Spacing = 6 };
        block.Children.Add(legend);
        block.Children.Add(graphics);
        if (model.ApproximateNote is { } note)
        {
            block.Children.Add(new Label { Text = note, FontSize = 12, TextColor = muted });
        }

        if (Hint.Length > 0)
        {
            block.Children.Add(new Label { Text = Hint, FontSize = 12, TextColor = muted });
        }

        Content = block;
    }
}
