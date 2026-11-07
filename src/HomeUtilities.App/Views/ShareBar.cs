using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>One horizontal bar split into coloured parts, each as wide as its share (the "by category" card). Read-only.</summary>
public sealed class ShareBar : ContentView
{
    public static readonly BindableProperty SegmentsProperty = BindableProperty.Create(
        nameof(Segments), typeof(IReadOnlyList<ShareSegment>), typeof(ShareBar), null,
        propertyChanged: (view, _, value) => ((ShareBar)view).Rebuild(value as IReadOnlyList<ShareSegment>));

    public IReadOnlyList<ShareSegment>? Segments
    {
        get => (IReadOnlyList<ShareSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    private void Rebuild(IReadOnlyList<ShareSegment>? segments)
    {
        if (segments is null || segments.Count == 0)
        {
            Content = null;
            return;
        }

        var grid = new Grid { ColumnSpacing = 1, HeightRequest = 18 };
        for (var i = 0; i < segments.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(segments[i].Fraction, 0.005), GridUnitType.Star)));
            var part = new BoxView { Color = Color.TryParse(segments[i].Color, out var color) ? color : Colors.Gray };
            grid.Add(part, i, 0);
        }

        Content = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(5) },
            Content = grid,
        };
    }
}
