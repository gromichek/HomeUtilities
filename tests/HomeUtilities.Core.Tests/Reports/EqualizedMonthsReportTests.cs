using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;
using static HomeUtilities.Core.Tests.Reports.ReportScenario;

namespace HomeUtilities.Core.Tests.Reports;

/// <summary>
/// A contract that was billed by consumption until June 2026 and is equalized from July 2026 (plan 25.00 per month): the months
/// a plan covers are left out of the consumption reports, the earlier months stay in.
/// </summary>
public class EqualizedMonthsReportTests
{
    private static readonly YearMonth May = new(2026, 5);
    private static readonly YearMonth Jul = new(2026, 7);
    private static readonly YearMonth Aug = new(2026, 8);

    /// <summary>
    /// One per-unit component at 3.00 net (VAT 21%); meter 100 (30 Apr) -> 110 (31 May) -> ... -> 140 (31 Aug), 10 units a month.
    /// May is billed 36.30 (10 * 3.00 = 30.00 + VAT 6.30); August is billed 25.00 by the plan.
    /// </summary>
    private static (ReportScenario S, Contract Gas) Scenario()
    {
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", mode: BillingMode.Equalized);
        s.AddPerUnit(gas, "Gas", 3.00m);
        s.AddMeter(
            gas,
            (D(2026, 4, 30), 100m), (D(2026, 5, 31), 110m), (D(2026, 6, 30), 120m), (D(2026, 7, 31), 130m), (D(2026, 8, 31), 140m));
        s.AddBill(gas, 36.30m, May);
        s.AddBill(gas, 25m, Aug).Kind = BillKind.Equalized;
        s.AddPlan(gas, D(2026, 7, 1), 25m);
        return (s, gas);
    }

    [Fact]
    public void The_plan_decides_which_months_are_equalized()
    {
        var (s, gas) = Scenario();
        var data = s.Data;

        Assert.False(data.IsEqualized(gas, May));
        Assert.False(data.IsEqualized(gas, new YearMonth(2026, 6)));
        Assert.True(data.IsEqualized(gas, Jul));
        Assert.True(data.IsEqualized(gas, Aug));
        Assert.NotNull(data.Charge(gas, May));
        Assert.Null(data.Charge(gas, Aug));
    }

    [Fact]
    public void Unit_cost_has_the_months_before_the_first_plan()
    {
        // May: 36.30 / 10 = 3.63
        var (s, _) = Scenario();

        var table = ReportBuilder.UnitCost(s.Data, Filter(May, May));

        Assert.Equal("Gas", Assert.Single(table.Columns).Name);
        Assert.Equal(3.63m, table.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void Unit_cost_leaves_out_the_months_a_plan_covers()
    {
        var (s, _) = Scenario();

        Assert.Empty(ReportBuilder.UnitCost(s.Data, Filter(Aug, Aug)).Columns);

        var wide = ReportBuilder.UnitCost(s.Data, Filter(May, Aug));
        Assert.Equal(3.63m, wide.Rows[0].Cells[0].Value);
        Assert.Null(wide.Rows[2].Cells[0].Value);
        Assert.Null(wide.Rows[3].Cells[0].Value);
    }

    [Fact]
    public void Calculated_against_billed_has_the_months_before_the_first_plan_only()
    {
        // May: billed 36.30 - calculated 36.30 = 0.00. August is billed by the plan and not compared.
        var (s, _) = Scenario();

        var may = ReportBuilder.CalculatedVsBilled(s.Data, Filter(May, May));
        var august = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Aug, Aug));

        Assert.Equal(0m, may.Rows[0].Cells[0].Value);
        Assert.Empty(august.Columns);
    }

    [Fact]
    public void An_equalized_contract_without_plans_is_treated_as_billed_by_consumption()
    {
        var (s, _) = Scenario();
        s.Plans.Clear();

        var table = ReportBuilder.UnitCost(s.Data, Filter(May, May));

        Assert.Equal(3.63m, table.Rows[0].Cells[0].Value);
    }
}
