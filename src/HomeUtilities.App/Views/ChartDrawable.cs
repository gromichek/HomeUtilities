using System.Globalization;
using HomeUtilities.ViewModels;
using Microsoft.Maui.Graphics;

namespace HomeUtilities.App.Views;

/// <summary>
/// Draws one <see cref="ChartModel"/> with <c>Microsoft.Maui.Graphics</c>: bars (grouped or stacked) or lines over round value
/// ticks. Points that rest on incomplete data are drawn muted (bars with a dashed outline, hollow markers, dashed line pieces).
/// A tap is resolved to a point with <see cref="HitTest"/>; the period of the tapped point is highlighted.
/// </summary>
public sealed class ChartDrawable(ChartModel model) : IDrawable
{
    private const float LeftMargin = 46;
    private const float RightMargin = 8;
    private const float TopMargin = 8;
    private const float BottomMargin = 24;
    private const float TickLabelHeight = 14;
    private const float FontSize = 11;

    private RectF _plot;
    private ChartScale _scale;
    private int? _selectedPeriod;

    /// <summary>Text colour of the labels.</summary>
    public Color TextColor { get; set; } = Colors.Gray;

    /// <summary>Colour of the grid lines.</summary>
    public Color GridColor { get; set; } = Colors.LightGray;

    /// <summary>Colour behind the hollow markers.</summary>
    public Color BackgroundColor { get; set; } = Colors.White;

    /// <summary>Remembers the tapped period (null = none) so that it is highlighted by the next draw.</summary>
    public void Select(int? period) => _selectedPeriod = period;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _plot = new RectF(LeftMargin, TopMargin, Math.Max(1, dirtyRect.Width - LeftMargin - RightMargin), Math.Max(1, dirtyRect.Height - TopMargin - BottomMargin));
        _scale = ScaleOf();
        var periods = model.Periods.Count;
        if (periods == 0)
        {
            return;
        }

        if (_selectedPeriod is { } selected && selected < periods)
        {
            canvas.FillColor = GridColor.WithAlpha(0.35f);
            canvas.FillRectangle(SlotX(selected), _plot.Top, SlotWidth, _plot.Height);
        }

        DrawGridAndAxes(canvas);
        switch (model.Kind)
        {
            case ChartKind.Lines:
                DrawLines(canvas);
                break;
            case ChartKind.StackedBars:
                DrawStackedBars(canvas);
                break;
            default:
                DrawGroupedBars(canvas);
                break;
        }
    }

    /// <summary>The point under a tap: its series and period; null if the tap hits no data.</summary>
    public (int Series, int Period)? HitTest(PointF at)
    {
        var periods = model.Periods.Count;
        if (periods == 0 || at.X < _plot.Left || at.X > _plot.Right)
        {
            return null;
        }

        var period = Math.Clamp((int)((at.X - _plot.Left) / SlotWidth), 0, periods - 1);
        var candidates = Enumerable.Range(0, model.Series.Count).Where(s => model.Series[s].Points[period].Value is not null).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        switch (model.Kind)
        {
            case ChartKind.Lines:
                return (candidates.MinBy(s => Math.Abs(Y((float)model.Series[s].Points[period].Value!.Value) - at.Y)), period);
            case ChartKind.StackedBars:
                var (low, high) = (0m, 0m);
                var value = (decimal)(_scale.Max - (decimal)((at.Y - _plot.Top) / _plot.Height) * (_scale.Max - _scale.Min));
                foreach (var s in candidates)
                {
                    var v = model.Series[s].Points[period].Value!.Value;
                    var (from, to) = v >= 0 ? (high, high + v) : (low + v, low);
                    if (v >= 0)
                    {
                        high += v;
                    }
                    else
                    {
                        low += v;
                    }

                    if (value >= from && value <= to)
                    {
                        return (s, period);
                    }
                }

                return (candidates[^1], period);
            default:
                var slot = (int)((at.X - SlotX(period)) / (SlotWidth / model.Series.Count));
                var index = Math.Clamp(slot, 0, model.Series.Count - 1);
                return (candidates.Contains(index) ? index : candidates.MinBy(s => Math.Abs(s - index)), period);
        }
    }

    private float SlotWidth => _plot.Width / Math.Max(1, model.Periods.Count);

    private float SlotX(int period) => _plot.Left + period * SlotWidth;

    private float Y(float value) => _plot.Bottom - (float)((decimal)value - _scale.Min) / (float)(_scale.Max - _scale.Min) * _plot.Height;

    private float Y(decimal value) => Y((float)value);

    private ChartScale ScaleOf()
    {
        var (low, high) = (decimal.MaxValue, decimal.MinValue);
        if (model.Kind == ChartKind.StackedBars)
        {
            for (var p = 0; p < model.Periods.Count; p++)
            {
                var values = model.Series.Select(s => s.Points[p].Value ?? 0m).ToList();
                low = Math.Min(low, values.Where(v => v < 0).Sum());
                high = Math.Max(high, values.Where(v => v > 0).Sum());
            }
        }
        else
        {
            foreach (var value in model.Series.SelectMany(s => s.Points).Select(p => p.Value).OfType<decimal>())
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
        }

        if (low > high)
        {
            (low, high) = (0m, 1m);
        }

        return ChartScale.For(low, high, includeZero: model.Kind != ChartKind.Lines);
    }

    private void DrawGridAndAxes(ICanvas canvas)
    {
        var decimals = _scale.Step >= 1m ? 0 : _scale.Step >= 0.1m ? 1 : _scale.Step >= 0.01m ? 2 : 4;
        canvas.FontColor = TextColor;
        canvas.FontSize = FontSize;
        canvas.StrokeSize = 1;
        foreach (var tick in _scale.Ticks())
        {
            var y = Y(tick);
            canvas.StrokeColor = tick == 0m ? TextColor.WithAlpha(0.6f) : GridColor;
            canvas.DrawLine(_plot.Left, y, _plot.Right, y);
            canvas.DrawString(
                tick.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture),
                0,
                y - TickLabelHeight / 2,
                LeftMargin - 4,
                TickLabelHeight,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        // Period labels: every n-th so that they do not overlap.
        var every = Math.Max(1, (int)Math.Ceiling(40f / SlotWidth));
        for (var p = 0; p < model.Periods.Count; p += every)
        {
            canvas.DrawString(
                model.Periods[p],
                SlotX(p) + SlotWidth / 2 - 24 * every,
                _plot.Bottom + 4,
                48 * every,
                TickLabelHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }
    }

    private void DrawGroupedBars(ICanvas canvas)
    {
        var count = Math.Max(1, model.Series.Count);
        var groupWidth = SlotWidth * 0.8f;
        var barWidth = groupWidth / count;
        for (var p = 0; p < model.Periods.Count; p++)
        {
            var left = SlotX(p) + (SlotWidth - groupWidth) / 2;
            for (var s = 0; s < model.Series.Count; s++)
            {
                if (model.Series[s].Points[p] is { Value: { } value } point)
                {
                    DrawBar(canvas, model.Series[s].Color, left + s * barWidth, barWidth, Y(0m), Y(value), point.IsApproximate);
                }
            }
        }
    }

    private void DrawStackedBars(ICanvas canvas)
    {
        var barWidth = SlotWidth * 0.7f;
        for (var p = 0; p < model.Periods.Count; p++)
        {
            var left = SlotX(p) + (SlotWidth - barWidth) / 2;
            var (positive, negative) = (0m, 0m);
            foreach (var series in model.Series)
            {
                if (series.Points[p] is not { Value: { } value } point)
                {
                    continue;
                }

                var from = value >= 0 ? positive : negative;
                var to = from + value;
                if (value >= 0)
                {
                    positive = to;
                }
                else
                {
                    negative = to;
                }

                DrawBar(canvas, series.Color, left, barWidth, Y(from), Y(to), point.IsApproximate);
            }
        }
    }

    private static void DrawBar(ICanvas canvas, string color, float x, float width, float y1, float y2, bool approximate)
    {
        var fill = Color.TryParse(color, out var parsed) ? parsed : Colors.Gray;
        var top = Math.Min(y1, y2);
        var height = Math.Max(1f, Math.Abs(y2 - y1));
        canvas.FillColor = approximate ? fill.WithAlpha(0.4f) : fill;
        canvas.FillRectangle(x, top, Math.Max(1f, width - 1f), height);
        if (approximate)
        {
            canvas.StrokeColor = fill;
            canvas.StrokeSize = 1;
            canvas.StrokeDashPattern = [3, 2];
            canvas.DrawRectangle(x, top, Math.Max(1f, width - 1f), height);
            canvas.StrokeDashPattern = null;
        }
    }

    private void DrawLines(ICanvas canvas)
    {
        foreach (var series in model.Series)
        {
            var color = Color.TryParse(series.Color, out var parsed) ? parsed : Colors.Gray;
            canvas.StrokeColor = color;
            canvas.StrokeSize = 2;
            PointF? previous = null;
            var previousApproximate = false;
            for (var p = 0; p < series.Points.Count; p++)
            {
                if (series.Points[p].Value is not { } value)
                {
                    previous = null;
                    continue;
                }

                var current = new PointF(SlotX(p) + SlotWidth / 2, Y(value));
                var approximate = series.Points[p].IsApproximate;
                if (previous is { } from)
                {
                    canvas.StrokeDashPattern = approximate || previousApproximate ? [4, 3] : null;
                    canvas.DrawLine(from, current);
                    canvas.StrokeDashPattern = null;
                }

                previous = current;
                previousApproximate = approximate;
            }

            for (var p = 0; p < series.Points.Count; p++)
            {
                if (series.Points[p].Value is not { } value)
                {
                    continue;
                }

                var (x, y) = (SlotX(p) + SlotWidth / 2, Y(value));
                canvas.FillColor = series.Points[p].IsApproximate ? BackgroundColor : color;
                canvas.FillCircle(x, y, 3.5f);
                canvas.StrokeColor = color;
                canvas.StrokeSize = 2;
                canvas.DrawCircle(x, y, 3.5f);
            }
        }
    }
}
