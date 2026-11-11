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

    private static EqualizedPlan PlanWithCarryOver(string from, decimal amount, decimal carryOver, string? to = null)
    {
        var plan = Plan(from, amount, to);
        plan.CarryOverMonthly = carryOver;
        return plan;
    }

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

    // ---- Carry-over from the previous period (T-046) ------------------------------------------------------------------

    [Fact]
    public void Carry_over_refund_is_taken_out_of_the_billed_amount()
    {
        // plan 30,00 includes the carry-over -4,00; billed 30,00; actual 36,50; 36,50 - (30,00 - (-4,00)) = +2,50 (old rule: +6,50)
        var result = EqualizedSettlement.Calculate(
            [PlanWithCarryOver("2026-06-01", 30m, -4m)], [Billed(June, 30m)], Actuals((June, 36.50m, true)), June, June);

        var month = Assert.Single(result.Months);
        Assert.Equal((30m, -4m, 2.50m), (month.Billed, month.CarryOver, month.Difference));
        Assert.Equal((30m, -4m, 2.50m), (result.TotalBilled, result.TotalCarryOver, result.Expected));
    }

    [Fact]
    public void Carry_over_surcharge_is_taken_out_of_the_billed_amount()
    {
        // plan 40,00 includes the carry-over +1,50; billed 40,00; actual 37,00; 37,00 - (40,00 - 1,50) = -1,50
        var result = EqualizedSettlement.Calculate(
            [PlanWithCarryOver("2026-06-01", 40m, 1.50m)], [Billed(June, 40m)], Actuals((June, 37m, true)), June, June);

        Assert.Equal(-1.50m, result.Months[0].Difference);
        Assert.Equal(-1.50m, result.Expected);
    }

    [Fact]
    public void Twelve_months_with_a_carry_over_expect_actual_minus_billed_plus_the_refund()
    {
        // plan 30,00 incl. -4,00 for 12 months: billed 360,00; actual 33 + 35 + ... + 55 = 12 x 44 = 528,00
        // expected = 528,00 - (360,00 + 12 x 4,00) = 528,00 - 408,00 = +120,00 (old rule: +168,00)
        var start = new YearMonth(2026, 7);
        var bills = Enumerable.Range(0, 12).Select(i => Billed(start.AddMonths(i), 30m)).ToList();
        var actual = Actuals([.. Enumerable.Range(0, 12).Select(i => (start.AddMonths(i), 33m + 2 * i, true))]);

        var result = EqualizedSettlement.Calculate(
            [PlanWithCarryOver("2026-07-01", 30m, -4m)], bills, actual, start, start.AddMonths(11));

        Assert.Equal((360m, 528m, -48m), (result.TotalBilled, result.TotalActual, result.TotalCarryOver));
        Assert.Equal(120m, result.Expected);
        Assert.Equal(120m, result.Months[^1].RunningTotal);
    }

    [Fact]
    public void Plan_versions_with_different_carry_overs_apply_per_month_and_a_month_without_one_is_unchanged()
    {
        // 06: plan 40,00 without carry-over: 45,00 - 40,00 = +5,00
        // 07: plan 30,00 with -4,00:        36,00 - 34,00 = +2,00
        // 08: plan 32,00 with +1,00 (new version): 30,00 - 31,00 = -1,00
        var plans = new[]
        {
            Plan("2026-06-01", 40m, "2026-06-30"),
            PlanWithCarryOver("2026-07-01", 30m, -4m, "2026-07-31"),
            PlanWithCarryOver("2026-08-01", 32m, 1m),
        };
        var bills = new[] { Billed(June, 40m), Billed(June.AddMonths(1), 30m), Billed(June.AddMonths(2), 32m) };
        var actual = Actuals((June, 45m, true), (June.AddMonths(1), 36m, true), (June.AddMonths(2), 30m, true));

        var result = EqualizedSettlement.Calculate(plans, bills, actual, June, June.AddMonths(2));

        Assert.Equal([5m, 2m, -1m], result.Months.Select(m => m.Difference!.Value));
        Assert.Equal([null, -4m, 1m], result.Months.Select(m => m.CarryOver));
        Assert.Equal(6m, result.Expected);
    }

    [Fact]
    public void A_month_without_a_bill_does_not_count_a_carry_over()
    {
        // nothing billed for 06, so nothing was paid and no carry-over was included
        var result = EqualizedSettlement.Calculate(
            [PlanWithCarryOver("2026-06-01", 30m, -4m)], [], Actuals((June, 36m, true)), June, June);

        Assert.Null(result.Months[0].CarryOver);
        Assert.Equal(36m, result.Expected);
    }

    [Fact]
    public void An_incomplete_month_leaves_the_carry_over_out_of_the_totals()
    {
        var result = EqualizedSettlement.Calculate(
            [PlanWithCarryOver("2026-06-01", 30m, -4m)], [Billed(June, 30m)], Actuals((June, 10m, false)), June, June);

        Assert.Equal((0m, 0m), (result.TotalBilled, result.TotalCarryOver));
        Assert.Equal(0m, result.Expected);
    }

    [Fact]
    public void A_settlement_bill_of_zero_starts_the_next_period_after_it()
    {
        var plans = new[] { PlanWithCarryOver("2026-07-01", 30m, -4m) };
        var bills = new[] { Billed(June, 0m, BillKind.Settlement), Billed(June.AddMonths(1), 30m) };

        var (from, to) = EqualizedSettlement.Range(plans, bills, November);

        Assert.Equal((new YearMonth(2026, 7), November), (from, to));
    }
}
