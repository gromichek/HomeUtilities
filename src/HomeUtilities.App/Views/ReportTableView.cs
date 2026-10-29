using HomeUtilities.App.Services;
using System.Windows.Input;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// Draws a report table: the period column stays in place while the numbers scroll sideways inside the table area only (the
/// page itself never scrolls sideways). Every row has a fixed height so the two parts line up. Each property's columns form a
/// group: a coloured stripe on its header, every other group tinted, a 2 px separator before the group and a bold, tinted
/// total column. Column headers wrap at word boundaries only (a column is as wide as its longest word needs). Numbers that rest
/// on incomplete data are muted, the totals row is bold, a number with bills or readings behind it can be tapped.
/// </summary>
public sealed class ReportTableView : ContentView
{
    private const double FirstColumnWidth = 92;
    private const double MinCellWidth = 112;
    private const double CharWidth = 9;
    private const double CellPadding = 20;
    private const double RowHeight = 36;
    private const double GroupHeight = 30;
    private const double HeaderHeight = 64;
    private const double SeparatorWidth = 2;
    private const double StripeHeight = 4;
    private const int HeaderRows = 2;
    private const double ScrollbarRoom = 24;

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

    /// <summary>How a cell looks.</summary>
    private readonly record struct CellLook(
        TextAlignment Alignment,
        bool Bold = false,
        bool Muted = false,
        CellTint Tint = CellTint.None,
        bool Separator = false,
        bool Stripe = false,
        int MaxLines = 1,
        ICommand? Tap = null,
        string? ToolTip = null);

    private enum CellTint
    {
        None,
        Group,
        Heading,
        Total,
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

    /// <summary>Width that fits the longest word of a header (headers wrap at word boundaries only).</summary>
    private static double WidthFor(string header)
    {
        var longest = header.Split([' ', '-', '/'], StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length).DefaultIfEmpty(0).Max();
        return Math.Max(MinCellWidth, (longest * CharWidth) + CellPadding);
    }

    private static void ApplyTint(VisualElement cell, CellTint tint)
    {
        switch (tint)
        {
            case CellTint.Group:
                cell.SetAppThemeColor(VisualElement.BackgroundColorProperty, ResourceColor("SurfaceLight"), ResourceColor("SurfaceDark"));
                break;
            case CellTint.Heading:
                cell.SetAppThemeColor(VisualElement.BackgroundColorProperty, ResourceColor("SurfaceLight"), ResourceColor("SurfaceDark"));
                break;
            case CellTint.Total:
                cell.SetAppThemeColor(
                    VisualElement.BackgroundColorProperty,
                    ResourceColor("PrimaryLight").WithAlpha(0.12f),
                    ResourceColor("PrimaryDark").WithAlpha(0.18f));
                break;
        }
    }

    private static View MakeCell(string text, CellLook look)
    {
        var label = new Label
        {
            Text = text,
            FontAttributes = look.Bold ? FontAttributes.Bold : FontAttributes.None,
            HorizontalTextAlignment = look.Alignment,
            VerticalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(8, look.Stripe ? StripeHeight : 0, 8, 0),
            MaxLines = look.MaxLines,
            LineBreakMode = look.MaxLines > 1 ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation,
        };
        if (look.Muted && Application.Current?.Resources.TryGetValue("MutedLabel", out var style) == true && style is Style mutedStyle)
        {
            label.Style = mutedStyle;
        }

        if (look.ToolTip is { Length: > 0 } tip)
        {
            ToolTipProperties.SetText(label, tip);
        }

        var line = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.End };
        line.SetAppThemeColor(BoxView.ColorProperty, ResourceColor("OutlineLight"), ResourceColor("OutlineDark"));

        var cell = new Grid();
        cell.Children.Add(label);
        cell.Children.Add(line);
        ApplyTint(cell, look.Tint);

        if (look.Stripe)
        {
            var stripe = new BoxView { HeightRequest = StripeHeight, VerticalOptions = LayoutOptions.Start };
            stripe.SetAppThemeColor(BoxView.ColorProperty, ResourceColor("PrimaryLight"), ResourceColor("PrimaryDark"));
            cell.Children.Add(stripe);
        }

        if (look.Separator)
        {
            var separator = new BoxView { WidthRequest = SeparatorWidth, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Fill };
            separator.SetAppThemeColor(BoxView.ColorProperty, ResourceColor("OutlineLight"), ResourceColor("OutlineDark"));
            cell.Children.Add(separator);
        }

        if (look.Tap is not null)
        {
            cell.GestureRecognizers.Add(new TapGestureRecognizer { Command = look.Tap });
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

        // Per column: the index of its group (-1 = grand total), whether it opens the group, and its width.
        var groupOf = new int[table.ColumnCount];
        var startsGroup = new bool[table.ColumnCount];
        var position = 0;
        foreach (var group in table.Groups)
        {
            for (var i = 0; i < group.Span && position + i < groupOf.Length; i++)
            {
                groupOf[position + i] = group.Index;
                startsGroup[position + i] = i == 0;
            }

            position += group.Span;
        }

        var widths = new double[table.ColumnCount];
        for (var column = 0; column < widths.Length; column++)
        {
            var header = column < table.ColumnHeaders.Count ? table.ColumnHeaders[column] : table.TotalHeader ?? string.Empty;
            widths[column] = WidthFor(header);
        }

        CellTint TintOf(int column, bool totalsRow)
        {
            if (totalsRow)
            {
                return CellTint.Heading;
            }

            return groupOf[column] % 2 == 1 ? CellTint.Group : CellTint.None;
        }

        var firstColumn = NewGrid(table.Rows.Count);
        firstColumn.WidthRequest = FirstColumnWidth;
        var body = NewGrid(table.Rows.Count);
        for (var column = 0; column < table.ColumnCount; column++)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(widths[column])));
        }

        // Headings: the property names (merged over their columns), then the column names.
        firstColumn.Add(MakeCell(string.Empty, new CellLook(TextAlignment.Start, Bold: true, Tint: CellTint.Heading)), 0, 0);
        firstColumn.Add(MakeCell(table.PeriodHeader, new CellLook(TextAlignment.Start, Bold: true, Tint: CellTint.Heading, MaxLines: 2)), 0, 1);
        position = 0;
        foreach (var group in table.Groups)
        {
            var cell = MakeCell(
                group.Text,
                new CellLook(TextAlignment.Center, Bold: true, Tint: CellTint.Heading, Separator: position > 0, Stripe: group.Index >= 0, ToolTip: group.Text));
            body.Add(cell, position, 0);
            Grid.SetColumnSpan(cell, group.Span);
            position += group.Span;
        }

        for (var column = 0; column < table.ColumnHeaders.Count; column++)
        {
            var header = table.ColumnHeaders[column];
            body.Add(
                MakeCell(header, new CellLook(TextAlignment.Center, Bold: true, Tint: CellTint.Heading, Separator: startsGroup[column] && column > 0, MaxLines: 3, ToolTip: header)),
                column,
                1);
        }

        if (table.TotalHeader is { } totalHeader)
        {
            var column = table.ColumnHeaders.Count;
            body.Add(MakeCell(totalHeader, new CellLook(TextAlignment.Center, Bold: true, Tint: CellTint.Total, Separator: true, MaxLines: 3, ToolTip: totalHeader)), column, 1);
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var gridRow = r + HeaderRows;
            firstColumn.Add(
                MakeCell(row.Label, new CellLook(TextAlignment.Start, Bold: row.IsTotalsRow, Tint: row.IsTotalsRow ? CellTint.Heading : CellTint.None)),
                0,
                gridRow);
            for (var column = 0; column < row.Cells.Count; column++)
            {
                var value = row.Cells[column];
                var tint = value.IsSubtotal && !row.IsTotalsRow ? CellTint.Total : TintOf(column, row.IsTotalsRow);
                body.Add(
                    MakeCell(
                        value.Text,
                        new CellLook(
                            TextAlignment.End,
                            Bold: row.IsTotalsRow || value.IsSubtotal,
                            Muted: value.IsApproximate,
                            Tint: tint,
                            Separator: startsGroup[column] && column > 0,
                            Tap: value.OpenCommand)),
                    column,
                    gridRow);
            }

            if (row.Total is { } total)
            {
                body.Add(
                    MakeCell(total.Text, new CellLook(TextAlignment.End, Bold: true, Muted: total.IsApproximate, Tint: CellTint.Total, Separator: true)),
                    row.Cells.Count,
                    gridRow);
            }
        }

        var scroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Default, Content = body };

        // Room below the last row: the horizontal scrollbar would otherwise sit on top of it.
        body.Margin = new Thickness(0, 0, 0, ScrollbarRoom);
        firstColumn.Margin = new Thickness(0, 0, 0, ScrollbarRoom);

        // Windows: the wheel over the table must scroll the page, not the table (the table scrolls sideways only).
        scroll.HandlerChanged += (_, _) => WheelForwarding.Attach(scroll);
        var root = new Grid { ColumnSpacing = 0 };
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        root.Add(firstColumn, 0, 0);
        root.Add(scroll, 1, 0);
        Content = root;
    }
}
