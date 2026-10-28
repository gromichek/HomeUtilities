using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>
/// Works out which periods a bill template is due for and the dates of the bills. Pure logic: the caller supplies today's
/// date and the periods that already have a bill.
/// </summary>
public static class BillTemplateScheduler
{
    /// <summary>Safety limit of the periods produced by one call.</summary>
    private const int MaxPeriods = 2400;

    /// <summary>
    /// The last period drafts may be created for today: the previous month for a bill in arrears (the month is over),
    /// the next month for a prepaid one.
    /// </summary>
    public static YearMonth Horizon(BillingTiming timing, DateOnly today) =>
        YearMonth.From(today).AddMonths(timing == BillingTiming.InAdvance ? 1 : -1);

    /// <summary>All periods of a schedule from its start up to and including <paramref name="upTo"/> (and its end month).</summary>
    public static IReadOnlyList<YearMonth> ScheduledPeriods(Schedule schedule, YearMonth upTo)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var result = new List<YearMonth>();
        foreach (var period in Periods(schedule))
        {
            if (period > upTo || result.Count >= MaxPeriods)
            {
                break;
            }

            result.Add(period);
        }

        return result;
    }

    /// <summary>
    /// The periods to create drafts for today, oldest first: scheduled periods up to the horizon, after the last
    /// generated one, and without a bill yet. Inactive, deleted and <see cref="ScheduleKind.Once"/> templates have none.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="today">Today's date.</param>
    /// <param name="existing">Periods that already have a bill of this template, deleted bills included.</param>
    public static IReadOnlyList<YearMonth> DuePeriods(BillTemplate template, DateOnly today, IEnumerable<YearMonth>? existing = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (!template.IsActive || template.IsDeleted || template.Schedule.Kind == ScheduleKind.Once)
        {
            return [];
        }

        var taken = existing?.ToHashSet() ?? [];
        return
        [
            .. ScheduledPeriods(template.Schedule, Horizon(template.Timing, today))
                .Where(p => (template.LastGeneratedPeriod is not { } last || p > last) && !taken.Contains(p)),
        ];
    }

    /// <summary>The next scheduled periods after the horizon (what is not due yet), for the preview in the editor.</summary>
    public static IReadOnlyList<YearMonth> UpcomingPeriods(BillTemplate template, DateOnly today, int count)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Schedule.Kind == ScheduleKind.Once)
        {
            return [];
        }

        var horizon = Horizon(template.Timing, today);
        return
        [
            .. Periods(template.Schedule)
                .SkipWhile(p => p <= horizon)
                .Take(count),
        ];
    }

    /// <summary>
    /// The day the bill of a period is issued: <see cref="Schedule.DayOfMonth"/> in the month before the period for a
    /// prepaid template, after it otherwise; a day beyond the month's length becomes its last day. Null without a day.
    /// </summary>
    public static DateOnly? IssueDate(BillTemplate template, YearMonth period)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Schedule.DayOfMonth is not { } day)
        {
            return null;
        }

        var month = period.AddMonths(template.Timing == BillingTiming.InAdvance ? -1 : 1);
        return new DateOnly(month.Year, month.Month, Math.Clamp(day, 1, DateTime.DaysInMonth(month.Year, month.Month)));
    }

    /// <summary>The issue date plus <see cref="BillTemplate.DueDayOffset"/>; null if either is missing.</summary>
    public static DateOnly? DueDate(BillTemplate template, YearMonth period) =>
        template.DueDayOffset is { } offset && IssueDate(template, period) is { } issued ? issued.AddDays(offset) : null;

    private static IEnumerable<YearMonth> Periods(Schedule schedule)
    {
        if (schedule.Kind == ScheduleKind.Once || schedule.StartMonth is not { } start)
        {
            yield break;
        }

        var step = schedule.Kind switch
        {
            ScheduleKind.Monthly => 1,
            ScheduleKind.EveryNMonths when schedule.EveryMonths is >= 1 => schedule.EveryMonths.Value,
            ScheduleKind.Yearly => 12,
            _ => 0,
        };
        if (step == 0)
        {
            yield break;
        }

        for (var period = start; period.Year < 2190; period = period.AddMonths(step))
        {
            if (schedule.EndMonth is { } end && period > end)
            {
                yield break;
            }

            yield return period;
        }
    }
}
