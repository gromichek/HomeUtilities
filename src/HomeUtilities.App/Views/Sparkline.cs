using HomeUtilities.ViewModels;
using Microsoft.Maui.Graphics;

namespace HomeUtilities.App.Views;

/// <summary>
/// A small line of twelve monthly values without axes. Months without a figure are gaps (the line breaks, nothing is invented);
/// approximate months are drawn with a dashed line and a hollow marker; the last month has a dot. Read-only.
/// </summary>
public sealed class Sparkline : GraphicsView
{
    public static readonly BindableProperty DataProperty = BindableProperty.Create(
        nameof(Data), typeof(SparklineData), typeof(Sparkline), null,
        propertyChanged: (view, _, _) => ((Sparkline)view).Update());

    public Sparkline()
    {
        WidthRequest = 96;
        HeightRequest = 32;
        InputTransparent = true;
    }

    public SparklineData? Data
    {
        get => (SparklineData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private void Update()
    {
        Drawable = Data is { } data ? new SparklineDrawable(data, ResourceColor("CardLight", "CardDark", Colors.White)) : null;
        Invalidate();
    }

    private static Color ResourceColor(string light, string dark, Color fallback)
    {
        var app = Application.Current;
        return app is not null && app.Resources.TryGetValue(app.RequestedTheme == AppTheme.Dark ? dark : light, out var value) && value is Color color ? color : fallback;
    }

    private sealed class SparklineDrawable(SparklineData data, Color background) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var known = data.Values.OfType<decimal>().ToList();
            if (known.Count == 0)
            {
                return;
            }

            var color = Color.TryParse(data.Color, out var parsed) ? parsed : Colors.Gray;
            var (low, high) = (known.Min(), known.Max());
            var span = high - low;
            const float pad = 4f;
            var step = (dirtyRect.Width - 2 * pad) / Math.Max(1, data.Values.Count - 1);
            float X(int index) => pad + index * step;
            float Y(decimal value) => span == 0m
                ? dirtyRect.Height / 2
                : dirtyRect.Height - pad - (float)((value - low) / span) * (dirtyRect.Height - 2 * pad);

            canvas.StrokeColor = color;
            canvas.StrokeSize = 2;
            canvas.StrokeLineCap = LineCap.Round;
            PointF? previous = null;
            var previousApproximate = false;
            for (var i = 0; i < data.Values.Count; i++)
            {
                if (data.Values[i] is not { } value)
                {
                    previous = null;
                    continue;
                }

                var point = new PointF(X(i), Y(value));
                if (previous is { } from)
                {
                    canvas.StrokeDashPattern = data.Approximate[i] || previousApproximate ? [3, 2] : null;
                    canvas.DrawLine(from, point);
                    canvas.StrokeDashPattern = null;
                }

                previous = point;
                previousApproximate = data.Approximate[i];
            }

            // Markers only for single months between two gaps (a lone point has no line) and for the last month.
            for (var i = 0; i < data.Values.Count; i++)
            {
                if (data.Values[i] is not { } value)
                {
                    continue;
                }

                var isolated = (i == 0 || data.Values[i - 1] is null) && (i == data.Values.Count - 1 || data.Values[i + 1] is null);
                if (!isolated && i != data.Values.Count - 1)
                {
                    continue;
                }

                canvas.FillColor = data.Approximate[i] ? background : color;
                canvas.FillCircle(X(i), Y(value), 3f);
                canvas.StrokeColor = color;
                canvas.StrokeSize = 1.5f;
                canvas.DrawCircle(X(i), Y(value), 3f);
            }
        }
    }
}
