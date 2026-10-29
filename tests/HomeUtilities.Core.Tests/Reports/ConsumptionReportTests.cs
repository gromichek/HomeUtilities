using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;
using static HomeUtilities.Core.Tests.Reports.ReportScenario;

namespace HomeUtilities.Core.Tests.Reports;

public class ConsumptionReportTests
{
    private static readonly YearMonth Jan = new(2026, 1);
    private static readonly YearMonth Feb = new(2026, 2);
    private static readonly YearMonth Mar = new(2026, 3);
    private static readonly YearMonth Apr = new(2026, 4);
    private static readonly YearMonth May = new(2026, 5);

    private static decimal?[] Values(ReportTable table) => [.. table.Rows.Select(r => r.Cells[0].Value)];

    private static bool[] Marks(ReportTable table) => [.. table.Rows.Select(r => r.Cells[0].IsApproximate)];

    [Fact]
    public void Consumption_per_month_from_the_readings_at_month_ends()
    {
        // 100.000 -> 105.000 -> 112.500 -> 118.250 at the month ends: 5.000, 7.500, 5.750 m3
        var s = new ReportScenario();
        s.AddMeter(null, (D(2025, 12, 31), 100m), (D(2026, 1, 31), 105m), (D(2026, 2, 28), 112.5m), (D(2026, 3, 31), 118.25m));

        var table = ReportBuilder.Consumption(s.Data, Filter(Jan, Mar));

        var column = Assert.Single(table.Columns);
        Assert.Equal((ColumnSubject.Resource, MeterResource.ColdWater, MeterUnit.M3, RegisterKind.Total, ReportValueKind.Quantity),
            (column.Subject, column.Resource, column.Unit, column.Register, column.ValueKind));
        Assert.Equal([5m, 7.5m, 5.75m], Values(table));
        Assert.Equal([false, false, false], Marks(table));
        Assert.Equal(18.25m, table.Totals!.Cells[0].Value);
        Assert.False(table.HasRowTotals);
        Assert.Null(table.Rows[0].Total);
    }

    [Fact]
    public void A_month_without_a_reading_is_marked_not_zero_and_the_next_month_covers_the_gap()
    {
        // No reading at the end of April: April is unknown; May 118.25 - 112.5 = 5.750 covers April and May and is marked too.
        var s = new ReportScenario();
        s.AddMeter(null, (D(2026, 2, 28), 100m), (D(2026, 3, 31), 105m), (D(2026, 5, 31), 110.75m));

        var table = ReportBuilder.Consumption(s.Data, Filter(Mar, May));

        Assert.Equal([5m, null, 5.75m], Values(table));
        Assert.Equal([false, true, true], Marks(table));
        Assert.Equal(10.75m, table.Totals!.Cells[0].Value);
        Assert.True(table.Totals.Cells[0].IsApproximate);
    }

    [Fact]
    public void With_a_reading_window_a_month_without_a_reading_in_the_window_is_incomplete()
    {
        // Window 20-26: readings on 26 Feb and 26 Mar, none in April, 26 May.
        var s = new ReportScenario();
        var water = s.AddContract("Water supply");
        water.ReadingDayFrom = 20;
        water.ReadingDayTo = 26;
        s.AddMeter(water, (D(2026, 2, 26), 100m), (D(2026, 3, 26), 104m), (D(2026, 5, 26), 111m));

        var table = ReportBuilder.Consumption(s.Data, Filter(Mar, May));

        Assert.Equal(4m, table.Rows[0].Cells[0].Value);
        Assert.Null(table.Rows[1].Cells[0].Value);
        Assert.True(table.Rows[1].Cells[0].IsApproximate);
    }

    [Fact]
    public void Months_before_the_meter_existed_are_blank_not_marked()
    {
        var s = new ReportScenario();
        var meter = s.AddMeter(null, (D(2026, 2, 28), 100m), (D(2026, 3, 31), 105m));
        meter.InstalledOn = new DateOnly(2026, 2, 15);

        var table = ReportBuilder.Consumption(s.Data, Filter(Jan, Mar));

        Assert.Equal(ReportCell.Empty, table.Rows[0].Cells[0]);
        Assert.Equal(5m, table.Rows[2].Cells[0].Value);
    }

    [Fact]
    public void A_replacement_chain_is_summed_without_a_dip_in_the_replacement_month()
    {
        // Old meter 140.000 on 28 Feb, final 150.000 on 15 Mar (10.000 used); new meter 0.000 on 15 Mar, 4.000 on 31 Mar (4.000 used).
        var s = new ReportScenario();
        var old = s.AddMeter(null, (D(2026, 1, 31), 130m), (D(2026, 2, 28), 140m));
        old.Add(D(2026, 3, 15), 150m, ReadingKind.Final);
        old.RemovedOn = new DateOnly(2026, 3, 15);
        var replacement = Meter(new DateOnly(2026, 3, 15), 6, old.Id);
        replacement.PropertyId = s.Flat.Id;
        replacement.Add(D(2026, 3, 15), 0m, ReadingKind.Initial);
        replacement.Add(D(2026, 3, 31), 4m);
        replacement.Add(D(2026, 4, 30), 9m);
        s.Meters.Add(replacement);

        var table = ReportBuilder.Consumption(s.Data, Filter(Feb, Apr));

        Assert.Single(table.Columns);
        Assert.Equal([10m, 14m, 5m], Values(table));
    }

    [Fact]
    public void An_estimated_reading_marks_the_month()
    {
        var s = new ReportScenario();
        var meter = s.AddMeter(null, (D(2026, 1, 31), 100m), (D(2026, 2, 28), 105m));
        meter.Add(D(2026, 3, 31), 110m, ReadingKind.Estimated);

        var table = ReportBuilder.Consumption(s.Data, Filter(Feb, Mar));

        Assert.Equal([5m, 5m], Values(table));
        Assert.Equal([false, true], Marks(table));
    }

    [Fact]
    public void Day_and_night_registers_get_their_own_columns_and_a_total()
    {
        // Day 100 -> 130, night 50 -> 60: day 30.000, night 10.000, total 40.000 kWh
        var s = new ReportScenario();
        var meter = Meter(new DateOnly(2020, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night);
        meter.PropertyId = s.Flat.Id;
        meter.Resource = MeterResource.Electricity;
        meter.Unit = MeterUnit.KWh;
        meter.Add(D(2026, 1, 31), 100m, register: RegisterKind.Day);
        meter.Add(D(2026, 2, 28), 130m, register: RegisterKind.Day);
        meter.Add(D(2026, 1, 31), 50m, register: RegisterKind.Night);
        meter.Add(D(2026, 2, 28), 60m, register: RegisterKind.Night);
        s.Meters.Add(meter);

        var table = ReportBuilder.Consumption(s.Data, Filter(Feb, Feb));

        Assert.Equal([RegisterKind.Total, RegisterKind.Day, RegisterKind.Night], table.Columns.Select(c => c.Register));
        Assert.All(table.Columns, c => Assert.Equal(MeterUnit.KWh, c.Unit));
        Assert.Equal([40m, 30m, 10m], table.Rows[0].Cells.Select(c => c.Value));
    }

    [Fact]
    public void Meters_of_one_resource_add_up_or_show_separately_per_meter()
    {
        var s = new ReportScenario();
        var kitchen = s.AddMeter(null, (D(2026, 1, 31), 100m), (D(2026, 2, 28), 103m));
        kitchen.Location = "Kitchen";
        var bath = s.AddMeter(null, (D(2026, 1, 31), 50m), (D(2026, 2, 28), 52m));
        bath.Location = "Bathroom";

        var summed = ReportBuilder.Consumption(s.Data, Filter(Feb, Feb));
        var perMeter = ReportBuilder.Consumption(s.Data, Filter(Feb, Feb, b => b.ConsumptionGrouping = ConsumptionGrouping.Meter));

        Assert.Equal(5m, Assert.Single(summed.Rows).Cells[0].Value);
        Assert.Equal([ColumnSubject.Meter, ColumnSubject.Meter], perMeter.Columns.Select(c => c.Subject));
        Assert.Equal(["Bathroom", "Kitchen"], perMeter.Columns.Select(c => c.Name));
        Assert.Equal([2m, 3m], perMeter.Rows[0].Cells.Select(c => c.Value));
    }

    [Fact]
    public void Resources_of_different_units_get_separate_columns_in_property_order()
    {
        var s = new ReportScenario();
        var cottage = s.AddProperty("Cottage");
        s.AddMeter(null, (D(2026, 1, 31), 10m), (D(2026, 2, 28), 12m));
        var power = Meter(new DateOnly(2020, 1, 1), 6);
        power.PropertyId = cottage.Id;
        power.Resource = MeterResource.Electricity;
        power.Unit = MeterUnit.KWh;
        power.Add(D(2026, 1, 31), 1000m);
        power.Add(D(2026, 2, 28), 1200m);
        s.Meters.Add(power);

        var table = ReportBuilder.Consumption(s.Data, Filter(Feb, Feb));

        Assert.Equal(["Flat", "Cottage"], table.Columns.Select(c => c.PropertyName));
        Assert.Equal([MeterUnit.M3, MeterUnit.KWh], table.Columns.Select(c => c.Unit));
        Assert.Equal([2m, 200m], table.Rows[0].Cells.Select(c => c.Value));
        Assert.Equal(DrillTarget.Readings, table.Rows[0].Cells[0].Drill!.Target);
    }

    [Fact]
    public void Year_over_year_compares_the_same_months()
    {
        var s = new ReportScenario();
        s.AddMeter(
            null,
            (D(2025, 1, 31), 100m), (D(2025, 2, 28), 104m),
            (D(2026, 1, 31), 120m), (D(2026, 2, 28), 127m));

        var table = ReportBuilder.Consumption(s.Data, Filter(new YearMonth(2025, 2), Feb, b => b.YearOverYear = true));

        Assert.Equal([2025, 2026], table.Columns.Select(c => c.Year));
        Assert.Equal([4m, 7m], table.Rows[1].Cells.Select(c => c.Value));
    }

    // ---- Group by contract ------------------------------------------------------------------------------------------

    private static ReportFilter ByContract(YearMonth from, YearMonth to, bool yearOverYear = false) =>
        Filter(from, to, b =>
        {
            b.ConsumptionGrouping = ConsumptionGrouping.Contract;
            b.YearOverYear = yearOverYear;
        });

    private static Meter AddLinked(ReportScenario s, MeterResource resource, MeterUnit unit, decimal january, decimal? february, params Contract[] contracts)
    {
        var meter = s.AddMeter(null, (D(2026, 1, 31), january));
        if (february is { } value)
        {
            meter.Add(D(2026, 2, 28), value);
        }

        meter.Resource = resource;
        meter.Unit = unit;
        foreach (var contract in contracts)
        {
            s.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        }

        return meter;
    }

    /// <summary>
    /// The flat of the owner: two cold and two hot water meters on the water contract, the hot ones also on the heating contract.
    /// February: cold 105 - 100 = 5.000 and 204 - 200 = 4.000, hot 53 - 50 = 3.000 and 82 - 80 = 2.000.
    /// </summary>
    private static (ReportScenario Scenario, Contract Water, Contract Heating, Meter Hot2) WaterFlat(bool hot2HasFebruary = true)
    {
        var s = new ReportScenario();
        var water = s.AddContract("Water");
        var heating = s.AddContract("Heating", service: ServiceKind.Heating);
        AddLinked(s, MeterResource.ColdWater, MeterUnit.M3, 100m, 105m, water);
        AddLinked(s, MeterResource.ColdWater, MeterUnit.M3, 200m, 204m, water);
        AddLinked(s, MeterResource.HotWater, MeterUnit.M3, 50m, 53m, water, heating);
        var hot2 = AddLinked(s, MeterResource.HotWater, MeterUnit.M3, 80m, hot2HasFebruary ? 82m : null, water, heating);
        return (s, water, heating, hot2);
    }

    [Fact]
    public void By_contract_the_water_of_the_flat_is_the_sum_of_all_its_meters_and_a_shared_meter_counts_in_both_contracts()
    {
        // Water: 5.000 + 4.000 + 3.000 + 2.000 = 14.000 m3; Heating: 3.000 + 2.000 = 5.000 m3 (hot water is in both)
        var (s, _, _, _) = WaterFlat();

        var table = ReportBuilder.Consumption(s.Data, ByContract(Feb, Feb));

        Assert.Equal(["Heating", "Water"], table.Columns.Select(c => c.Name));
        Assert.All(table.Columns, c => Assert.Equal((ColumnSubject.Contract, MeterUnit.M3, RegisterKind.Total, ReportValueKind.Quantity), (c.Subject, c.Unit, c.Register, c.ValueKind)));
        Assert.Equal([5m, 14m], table.Rows[0].Cells.Select(c => c.Value));
        Assert.All(table.Rows[0].Cells, c => Assert.False(c.IsApproximate));
        Assert.Equal(DrillTarget.Readings, table.Rows[0].Cells[1].Drill!.Target);
        Assert.Equal(s.Water.Id, table.Columns[1].CategoryId);
    }

    [Fact]
    public void By_contract_a_part_that_is_incomplete_makes_the_whole_figure_approximate()
    {
        // The second hot meter has no reading at the end of February: Water = 5.000 + 4.000 + 3.000 = 12.000 ≈, Heating = 3.000 ≈
        var (s, _, _, hot2) = WaterFlat(hot2HasFebruary: false);
        hot2.Add(D(2026, 3, 31), 85m);

        var table = ReportBuilder.Consumption(s.Data, ByContract(Feb, Feb));

        Assert.Equal([3m, 12m], table.Rows[0].Cells.Select(c => c.Value));
        Assert.Equal([true, true], table.Rows[0].Cells.Select(c => c.IsApproximate));
    }

    [Fact]
    public void By_contract_only_the_months_of_the_link_count()
    {
        // The hot meter is linked to the heating contract from March: February heating is blank, March = 56 - 53 = 3.000 m3.
        var s = new ReportScenario();
        var heating = s.AddContract("Heating", service: ServiceKind.Heating);
        var hot = AddLinked(s, MeterResource.HotWater, MeterUnit.M3, 50m, 53m);
        hot.Add(D(2026, 3, 31), 56m);
        s.Links.Add(new ContractMeter { ContractId = heating.Id, MeterId = hot.Id, ValidFrom = new DateOnly(2026, 3, 1) });

        var table = ReportBuilder.Consumption(s.Data, ByContract(Feb, Mar));

        Assert.Equal(ReportCell.Empty, table.Rows[0].Cells[0]);
        Assert.Equal(3m, table.Rows[1].Cells[0].Value);
    }

    [Fact]
    public void By_contract_a_contract_without_meters_has_no_column_and_units_are_never_added_together()
    {
        // Electricity contract with a 200.000 kWh meter and a 3.000 m3 meter: two columns, not 203.
        var s = new ReportScenario();
        var power = s.AddContract("Electricity", service: ServiceKind.Electricity);
        s.AddContract("Maintenance", service: ServiceKind.Maintenance);
        AddLinked(s, MeterResource.Electricity, MeterUnit.KWh, 1000m, 1200m, power);
        AddLinked(s, MeterResource.HotWater, MeterUnit.M3, 50m, 53m, power);

        var table = ReportBuilder.Consumption(s.Data, ByContract(Feb, Feb));

        Assert.Equal(["Electricity", "Electricity"], table.Columns.Select(c => c.Name));
        Assert.Equal([MeterUnit.M3, MeterUnit.KWh], table.Columns.Select(c => c.Unit));
        Assert.Equal([3m, 200m], table.Rows[0].Cells.Select(c => c.Value));
        Assert.NotEqual(table.Columns[0].Key, table.Columns[1].Key);
    }

    [Fact]
    public void By_contract_year_over_year_compares_the_same_months()
    {
        // February 2025: cold 104 - 100 = 4.000 + hot 51 - 50 = 1.000 -> 5.000; February 2026: 127 - 120 = 7.000 + 62 - 60 = 2.000 -> 9.000
        var s = new ReportScenario();
        var water = s.AddContract("Water");
        var cold = s.AddMeter(water, (D(2025, 1, 31), 100m), (D(2025, 2, 28), 104m), (D(2026, 1, 31), 120m), (D(2026, 2, 28), 127m));
        var hot = s.AddMeter(water, (D(2025, 1, 31), 50m), (D(2025, 2, 28), 51m), (D(2026, 1, 31), 60m), (D(2026, 2, 28), 62m));
        hot.Resource = MeterResource.HotWater;

        var table = ReportBuilder.Consumption(s.Data, ByContract(new YearMonth(2025, 2), Feb, yearOverYear: true));

        Assert.NotEqual(cold.Id, hot.Id);
        Assert.Equal([2025, 2026], table.Columns.Select(c => c.Year));
        Assert.Equal([5m, 9m], table.Rows[1].Cells.Select(c => c.Value));
    }

    [Fact]
    public void By_contract_a_day_night_electricity_meter_gets_a_total_and_a_column_per_tariff()
    {
        // Day 100 -> 130, night 50 -> 60 on the contract: total 40.000, day 30.000, night 10.000 kWh
        var s = new ReportScenario();
        var power = s.AddContract("Electricity", service: ServiceKind.Electricity);
        var meter = Meter(new DateOnly(2020, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night);
        meter.PropertyId = s.Flat.Id;
        meter.Resource = MeterResource.Electricity;
        meter.Unit = MeterUnit.KWh;
        meter.Add(D(2026, 1, 31), 100m, register: RegisterKind.Day);
        meter.Add(D(2026, 2, 28), 130m, register: RegisterKind.Day);
        meter.Add(D(2026, 1, 31), 50m, register: RegisterKind.Night);
        meter.Add(D(2026, 2, 28), 60m, register: RegisterKind.Night);
        s.Meters.Add(meter);
        s.Links.Add(new ContractMeter { ContractId = power.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });

        var table = ReportBuilder.Consumption(s.Data, ByContract(Feb, Feb));

        Assert.Equal([RegisterKind.Total, RegisterKind.Day, RegisterKind.Night], table.Columns.Select(c => c.Register));
        Assert.Equal([40m, 30m, 10m], table.Rows[0].Cells.Select(c => c.Value));
    }
}
