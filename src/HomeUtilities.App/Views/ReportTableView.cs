using System.Windows.Input;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// Draws a report table: the period column stays in place while the numbers scroll sideways inside the table area only (the
/// page itself never scrolls sideways). Every row has a fixed height so the two parts line up. Numbers that rest on incomplete
/// data are muted, the totals row and the subtotal columns are bold, a number with bills or readings behind it can be tapped.
/// </summary>
public sealed class ReportTableView : ContentView
{
    private const double FirstColumnWidth = 92;
    private const double CellWidth = 112;
    private const double RowHeight = 36;
    private const double GroupHeight = 28;
    private const double HeaderHeight = 64;
    private const int HeaderRows = 2;

    public static readonly BindableProperty TableProperty = BindableProperty.Create(
        nameof(Table),
        typeof(ReportTableViewModel),
        typeof(ReportTableView),
        null,
        propertyChanged: (view, _, value) => ((ReportTableView)view).Rebuild(value as ReportTableViewModel));

    public ReportTableViewModel? Table
    {
        get => (ReportTableViewModel?)GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }

    private static Color ResourceColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Gray;

    private static Grid NewGrid(int dataRows)
    {
        var grid = new Grid { RowSpacing = 0, ColumnSpacing = 0 };
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(GroupHeight)));
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(HeaderHeight)));
        for (var i = 0; i < dataRows; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(RowHeight)));
        }

        return grid;
    }

    private static View MakeCell(string text, TextAlignment alignment, bool bold, bool muted, bool tinted, ICommand? tap, int maxLines)
    {
        var label = new Label
        {
            Text = text,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            HorizontalTextAlignment = alignment,
            VerticalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(8, 0),
            MaxLines = maxLines,
            LineBreakMode = maxLines > 1 ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation,
        };
        if (muted && Application.Current?.Resources.TryGetValue("MutedLabel", out var style) == true && style is Style mutedStyle)
        {
            label.Style = mutedStyle;
        }

        var line = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.End };
        line.SetAppThemeColor(BoxView.ColorProperty, ResourceColor("OutlineLight"), ResourceColor("OutlineDark"));

        var cell = new Grid();
        cell.Children.Add(label);
        cell.Children.Add(line);
        if (tinted)
        {
            cell.SetAppThemeColor(VisualElement.BackgroundColorProperty, ResourceColor("SurfaceLight"), ResourceColor("SurfaceDark"));
        }

        if (tap is not null)
        {
            cell.GestureRecognizers.Add(new TapGestureRecognizer { Command = tap });
        }

        return cell;
    }

    private void Rebuild(ReportTableViewModel? table)
    {
        if (table is null || table.Rows.Count == 0)
        {
            Content = null;
            return;
        }

        var firstColumn = NewGrid(table.Rows.Count);
        firstColumn.WidthRequest = FirstColumnWidth;
        var body = NewGrid(table.Rows.Count);
        for (var column = 0; column < table.ColumnCount; column++)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
        }

        // Headings: the property names (merged over their columns), then the column names.
        firstColumn.Add(MakeCell(string.Empty, TextAlignment.Start, true, false, true, null, 1), 0, 0);
        firstColumn.Add(MakeCell(table.PeriodHeader, TextAlignment.Start, true, false, true, null, 2), 0, 1);
        var position = 0;
        foreach (var group in table.Groups)
        {
            var cell = MakeCell(group.Text, TextAlignment.Center, true, false, true, null, 1);
            body.Add(cell, position, 0);
            Grid.SetColumnSpan(cell, group.Span);
            position += group.Span;
        }

        for (var column = 0; column < table.ColumnHeaders.Count; column++)
        {
            body.Add(MakeCell(table.ColumnHeaders[column], TextAlignment.Center, true, false, true, null, 3), column, 1);
        }

        if (table.TotalHeader is { } totalHeader)
        {
            body.Add(MakeCell(totalHeader, TextAlignment.Center, true, false, true, null, 3), table.ColumnHeaders.Count, 1);
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var gridRow = r + HeaderRows;
            firstColumn.Add(MakeCell(row.Label, TextAlignment.Start, row.IsTotalsRow, false, row.IsTotalsRow, null, 1), 0, gridRow);
            for (var column = 0; column < row.Cells.Count; column++)
            {
                var value = row.Cells[column];
                body.Add(
                    MakeCell(value.Text, TextAlignment.End, row.IsTotalsRow || value.IsSubtotal, value.IsApproximate, row.IsTotalsRow, value.OpenCommand, 1),
                    column,
                    gridRow);
            }

            if (row.Total is { } total)
            {
                body.Add(MakeCell(total.Text, TextAlignment.End, true, total.IsApproximate, row.IsTotalsRow, null, 1), row.Cells.Count, gridRow);
            }
        }

        var scroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Default, Content = body };
        var root = new Grid { ColumnSpacing = 0 };
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        root.Add(firstColumn, 0, 0);
        root.Add(scroll, 1, 0);
        Content = root;
    }
}
