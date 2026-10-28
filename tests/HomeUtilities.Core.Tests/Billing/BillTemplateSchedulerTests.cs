using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Billing;

public class BillTemplateSchedulerTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static BillTemplate Template(
        ScheduleKind kind, YearMonth start, BillingTiming timing = BillingTiming.InArrears, int? every = null, YearMonth? end = null, int? day = null) =>
        new()
        {
            Name = "T",
            Timing = timing,
            Schedule = new Schedule { Kind = kind, StartMonth = start, EveryMonths = every, EndMonth = end, DayOfMonth = day },
        };

    private static string[] Text(IEnumerable<YearMonth> periods) => [.. periods.Select(p => p.ToString())];

    [Fact]
    public void Monthly_InArrears_StopsAtThePreviousMonth()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1));

        // today 15.10.2026, in arrears: January … September
        Assert.Equal(
            ["2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06", "2026-07", "2026-08", "2026-09"],
            Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void Monthly_InAdvance_ReachesTheNextMonth()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 8), BillingTiming.InAdvance);

        // today 15.10.2026, prepaid: August … November
        Assert.Equal(["2026-08", "2026-09", "2026-10", "2026-11"], Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void EveryThreeMonths_FromFebruary_GivesFebMayAugNov()
    {
        var template = Template(ScheduleKind.EveryNMonths, new YearMonth(2026, 2), BillingTiming.InAdvance, every: 3);

        Assert.Equal(["2026-02", "2026-05", "2026-08", "2026-11"], Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void EveryThreeMonths_InArrears_StopsBeforeNovember()
    {
        var template = Template(ScheduleKind.EveryNMonths, new YearMonth(2026, 2), every: 3);

        Assert.Equal(["2026-02", "2026-05", "2026-08"], Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void Yearly_FromMarch2025_GivesTwoPeriods()
    {
        var template = Template(ScheduleKind.Yearly, new YearMonth(2025, 3));

        Assert.Equal(["2025-03", "2026-03"], Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void EndMonth_CutsTheSchedule()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1), end: new YearMonth(2026, 3));

        Assert.Equal(["2026-01", "2026-02", "2026-03"], Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void LastGeneratedPeriod_IsRespected()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1));
        template.LastGeneratedPeriod = new YearMonth(2026, 7);

        Assert.Equal(["2026-08", "2026-09"], Text(BillTemplateScheduler.DuePeriods(template, Today)));
    }

    [Fact]
    public void ExistingPeriods_AreNotCreatedAgain()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 7));

        // 2026-08 already has a bill (a deleted draft counts too)
        Assert.Equal(["2026-07", "2026-09"], Text(BillTemplateScheduler.DuePeriods(template, Today, [new YearMonth(2026, 8)])));
    }

    [Fact]
    public void StartInTheFuture_HasNothingDue()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2027, 1));

        Assert.Empty(BillTemplateScheduler.DuePeriods(template, Today));
    }

    [Fact]
    public void OnceInactiveAndDeleted_HaveNothingDue()
    {
        var once = new BillTemplate { Schedule = new Schedule { Kind = ScheduleKind.Once } };
        var inactive = Template(ScheduleKind.Monthly, new YearMonth(2026, 1));
        inactive.IsActive = false;
        var deleted = Template(ScheduleKind.Monthly, new YearMonth(2026, 1));
        deleted.IsDeleted = true;

        Assert.Empty(BillTemplateScheduler.DuePeriods(once, Today));
        Assert.Empty(BillTemplateScheduler.DuePeriods(inactive, Today));
        Assert.Empty(BillTemplateScheduler.DuePeriods(deleted, Today));
    }

    [Fact]
    public void BrokenEveryNMonths_DoesNotLoopForever()
    {
        var template = Template(ScheduleKind.EveryNMonths, new YearMonth(2026, 1), every: null);

        Assert.Empty(BillTemplateScheduler.DuePeriods(template, Today));
    }

    [Fact]
    public void Upcoming_ListsThePeriodsAfterTheHorizon()
    {
        var monthly = Template(ScheduleKind.Monthly, new YearMonth(2026, 1));
        var yearly = Template(ScheduleKind.Yearly, new YearMonth(2025, 3));

        // in arrears the horizon is 2026-09: 10, 11, 12 follow; the yearly one next comes in March 2027
        Assert.Equal(["2026-10", "2026-11", "2026-12"], Text(BillTemplateScheduler.UpcomingPeriods(monthly, Today, 3)));
        Assert.Equal(["2027-03", "2028-03"], Text(BillTemplateScheduler.UpcomingPeriods(yearly, Today, 2)));
    }

    [Fact]
    public void Upcoming_StopsAtTheEndMonth()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1), end: new YearMonth(2026, 11));

        Assert.Equal(["2026-10", "2026-11"], Text(BillTemplateScheduler.UpcomingPeriods(template, Today, 3)));
    }

    [Fact]
    public void IssueAndDueDate_InArrears_AreInTheMonthAfterThePeriod()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1), day: 5);
        template.DueDayOffset = 14;

        // period 2026-10, issued 05.11.2026, due 19.11.2026
        Assert.Equal(new DateOnly(2026, 11, 5), BillTemplateScheduler.IssueDate(template, new YearMonth(2026, 10)));
        Assert.Equal(new DateOnly(2026, 11, 19), BillTemplateScheduler.DueDate(template, new YearMonth(2026, 10)));
    }

    [Fact]
    public void IssueAndDueDate_InAdvance_AreInTheMonthBeforeThePeriod()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1), BillingTiming.InAdvance, day: 20);
        template.DueDayOffset = 10;

        // period 2027-01, issued 20.12.2026, due 30.12.2026
        Assert.Equal(new DateOnly(2026, 12, 20), BillTemplateScheduler.IssueDate(template, new YearMonth(2027, 1)));
        Assert.Equal(new DateOnly(2026, 12, 30), BillTemplateScheduler.DueDate(template, new YearMonth(2027, 1)));
    }

    [Fact]
    public void Day31_InFebruary_BecomesTheLastDay()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1), day: 31);

        // period 2027-01 in arrears is issued in February 2027 (28 days); period 2027-12 in 2028 January (31)
        Assert.Equal(new DateOnly(2027, 2, 28), BillTemplateScheduler.IssueDate(template, new YearMonth(2027, 1)));
        // leap year: period 2028-01 -> February 2028 has 29 days
        Assert.Equal(new DateOnly(2028, 2, 29), BillTemplateScheduler.IssueDate(template, new YearMonth(2028, 1)));
    }

    [Fact]
    public void WithoutDayOfMonth_ThereAreNoDates()
    {
        var template = Template(ScheduleKind.Monthly, new YearMonth(2026, 1));
        template.DueDayOffset = 14;

        Assert.Null(BillTemplateScheduler.IssueDate(template, new YearMonth(2026, 10)));
        Assert.Null(BillTemplateScheduler.DueDate(template, new YearMonth(2026, 10)));
    }
}
