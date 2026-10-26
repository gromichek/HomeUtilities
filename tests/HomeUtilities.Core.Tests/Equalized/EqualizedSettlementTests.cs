using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Equalized;

public class EqualizedSettlementTests
{
    private static readonly YearMonth June = new(2026, 6);
    private static readonly YearMonth November = new(2026, 11);

    private static EqualizedPlan Plan(string from, decimal amount, string? to = null) => new()
    {
        ValidFrom = DateOnly.Parse(from),
        ValidTo = to is null ? null : DateOnly.Parse(to),
        MonthlyAmount = amount,
    };

    private static Bill Billed(YearMonth period, decimal gross, BillKind kind = BillKind.Equalized)
    {
        var bill = BillFor(Contract(BillingMode.Equalized), gross, period);
        bill.Kind = kind;
        return bill;
    }

    private static Dictionary<YearMonth, CalculatedCharge> Actuals(params (YearMonth Month, decimal Gross, bool Complete)[] values) =>
        values.ToDictionary(v => v.Month, v => Charge(v.Gross, v.Complete));

    [Fact]
    public void Six_months_plan_40_actual_249_50_gives_a_surcharge_of_9_50()
    {
        // plan 40,00 x 6 = 240,00 billed; actual 35,20 + 31,10 + 38,40 + 44,90 + 52,30 + 47,60 = 249,50; 249,50 - 240,00 = +9,50
        var bills = Enumerable.Range(0, 6).Select(i => Billed(June.AddMonths(i), 40m)).ToList();
        var actual = Actuals(
            (June, 35.20m, true), (June.AddMonths(1), 31.10m, true), (June.AddMonths(2), 38.40m, true),
            (June.AddMonths(3), 44.90m, true), (June.AddMonths(4), 52.30m, true), (November, 47.60m, true));

        var result = EqualizedSettlement.Calculate([Plan("2026-06-01", 40m)], bills, actual, June, November);

        Assert.Equal(6, result.Months.Count);
        Assert.Equal(240m, result.TotalBilled);
        Assert.Equal(249.50m, result.TotalActual);
        Assert.Equal(9.50m, result.Expected);
        Assert.True(result.IsComplete);
        Assert.Equal(40m, result.Months[0].PlanAmount);
        Assert.Equal(-4.80m, result.Months[0].Difference);
        Assert.Equal(9.50m, result.Months[^1].RunningTotal);
    }

    [Fact]
    public void Actual_below_billed_gives_a_refund()
    {
        // billed 40,00 + 40,00 = 80,00; actual 30,00 + 38,00 = 68,00; 68,00 - 80,00 = -12,00
        var bills = new[] { Billed(June, 40m), Billed(June.AddMonths(1), 40m) };
        var actual = Actuals((June, 30m, true), (June.AddMonths(1), 38m, true));

        var result = EqualizedSettlement.Calculate([Plan("2026-06-01", 40m)], bills, actual, June, June.AddMonths(1));

        Assert.Equal(-12m, result.Expected);
        Assert.Equal([-10m, -12m], result.Months.Select(m => m.RunningTotal));
    }

    [Fact]
    public void Incomplete_and_missing_months_are_left_out_of_the_totals_and_counted()
    {
        // June complete: 35,20 - 40,00 = -4,80. July incomplete (billed 40,00 ignored), August has no charge at all.
        var bills = new[] { Billed(June, 40m), Billed(June.AddMonths(1), 40m), Billed(June.AddMonths(2), 40m) };
        var actual = Actuals((June, 35.20m, true), (June.AddMonths(1), 10m, false));

        var result = EqualizedSettlement.Calculate([Plan("2026-06-01", 40m)], bills, actual, June, June.AddMonths(2));

        Assert.Equal(2, result.MissingMonths);
        Assert.False(result.IsComplete);
        Assert.Equal(-4.80m, result.Expected);
        Assert.Equal(40m, result.TotalBilled);
        Assert.Equal(35.20m, result.TotalActual);
        Assert.False(result.Months[1].IsComplete);
        Assert.Null(result.Months[1].Difference);
        Assert.Equal(10m, result.Months[1].Actual);
        Assert.Null(result.Months[2].Actual);
        Assert.Equal(-4.80m, result.Months[2].RunningTotal);
    }

    [Fact]
    public void Only_equalized_bills_count_as_billed_and_deleted_ones_are_ignored()
    {
        var deleted = Billed(June, 40m);
        deleted.IsDeleted = true;
        var bills = new[] { Billed(June, 40m), Billed(June, 99m, BillKind.Settlement), Billed(June, 5m, BillKind.Regular), deleted };

        var result = EqualizedSettlement.Calculate([], bills, Actuals((June, 35m, true)), June, June);

        Assert.Equal(40m, result.Months[0].Billed);
        Assert.Equal(-5m, result.Expected);
    }

    [Fact]
    public void Two_equalized_bills_in_a_month_are_added()
    {
        var bills = new[] { Billed(June, 30m), Billed(June, 10m) };
        var result = EqualizedSettlement.Calculate([], bills, Actuals((June, 45m, true)), June, June);
        Assert.Equal((40m, 5m), (result.Months[0].Billed, result.Expected));
    }

    [Fact]
    public void Range_starts_after_the_last_settlement_bill()
    {
        // settlement of 2026-05 -> the next period starts in 2026-06
        var bills = new[] { Billed(new YearMonth(2026, 5), 12m, BillKind.Settlement), Billed(new YearMonth(2025, 11), 12m, BillKind.Settlement) };
        var (from, to) = EqualizedSettlement.Range([Plan("2025-01-01", 40m)], bills, new YearMonth(2026, 10));
        Assert.Equal((June, new YearMonth(2026, 10)), (from, to));
    }

    [Fact]
    public void Range_without_settlement_starts_at_the_first_plan()
    {
        var (from, to) = EqualizedSettlement.Range(
            [Plan("2026-03-15", 40m), Plan("2026-01-01", 35m, "2026-03-14")], [], new YearMonth(2026, 10));
        Assert.Equal((new YearMonth(2026, 1), new YearMonth(2026, 10)), (from, to));
    }

    [Fact]
    public void Range_without_plans_falls_back_to_the_first_equalized_bill_then_to_the_current_month()
    {
        var current = new YearMonth(2026, 10);
        Assert.Equal((new YearMonth(2026, 8), current), EqualizedSettlement.Range([], [Billed(new YearMonth(2026, 9), 40m), Billed(new YearMonth(2026, 8), 40m)], current));
        Assert.Equal((current, current), EqualizedSettlement.Range([], [], current));
    }

    [Fact]
    public void Range_never_starts_after_the_current_month()
    {
        var bills = new[] { Billed(new YearMonth(2026, 10), 5m, BillKind.Settlement) };
        var (from, to) = EqualizedSettlement.Range([], bills, new YearMonth(2026, 10));
        Assert.Equal((to, to), (from, to));
    }
}
