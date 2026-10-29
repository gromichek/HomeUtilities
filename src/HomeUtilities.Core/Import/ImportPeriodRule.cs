using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>How the date in the period column of a sheet row is turned into the billing period of the row.</summary>
public enum ImportPeriodRule
{
    /// <summary>The period is the month of the date.</summary>
    MonthOfDate = 0,

    /// <summary>
    /// The period is found like the charges find the readings of a period: a date inside the contract's reading window belongs to
    /// that window's month; a late date (after the window end, before the next window start) to the month of the window before it.
    /// Without a reading window it works like <see cref="MonthOfDate"/>.
    /// </summary>
    ReadingWindow = 1,

    /// <summary>The date is the 1st of the month after the service month (heating bills, readings taken on the 1st).</summary>
    PreviousMonth = 2,

    /// <summary>The previous month if the day of the date is at most the day limit, otherwise the month of the date.</summary>
    PreviousMonthIfDayUpTo = 3,

    /// <summary>The date is in the month before the service month (prepaid services).</summary>
    NextMonth = 4,
}

/// <summary>A change of the period rule that applies to the rows dated on or after <see cref="FromDate"/>.</summary>
public sealed class PeriodRuleChange
{
    /// <summary>First date the rule applies to.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>The rule.</summary>
    public ImportPeriodRule Rule { get; set; }

    /// <summary>Day limit of <see cref="ImportPeriodRule.PreviousMonthIfDayUpTo"/>.</summary>
    public int DayLimit { get; set; } = ImportPeriodResolver.DefaultDayLimit;
}

/// <summary>
/// The owner's correction of one row, found again by the date in its period column (so a re-import keeps it): another period,
/// a range of months the bill covers, and a remark for the bill's notes.
/// </summary>
public sealed class ImportRowOverride
{
    /// <summary>The date in the period column of the row.</summary>
    public DateOnly RowDate { get; set; }

    /// <summary>A day of the month that replaces the computed period; null = keep the computed period.</summary>
    public DateOnly? Period { get; set; }

    /// <summary>A day of the last month the bill covers (the bill then covers <see cref="Period"/> … this month); null = one month.</summary>
    public DateOnly? CoversUntil { get; set; }

    /// <summary>Text added to the bill's notes (e.g. "incl. connection fee").</summary>
    public string? Remark { get; set; }

    /// <summary>True when nothing is overridden.</summary>
    public bool IsEmpty => Period is null && CoversUntil is null && string.IsNullOrWhiteSpace(Remark);
}

/// <summary>Header text and column letter of one mapped field as read from the workbook; the base of an export in the same layout.</summary>
public sealed class ImportColumnHeader
{
    /// <summary>Field name: <c>Period</c>, <c>Total</c>, <c>TotalFallback</c>, <c>Net</c>, <c>Vat</c>, <c>Paid</c>, <c>Reading:{register id}</c>…</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Column letter (A, B, … AA).</summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>Text of the header cell.</summary>
    public string Header { get; set; } = string.Empty;
}

/// <summary>Turns a date into a billing period under an <see cref="ImportPeriodRule"/>.</summary>
public static class ImportPeriodResolver
{
    /// <summary>Default day limit of <see cref="ImportPeriodRule.PreviousMonthIfDayUpTo"/>.</summary>
    public const int DefaultDayLimit = 10;

    /// <summary>The billing period of a row dated <paramref name="date"/>.</summary>
    /// <param name="date">The date in the period column.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="dayLimit">Day limit of <see cref="ImportPeriodRule.PreviousMonthIfDayUpTo"/>.</param>
    /// <param name="contract">The contract whose reading window is used by <see cref="ImportPeriodRule.ReadingWindow"/>.</param>
    public static YearMonth Resolve(DateOnly date, ImportPeriodRule rule, int dayLimit, Contract? contract)
    {
        var month = YearMonth.From(date);
        return rule switch
        {
            ImportPeriodRule.PreviousMonth => month.AddMonths(-1),
            ImportPeriodRule.NextMonth => month.AddMonths(1),
            ImportPeriodRule.PreviousMonthIfDayUpTo => date.Day <= dayLimit ? month.AddMonths(-1) : month,
            ImportPeriodRule.ReadingWindow => FromWindow(date, contract) ?? month,
            _ => month,
        };
    }

    private static YearMonth? FromWindow(DateOnly date, Contract? contract)
    {
        if (contract is null || contract.GetReadingWindow(YearMonth.From(date)) is null)
        {
            return null;
        }

        var month = YearMonth.From(date);
        var candidates = new[] { month.AddMonths(-1), month, month.AddMonths(1) };
        foreach (var period in candidates)
        {
            var (from, to) = contract.GetReadingWindow(period)!.Value;
            if (from <= date && date <= to)
            {
                return period;
            }
        }

        // A late reading: after the window of a period and before the window of the next one.
        foreach (var period in candidates)
        {
            var (_, to) = contract.GetReadingWindow(period)!.Value;
            var nextFrom = contract.GetReadingWindow(period.AddMonths(1))!.Value.From;
            if (date > to && date < nextFrom)
            {
                return period;
            }
        }

        return null;
    }
}
