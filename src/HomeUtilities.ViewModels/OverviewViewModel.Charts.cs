using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reports;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The three charts of the Overview (T-038): this year by month, spending by category, consumption.</summary>
public sealed partial class OverviewViewModel
{
    /// <summary>Token of the colour of the chart series "this year"; the view resolves it from the theme resources.</summary>
    public const string AccentColor = "accent";

    /// <summary>Token of the colour of the chart series "last year" (muted); resolved by the view.</summary>
    public const string MutedColor = "muted";

    private IReadOnlyList<ConsumptionSeries> _consumption = [];

    [ObservableProperty]
    private string _categoriesHeader = string.Empty;

    [ObservableProperty]
    private string _consumptionHeader = string.Empty;

    [ObservableProperty]
    private string _consumptionSubtitle = string.Empty;

    [ObservableProperty]
    private string _yearChartHint = string.Empty;

    /// <summary>The months of this year and last year as grouped bars; null = no chart (no bill this year).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasYearChart))]
    private ChartModel? _yearChart;

    /// <summary>Text summary of the chart for screen readers: "October 2026: 95,00 €, 2025: 90,00 €; …".</summary>
    [ObservableProperty]
    private string _yearChartSummary = string.Empty;

    [ObservableProperty]
    private string _categoriesSubtitle = string.Empty;

    [ObservableProperty]
    private string _categoriesSummary = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ShareSegment> _categorySegments = [];

    // The visibility follows the ROWS (set after the segments): notifying on the segments left the card body hidden (owner, T-038).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategories), nameof(ShowCategoriesEmpty))]
    private IReadOnlyList<CategoryRowViewModel> _categoryRows = [];

    [ObservableProperty]
    private string _categoriesEmptyText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConsumption))]
    private IReadOnlyList<ConsumptionRowViewModel> _consumptionRows = [];

    public bool HasYearChart => YearChart is not null;

    public bool HasCategories => CategoryRows.Count > 0;

    public bool ShowCategoriesEmpty => IsLoaded && CategoryRows.Count == 0;

    /// <summary>The consumption card is hidden when there are no meters or readings.</summary>
    public bool HasConsumption => ConsumptionRows.Count > 0;

    private void RefreshChartTexts()
    {
        CategoriesHeader = _localizer.Get("OverviewCategoriesHeader", _clock.Today.Year);
        CategoriesEmptyText = _localizer["OverviewCategoriesEmpty"];
        ConsumptionHeader = _localizer["OverviewConsumptionHeader"];
        ConsumptionSubtitle = _localizer["OverviewConsumptionSubtitle"];
        YearChartHint = _localizer["OverviewChartHint"];
    }

    private void BuildCharts()
    {
        BuildYearChart();
        BuildCategories();
        BuildConsumption();
        OnPropertyChanged(nameof(ShowCategoriesEmpty));
    }

    private string Symbol(Currency currency) => _localizer["Currency_Symbol_" + currency];

    // ---- A: this year by month -----------------------------------------------------------------------------------

    private void BuildYearChart()
    {
        var today = _clock.Today;
        if (OverviewCharts.MonthlyComparison(_data, today, _propertyFilter) is not { } bars)
        {
            YearChart = null;
            YearChartSummary = string.Empty;
            return;
        }

        var symbol = Symbol(bars.Currency);
        var periods = Enumerable.Range(1, 12).Select(m => MonthFormat.ShortName(_localizer, m)).ToList();
        var series = new List<ChartSeries>
        {
            new(bars.Year.ToString(CultureInfo.InvariantCulture), AccentColor, Points(bars.ThisYear, bars.ThisYearApproximate, bars.Year, symbol)),
        };
        if (bars.HasLastYear)
        {
            series.Add(new(
                (bars.Year - 1).ToString(CultureInfo.InvariantCulture), MutedColor, Points(bars.LastYear, bars.LastYearApproximate, bars.Year - 1, symbol)));
        }

        var approximate = series.Any(s => s.Points.Any(p => p.IsApproximate));
        YearChart = new ChartModel(
            _localizer["OverviewYearHeader"],
            ChartKind.Bars,
            periods,
            series,
            ReportValueKind.Money,
            symbol,
            approximate,
            approximate ? _localizer["ReportChartApproximate"] : null);

        var parts = new List<string>();
        for (var month = 1; month <= 12; month++)
        {
            if (bars.ThisYear[month - 1] is not { } value)
            {
                continue;
            }

            var name = MonthFormat.Long(new YearMonth(bars.Year, month));
            parts.Add(bars.LastYear[month - 1] is { } last
                ? _localizer.Get("OverviewChartMonthWithLast", name, BillText.Money(value, symbol), bars.Year - 1, BillText.Money(last, symbol))
                : _localizer.Get("OverviewChartMonth", name, BillText.Money(value, symbol)));
        }

        YearChartSummary = string.Join("; ", parts);
    }

    private List<ChartPoint> Points(IReadOnlyList<decimal?> values, IReadOnlyList<bool> approximate, int year, string symbol) =>
    [
        .. values.Select((value, index) => new ChartPoint(
            value,
            value is not null && approximate[index],
            value is { } v ? _localizer.Get("OverviewChartMonth", MonthFormat.Long(new YearMonth(year, index + 1)), BillText.Money(v, symbol)) : string.Empty)),
    ];

    /// <summary>A tap on a month of the chart: the bills of that month of this year (and of the chosen property).</summary>
    [RelayCommand]
    public Task OpenMonthAsync(int month) => RunSafeAsync(() =>
    {
        var parameters = BillsQuery(null, new YearMonth(_clock.Today.Year, month), null);
        return _navigation.GoToAsync(Routes.BillsFor, parameters);
    });

    private Dictionary<string, object> BillsQuery(int? year, YearMonth? month, Guid? categoryId)
    {
        var parameters = new Dictionary<string, object>();
        if (_propertyFilter is { } property)
        {
            parameters[EditorNavigation.PropertyIdParameter] = property;
        }

        if (month is { } shown)
        {
            parameters[BillsViewModel.MonthParameter] = shown.ToString();
        }
        else if (year is { } shownYear)
        {
            parameters[BillsViewModel.YearParameter] = shownYear;
        }

        if (categoryId is { } category)
        {
            parameters[BillsViewModel.CategoryParameter] = category;
        }

        return parameters;
    }

    // ---- B: spending by category ---------------------------------------------------------------------------------

    private void BuildCategories()
    {
        var today = _clock.Today;
        if (OverviewCharts.SpendingByCategory(_data, today, _propertyFilter) is not { } spending)
        {
            CategorySegments = [];
            CategoryRows = [];
            CategoriesSubtitle = string.Empty;
            CategoriesSummary = string.Empty;
            return;
        }

        var symbol = Symbol(spending.Currency);
        var names = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
        var first = names[0].ToLower(CultureInfo.CurrentCulture);
        var last = names[spending.ThroughMonth - 1].ToLower(CultureInfo.CurrentCulture);
        var total = BillText.Money(spending.Total, symbol);
        CategoriesSubtitle = spending.ThroughMonth == 1
            ? _localizer.Get("OverviewCategoriesSubtitleOne", first, total)
            : _localizer.Get("OverviewCategoriesSubtitle", first, last, total);

        var positive = spending.Slices.Where(s => s.Amount > 0).Sum(s => s.Amount);
        var categories = _data.Categories.ToDictionary(c => c.Id);
        var segments = new List<ShareSegment>();
        var rows = new List<CategoryRowViewModel>();
        foreach (var slice in spending.Slices)
        {
            var category = slice.CategoryId is { } id && categories.TryGetValue(id, out var found) ? found : null;
            var name = category is null ? _localizer["OverviewCategoryOther"] : CategoryNames.DisplayName(category, _localizer);
            var color = category?.Color ?? ReportChartBuilder.OtherColor;
            var share = positive > 0 && slice.Amount > 0 ? slice.Amount / positive : 0m;
            var shareText = share > 0 ? $"{Math.Round(share * 100m, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture)}{BillText.NonBreakingSpace}%" : string.Empty;
            var amount = BillText.Money(slice.Amount, symbol);
            if (share > 0)
            {
                segments.Add(new ShareSegment((double)share, color));
            }

            var categoryId = slice.CategoryId;
            rows.Add(new CategoryRowViewModel(
                name,
                color,
                amount,
                shareText,
                shareText.Length > 0 ? $"{name}: {amount}, {shareText}" : $"{name}: {amount}",
                // "Other" has no single category to filter by: it opens the year's bills.
                () => RunSafeAsync(() => _navigation.GoToAsync(Routes.BillsFor, BillsQuery(today.Year, null, categoryId)))));
        }

        CategorySegments = segments;
        CategoryRows = rows;
        CategoriesSummary = $"{CategoriesHeader}. {CategoriesSubtitle}. {string.Join("; ", rows.Select(r => r.Description))}";
    }

    // ---- C: consumption ------------------------------------------------------------------------------------------

    private void BuildConsumption()
    {
        var trends = OverviewCharts.Trends(_consumption, _propertyFilter);
        var rows = new List<ConsumptionRowViewModel>();
        for (var i = 0; i < trends.Count; i++)
        {
            var trend = trends[i];
            var name = EnumText.Get(_localizer, trend.Resource);
            var unit = _localizer["Enum_MeterUnit_" + trend.Unit];
            var value = trend.Last is { } last
                ? (trend.LastApproximate ? "≈" + BillText.NonBreakingSpace : string.Empty) + BillText.WithUnit(last.ToString("#,0.##", CultureInfo.CurrentCulture), unit)
                : "—";
            if (trend.Last is not null && trend.LastIndex is >= 0 and < OverviewCharts.ConsumptionMonths - 1)
            {
                // An older month than the last complete one: say which.
                var shown = YearMonth.From(_clock.Today).AddMonths(-OverviewCharts.ConsumptionMonths + trend.LastIndex);
                value = _localizer.Get("OverviewConsumptionInMonth", value, MonthFormat.ShortWithYear(_localizer, shown.Year, shown.Month));
            }

            var change = trend.ChangePercent is { } percent
                ? $"{percent.ToString("+0;-0;0", CultureInfo.CurrentCulture)}{BillText.NonBreakingSpace}%"
                : string.Empty;
            var rise = _localizer["OverviewConsumptionRise"];
            var description = change.Length > 0
                ? _localizer.Get("OverviewConsumptionRowSummary", name, value, change) + (trend.IsRise ? ". " + rise : string.Empty)
                : $"{name}: {value}";
            var line = trend.ShowLine
                ? new SparklineData(trend.Values, trend.Approximate, ReportChartBuilder.Palette[i % ReportChartBuilder.Palette.Count])
                : null;
            rows.Add(new ConsumptionRowViewModel(name, value, change, trend.IsRise, rise, line, description, () => RunSafeAsync(OpenConsumptionAsync)));
        }

        ConsumptionRows = rows;
    }

    /// <summary>
    /// A tap on a consumption row: the Reports screen with the consumption report of the last twelve complete months (and of the
    /// chosen property). The report lists every service of the property; the tapped one is one of its columns.
    /// </summary>
    private Task OpenConsumptionAsync()
    {
        if (_reportState is { } state)
        {
            var last = YearMonth.From(_clock.Today).AddMonths(-1);
            state.Kind = ReportKind.Consumption;
            state.Preset = ReportPreset.Custom;
            state.From = last.AddMonths(1 - OverviewCharts.ConsumptionMonths);
            state.To = last;
            state.Properties = _propertyFilter is { } property ? [property] : null;
            state.ConsumptionGrouping = ConsumptionGrouping.Resource;
            state.Rows = ReportRows.Months;
            state.YearOverYear = false;
            state.ChangedExternally = true;
        }

        return _navigation.GoToAsync(Routes.Reports);
    }
}
