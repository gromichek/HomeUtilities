using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;
using static HomeUtilities.Core.Tests.Reports.ReportScenario;

namespace HomeUtilities.Core.Tests.Reports;

/// <summary>
/// Contracts whose quantity comes from the meters: the owner has the bills of the past but no historic tariffs, so there is nothing to
/// calculate with, yet bill / metered quantity still says what a cubic metre cost. Equalized months are reported by the tariff.
/// </summary>
public class MeteredContractReportTests
{
    private static readonly YearMonth Sep = new(2026, 9);
    private static readonly YearMonth Oct = new(2026, 10);

    /// <summary>Gas contract without tariff components; meter 100.000 (31 Aug) -> 112.500 (30 Sep) = 12.500 m3; bill 18.75.</summary>
    private static (ReportScenario S, Contract Gas) GasWithoutTariff()
    {
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", service: ServiceKind.Gas);
        s.AddMeter(gas, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 112.5m));
        s.AddBill(gas, 18.75m, Sep);
        return (s, gas);
    }

    [Fact]
    public void Unit_cost_without_a_tariff_is_the_bill_per_metered_unit()
    {
        // 18.75 EUR / 12.500 m3 = 1.50 EUR/m3
        var (s, gas) = GasWithoutTariff();

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep));

        var column = Assert.Single(table.Columns);
        Assert.Equal(("Gas", MeterUnit.M3, ReportValueKind.UnitPrice), (column.Name, column.Unit, column.ValueKind));
        Assert.Equal(1.50m, table.Rows[0].Cells[0].Value);
        Assert.False(table.Rows[0].Cells[0].IsApproximate);
        Assert.Equal(gas.Id, table.Rows[0].Cells[0].Drill!.ContractId);
    }

    [Fact]
    public void A_month_without_new_readings_is_marked_and_a_month_without_a_bill_is_left_empty()
    {
        // October: billed 20.00 but no reading since 30 Sep -> marked. November: readings but no bill and no tariff -> nothing.
        var (s, gas) = GasWithoutTariff();
        s.AddBill(gas, 20m, Oct);
        s.Meters[0].Add(D(2026, 11, 30), 130m);

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, new YearMonth(2026, 11)));

        Assert.Equal(1.50m, table.Rows[0].Cells[0].Value);
        Assert.Null(table.Rows[1].Cells[0].Value);
        Assert.True(table.Rows[1].Cells[0].IsApproximate);
        Assert.True(table.Rows[2].Cells[0].IsEmpty);
    }

    [Fact]
    public void Bills_before_the_contract_start_still_count()
    {
        // An imported history older than the contract record: 2018 bill 9.00 / 6.000 m3 = 1.50.
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", service: ServiceKind.Gas);
        s.AddMeter(gas, (D(2018, 2, 28), 50m), (D(2018, 3, 31), 56m));
        s.Meters[0].InstalledOn = D(2018, 1, 1);
        s.Links[0].ValidFrom = D(2018, 1, 1);
        s.AddBill(gas, 9m, new YearMonth(2018, 3));

        var table = ReportBuilder.UnitCost(s.Data, Filter(new YearMonth(2018, 3), new YearMonth(2018, 3)));

        Assert.Equal(1.50m, table.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void A_tariff_without_a_rate_for_the_month_falls_back_to_the_readings()
    {
        // The rate starts on 2026-01-01; March 2018 has per-unit lines without a price. 9.00 / 6.000 m3 = 1.50.
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", service: ServiceKind.Gas);
        gas.ValidFrom = D(2018, 1, 1);
        s.AddPerUnit(gas, "Gas", 1.20m);
        s.AddMeter(gas, (D(2018, 2, 28), 50m), (D(2018, 3, 31), 56m));
        s.Meters[0].InstalledOn = D(2018, 1, 1);
        s.Links[0].ValidFrom = D(2018, 1, 1);
        s.AddBill(gas, 9m, new YearMonth(2018, 3));

        var table = ReportBuilder.UnitCost(s.Data, Filter(new YearMonth(2018, 3), new YearMonth(2018, 3)));

        Assert.Equal(1.50m, table.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void Contracts_with_and_without_a_tariff_are_reported_together()
    {
        // Water (tariff 3.00 net, 12.150 m3): 46.02 / 12.150 = 3.79. Gas (no tariff, 12.500 m3): 18.75 / 12.500 = 1.50.
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        s.AddPerUnit(water, "Water", 3.00m);
        s.AddMeter(water, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 112.15m));
        s.AddBill(water, 46.02m, Sep);
        var gas = s.AddContract("Gas", service: ServiceKind.Gas);
        s.AddMeter(gas, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 112.5m));
        s.AddBill(gas, 18.75m, Sep);

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep));

        Assert.Equal(["Gas", "Water supply"], table.Columns.Select(c => c.Name));
        Assert.Equal([1.50m, 3.79m], table.Rows[0].Cells.Select(c => c.Value));
    }

    [Fact]
    public void A_contract_metered_in_two_units_gets_a_column_per_unit()
    {
        // Heat: 12.500 m3 and 80.000 kWh in the month, bill 20.00 -> 1.60 EUR/m3 and 0.25 EUR/kWh.
        var s = new ReportScenario();
        var heat = s.AddContract("Heat", service: ServiceKind.Gas);
        s.AddMeter(heat, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 112.5m));
        var power = s.AddMeter(heat, (D(2026, 8, 31), 1000m), (D(2026, 9, 30), 1080m));
        power.Unit = MeterUnit.KWh;
        s.AddBill(heat, 20m, Sep);

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep));

        Assert.Equal([MeterUnit.M3, MeterUnit.KWh], table.Columns.Select(c => c.Unit));
        Assert.Equal([1.60m, 0.25m], table.Rows[0].Cells.Select(c => c.Value));
        Assert.Equal(2, table.Columns.Select(c => c.Key).Distinct().Count());
    }

    [Fact]
    public void Calculated_against_billed_without_a_tariff_shows_the_bill_and_no_calculation()
    {
        var (s, _) = GasWithoutTariff();

        var billed = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.Billed));
        var calculated = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.Calculated));
        var difference = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep));
        var percent = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.DifferencePercent));

        Assert.Equal(18.75m, billed.Rows[0].Cells[0].Value);
        Assert.False(billed.Rows[0].Cells[0].IsApproximate);
        Assert.Null(calculated.Rows[0].Cells[0].Value);
        Assert.Null(difference.Rows[0].Cells[0].Value);
        Assert.Null(percent.Rows[0].Cells[0].Value);
        Assert.Equal(18.75m, billed.Totals!.Total!.Value);
    }

    [Fact]
    public void The_quantity_difference_without_a_tariff_uses_the_meters()
    {
        // 12.600 on the bill - 12.500 metered = 0.100 m3
        var (s, _) = GasWithoutTariff();
        s.Bills[0].ConsumptionOnBill = 12.6m;

        var table = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.QuantityDifference));

        Assert.Equal(0.1m, table.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void A_contract_without_meters_and_without_a_tariff_is_still_left_out()
    {
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", service: ServiceKind.Gas);
        s.AddBill(gas, 18.75m, Sep);

        Assert.Empty(ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep)).Columns);
        Assert.Empty(ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep)).Columns);
    }

    // ---- Equalized months -----------------------------------------------------------------------------------------

    /// <summary>
    /// Equalized gas from 1 Sep 2026, plan 25.00 per month, tariff 1.50 net per m3 (VAT 21%); meter 100.000 -> 112.500 = 12.500 m3.
    /// By tariff: 12.500 * 1.50 = 18.75 + VAT 3.94 = 22.69. September is billed 25.00 as an equalized bill.
    /// </summary>
    private static (ReportScenario S, Contract Gas) EqualizedGas(bool withTariff = true)
    {
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", mode: BillingMode.Equalized, service: ServiceKind.Gas);
        if (withTariff)
        {
            s.AddPerUnit(gas, "Gas", 1.50m);
        }

        s.AddMeter(gas, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 112.5m));
        s.AddBill(gas, 25m, Sep).Kind = BillKind.Equalized;
        s.AddPlan(gas, D(2026, 9, 1), 25m);
        return (s, gas);
    }

    [Fact]
    public void Unit_cost_of_an_equalized_month_is_the_tariff_charge_per_unit_and_marked()
    {
        // Net: 18.75 / 12.500 = 1.50. Gross: 22.69 / 12.500 = 1.8152 -> 1.82. Both marked as approximate.
        var (s, _) = EqualizedGas();

        var net = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.Net)).Rows[0].Cells[0];
        var gross = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep)).Rows[0].Cells[0];

        Assert.Equal((1.50m, true), (net.Value, net.IsApproximate));
        Assert.Equal((1.82m, true), (gross.Value, gross.IsApproximate));
    }

    [Fact]
    public void Unit_cost_of_an_equalized_month_without_a_tariff_uses_the_plan_amount()
    {
        // 25.00 (plan) / 12.500 m3 = 2.00, marked.
        var (s, _) = EqualizedGas(withTariff: false);

        var cell = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep)).Rows[0].Cells[0];

        Assert.Equal((2.00m, true), (cell.Value, cell.IsApproximate));
    }

    [Fact]
    public void Calculated_against_billed_of_an_equalized_month_shows_where_the_settlement_goes()
    {
        // Billed 25.00 (the plan) - calculated 22.69 = +2.31: the supplier will refund about that much.
        var (s, _) = EqualizedGas();

        var difference = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep)).Rows[0].Cells[0];
        var calculated = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.Calculated)).Rows[0].Cells[0];
        var billed = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.Billed)).Rows[0].Cells[0];

        Assert.Equal(2.31m, difference.Value);
        Assert.Equal(22.69m, calculated.Value);
        Assert.Equal(25m, billed.Value);
    }

    [Fact]
    public void A_settlement_is_billed_in_its_own_month_with_no_calculation()
    {
        // October: no readings besides September's; the settlement bill -3.00 is shown as billed, without a difference.
        var (s, gas) = EqualizedGas();
        s.AddBill(gas, -3m, Oct).Kind = BillKind.Settlement;

        var billed = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Oct, b => b.Measure = ReportMeasure.Billed));
        var difference = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Oct));

        Assert.Equal([25m, -3m], billed.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal(22m, billed.Totals!.Total!.Value);
        Assert.Equal([2.31m, null], difference.Rows.Select(r => r.Cells[0].Value));
    }

    [Fact]
    public void Equalized_contracts_whose_every_month_is_covered_by_a_plan_are_reported()
    {
        // The owner's gas: Equalized from the first month of the history, so no month is "before the plans".
        var (s, _) = EqualizedGas();
        s.Plans[0].ValidFrom = D(2020, 1, 1);

        Assert.Single(ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep)).Columns);
        Assert.Single(ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep)).Columns);
    }

    // ---- Bills outside the contract's dates ----------------------------------------------------------------------

    [Fact]
    public void Months_before_the_contract_start_are_shown_but_marked()
    {
        // The contract starts on 2020-01-01 (default) but a March 2018 bill exists: 9.00 / 6.000 m3 = 1.50, marked.
        var s = new ReportScenario();
        var gas = s.AddContract("Gas", service: ServiceKind.Gas);
        s.AddMeter(gas, (D(2018, 2, 28), 50m), (D(2018, 3, 31), 56m));
        s.Meters[0].InstalledOn = D(2018, 1, 1);
        s.Links[0].ValidFrom = D(2018, 1, 1);
        s.AddBill(gas, 9m, new YearMonth(2018, 3));
        s.AddBill(gas, 9m, new YearMonth(2020, 3));
        s.Meters[0].Add(D(2020, 2, 29), 100m);
        s.Meters[0].Add(D(2020, 3, 31), 106m);

        var unitCost = ReportBuilder.UnitCost(s.Data, Filter(new YearMonth(2018, 3), new YearMonth(2020, 3), b => b.Rows = ReportRows.Months));
        var billed = ReportBuilder.CalculatedVsBilled(s.Data, Filter(new YearMonth(2018, 3), new YearMonth(2018, 3), b => b.Measure = ReportMeasure.Billed));

        Assert.Equal((1.50m, true), (unitCost.Rows[0].Cells[0].Value, unitCost.Rows[0].Cells[0].IsApproximate));
        Assert.Equal((1.50m, false), (unitCost.Rows[^1].Cells[0].Value, unitCost.Rows[^1].Cells[0].IsApproximate));
        Assert.Equal((9m, true), (billed.Rows[0].Cells[0].Value, billed.Rows[0].Cells[0].IsApproximate));
    }
}
