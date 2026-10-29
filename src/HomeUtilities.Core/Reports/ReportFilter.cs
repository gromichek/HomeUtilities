using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Reports;

/// <summary>What a report is calculated for.</summary>
/// <param name="From">First month (inclusive).</param>
/// <param name="To">Last month (inclusive).</param>
/// <param name="PropertyIds">The properties; null or empty = all.</param>
/// <param name="CategoryIds">The categories (costs only); null or empty = all.</param>
/// <param name="Rows">Months or years as rows.</param>
/// <param name="YearOverYear">Costs and consumption: the months of the year as rows and one column per year.</param>
/// <param name="Grouping">Costs: what the columns are grouped by.</param>
/// <param name="Measure">The figure shown: costs and unit cost Gross/Net/Vat, calculated vs billed Difference/DifferencePercent/Calculated/Billed/QuantityDifference.</param>
/// <param name="ConsumptionGrouping">Consumption: what the columns are grouped by (resource, contract or meter).</param>
public sealed record ReportFilter(
    YearMonth From,
    YearMonth To,
    IReadOnlyCollection<Guid>? PropertyIds = null,
    IReadOnlyCollection<Guid>? CategoryIds = null,
    ReportRows Rows = ReportRows.Months,
    bool YearOverYear = false,
    CostGrouping Grouping = CostGrouping.Category,
    ReportMeasure Measure = ReportMeasure.Gross,
    ConsumptionGrouping ConsumptionGrouping = ConsumptionGrouping.Resource)
{
    /// <summary>Every month of the range, oldest first; empty if <see cref="To"/> is before <see cref="From"/>.</summary>
    public IReadOnlyList<YearMonth> Months()
    {
        var count = From.MonthsUntil(To) + 1;
        return count <= 0 ? [] : [.. Enumerable.Range(0, count).Select(From.AddMonths)];
    }

    /// <summary>True if the month lies in the range.</summary>
    public bool Contains(YearMonth month) => From <= month && month <= To;
}
