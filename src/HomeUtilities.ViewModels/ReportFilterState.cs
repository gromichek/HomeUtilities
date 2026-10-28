using HomeUtilities.Core.Common;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels;

/// <summary>The period presets of the Reports screen.</summary>
public enum ReportPreset
{
    /// <summary>January to December of the current year.</summary>
    ThisYear = 0,

    /// <summary>The current month and the 11 before it.</summary>
    Last12Months = 1,

    /// <summary>January to December of the year before.</summary>
    LastYear = 2,

    /// <summary>From the first month with data to the last.</summary>
    All = 3,

    /// <summary>A range set with the month buttons.</summary>
    Custom = 4,
}

/// <summary>Resolves a <see cref="ReportPreset"/> to months.</summary>
public static class ReportPeriods
{
    /// <summary>
    /// The months of a preset. "This year" is the whole calendar year (like the year filter of the Bills screen, so the totals
    /// agree); "All" ends at the later of the current month and the last bill.
    /// </summary>
    public static (YearMonth From, YearMonth To) Resolve(ReportPreset preset, DateOnly today, YearMonth? earliest, YearMonth? latest)
    {
        var current = YearMonth.From(today);
        switch (preset)
        {
            case ReportPreset.Last12Months:
                return (current.AddMonths(-11), current);
            case ReportPreset.LastYear:
                return (new YearMonth(today.Year - 1, 1), new YearMonth(today.Year - 1, 12));
            case ReportPreset.All:
                var to = latest is { } last && last > current ? last : current;
                return (earliest is { } first && first < to ? first : to, to);
            default:
                return (new YearMonth(today.Year, 1), new YearMonth(today.Year, 12));
        }
    }
}

/// <summary>The filters of the Reports screen, remembered for the running session. Registered as a singleton.</summary>
public sealed class ReportFilterState
{
    /// <summary>The report shown.</summary>
    public ReportKind Kind { get; set; } = ReportKind.Costs;

    /// <summary>The period preset.</summary>
    public ReportPreset Preset { get; set; } = ReportPreset.ThisYear;

    /// <summary>First month of a custom range.</summary>
    public YearMonth? From { get; set; }

    /// <summary>Last month of a custom range.</summary>
    public YearMonth? To { get; set; }

    /// <summary>The selected properties; null = the properties that are not archived.</summary>
    public HashSet<Guid>? Properties { get; set; }

    /// <summary>The selected categories (costs); null = all.</summary>
    public HashSet<Guid>? Categories { get; set; }

    /// <summary>Months or years as rows.</summary>
    public ReportRows Rows { get; set; } = ReportRows.Months;

    /// <summary>Year over year.</summary>
    public bool YearOverYear { get; set; }

    /// <summary>Costs: what the columns are grouped by.</summary>
    public CostGrouping Grouping { get; set; } = CostGrouping.Category;

    /// <summary>Costs and unit cost: gross, net or VAT.</summary>
    public ReportMeasure CostMeasure { get; set; } = ReportMeasure.Gross;

    /// <summary>Calculated vs billed: the figure shown.</summary>
    public ReportMeasure CompareMeasure { get; set; } = ReportMeasure.Difference;

    /// <summary>True = the chart is shown instead of the table (on narrow screens; wide screens show both).</summary>
    public bool ShowChart { get; set; }

    /// <summary>Consumption: one column per meter.</summary>
    public bool PerMeter { get; set; }
}
