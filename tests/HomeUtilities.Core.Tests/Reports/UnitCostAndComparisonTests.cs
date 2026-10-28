using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;
using static HomeUtilities.Core.Tests.Reports.ReportScenario;

namespace HomeUtilities.Core.Tests.Reports;

public class UnitCostAndComparisonTests
{
    private static readonly YearMonth Sep = new(2026, 9);
    private static readonly YearMonth Oct = new(2026, 10);
    private static readonly YearMonth Nov = new(2026, 11);

    /// <summary>
    /// Water contract with one per-unit component at 3.00 net (VAT 21%), meter 100.000 (31 Aug) -> 112.150 (30 Sep) -> 120.150 (31 Oct)
    /// -> 129.150 (30 Nov): 12.150, 8.000, 9.000 m3. September is billed 46.02, October has no bill, November is billed 33.00.
    /// Calculated: Sep 12.150 * 3.00 = 36.45 + VAT 7.65 = 44.10; Oct 24.00 + 5.04 = 29.04; Nov 27.00 + 5.67 = 32.67.
    /// </summary>
    private static (ReportScenario S, Contract Water) WaterScenario()
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        s.AddPerUnit(water, "Water", 3.00m);
        s.AddMeter(water, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 112.15m), (D(2026, 10, 31), 120.15m), (D(2026, 11, 30), 129.15m));
        var sep = s.AddBill(water, 46.02m, Sep);
        sep.Net = 38.03m;
        sep.Vat = 7.99m;
        sep.ConsumptionOnBill = 12.35m;
        s.AddBill(water, 33m, Nov).ConsumptionOnBill = 9m;
        return (s, water);
    }

    [Fact]
    public void Unit_cost_is_the_bill_divided_by_the_metered_quantity()
    {
        // 46.02 EUR / 12.150 m3 = 3.7877 -> 3.79 EUR/m3
        var (s, water) = WaterScenario();

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep));

        var column = Assert.Single(table.Columns);
        Assert.Equal((ColumnSubject.Contract, "Water supply", MeterUnit.M3, ReportValueKind.UnitPrice), (column.Subject, column.Name, column.Unit, column.ValueKind));
        Assert.Equal(3.79m, table.Rows[0].Cells[0].Value);
        Assert.False(table.Rows[0].Cells[0].IsApproximate);
        Assert.Equal(water.Id, table.Rows[0].Cells[0].Drill!.ContractId);
    }

    [Fact]
    public void The_net_variant_excludes_vat()
    {
        // 38.03 / 12.150 = 3.1300 -> 3.13
        var (s, _) = WaterScenario();

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep, b => b.Measure = ReportMeasure.Net));

        Assert.Equal(3.13m, table.Rows[0].Cells[0].Value);
        Assert.Equal(ReportMeasure.Net, table.Columns[0].Measure);
    }

    [Fact]
    public void A_month_without_a_bill_uses_the_calculation_and_is_marked()
    {
        // October: calculated 29.04 / 8.000 = 3.63 (marked)
        var (s, _) = WaterScenario();

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Nov));

        Assert.Equal([3.79m, 3.63m, 3.67m], table.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal([false, true, false], table.Rows.Select(r => r.Cells[0].IsApproximate));
    }

    [Fact]
    public void A_longer_row_divides_the_sums_not_the_average_of_the_months()
    {
        // (46.02 + 29.04 + 33.00) / (12.150 + 8.000 + 9.000) = 108.06 / 29.150 = 3.7071 -> 3.71
        var (s, _) = WaterScenario();

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Nov, b => b.Rows = ReportRows.Years));

        Assert.Equal(3.71m, Assert.Single(table.Rows).Cells[0].Value);
        Assert.Equal(3.71m, table.Totals!.Cells[0].Value);
        Assert.False(table.HasRowTotals);
    }

    [Fact]
    public void Unit_costs_below_one_have_four_decimals()
    {
        // 9.00 EUR / 100.000 kWh = 0.09 -> 0.0900; 15.23 / 100 = 0.1523
        var s = new ReportScenario();
        var power = s.AddContract("Power", service: ServiceKind.Electricity);
        s.AddPerUnit(power, "Energy", 0.10m);
        s.AddMeter(power, (D(2026, 8, 31), 1000m), (D(2026, 9, 30), 1100m));
        s.AddBill(power, 15.23m, Sep);

        var cell = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep)).Rows[0].Cells[0];

        Assert.Equal(0.1523m, cell.Value);
        Assert.Equal(4, ReportFormat.Decimals(ReportValueKind.UnitPrice, cell.Value!.Value));
        Assert.Equal(2, ReportFormat.Decimals(ReportValueKind.UnitPrice, 3.79m));
    }

    [Fact]
    public void Contracts_without_metered_quantity_are_left_out()
    {
        var (s, _) = WaterScenario();
        var fixedFee = s.AddContract("Maintenance", s.Maintenance, mode: BillingMode.Fixed, service: ServiceKind.Maintenance);
        s.AddBill(fixedFee, 15m, Sep);
        var equalized = s.AddContract("Heating", mode: BillingMode.Equalized);
        s.AddBill(equalized, 50m, Sep);

        var table = ReportBuilder.UnitCost(s.Data, Filter(Sep, Sep));

        Assert.Equal("Water supply", Assert.Single(table.Columns).Name);
    }

    [Fact]
    public void Calculated_against_billed_with_the_difference_in_money_and_percent()
    {
        // September: billed 46.02 - calculated 44.10 = +1.92 = +4.4 %; November: 33.00 - 32.67 = +0.33 = +1.0 %
        var (s, _) = WaterScenario();

        var money = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Nov));
        var percent = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Nov, b => b.Measure = ReportMeasure.DifferencePercent));
        var calculated = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Nov, b => b.Measure = ReportMeasure.Calculated));
        var billed = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Nov, b => b.Measure = ReportMeasure.Billed));

        Assert.Equal([1.92m, null, 0.33m], money.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal([4.4m, null, 1.0m], percent.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal([44.10m, null, 32.67m], calculated.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal([46.02m, null, 33m], billed.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal(2.25m, money.Totals!.Cells[0].Value);
        Assert.True(money.HasRowTotals);
        Assert.False(percent.HasRowTotals);
        Assert.Equal(ReportValueKind.Percent, percent.Columns[0].ValueKind);
    }

    [Fact]
    public void The_quantity_difference_shows_the_losses()
    {
        // September: 12.350 on the bill - 12.150 metered = 0.200 m3; November: 9.000 - 9.000 = 0
        var (s, _) = WaterScenario();

        var table = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Nov, b => b.Measure = ReportMeasure.QuantityDifference));

        Assert.Equal([0.2m, null, 0m], table.Rows.Select(r => r.Cells[0].Value));
        Assert.Equal(ReportValueKind.Quantity, table.Columns[0].ValueKind);
        Assert.Equal(0.2m, table.Totals!.Cells[0].Value);
    }

    [Fact]
    public void The_stored_snapshot_is_what_was_calculated_when_the_bill_was_entered()
    {
        // The snapshot says 40.00; the calculation today would say 44.10. Billed 46.02: difference 6.02.
        var (s, water) = WaterScenario();
        var line = new ChargeLine(
            Guid.NewGuid(), "Water", ServiceKind.ColdWater, water.CategoryId, ChargeBasis.PerUnit, 12.15m, 2.7m,
            33.06m, 21m, 6.94m, 40m, false, true, []);
        s.Bills.First(b => b.Period == Sep).CalculatedSnapshot = new CalculatedCharge(water.Id, Sep, Currency.EUR, [line], 33.06m, 6.94m, 40m, true, []);

        var table = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Sep));

        Assert.Equal(6.02m, table.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void A_bill_without_a_usable_calculation_is_marked_not_compared()
    {
        // No tariff components: nothing can be calculated.
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        s.AddBill(water, 20m, Sep);
        s.AddBill(water, 20m, Oct);
        s.AddPerUnit(water, "Water", 3.00m);
        s.AddMeter(water, (D(2026, 8, 31), 100m), (D(2026, 9, 30), 110m));

        var table = ReportBuilder.CalculatedVsBilled(s.Data, Filter(Sep, Oct));

        // September: 10.000 * 3.00 = 30.00 + 6.30 = 36.30, billed 20.00; October has no readings: marked.
        Assert.Equal(-16.30m, table.Rows[0].Cells[0].Value);
        Assert.Null(table.Rows[1].Cells[0].Value);
        Assert.True(table.Rows[1].Cells[0].IsApproximate);
    }
}
