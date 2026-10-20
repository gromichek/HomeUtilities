using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Charges;

public class ChargeCalculatorTests
{
    private static readonly YearMonth Sep2026 = new(2026, 9);

    private static string[] Codes(CalculatedCharge c) => [.. c.Warnings.Select(w => w.Code)];

    /// <summary>Cold meter 425.900 → 437.097 and hot meter 371.927 → 380.844 with readings on the 26th.</summary>
    private static (ChargeBuilder B, Meter Cold, Meter Hot) WaterFixture()
    {
        var b = new ChargeBuilder().Window(20, 26);
        var cold = b.AddMeter(Meter(D(2020, 1, 1), 6));
        cold.Add(D(2026, 8, 26), 425.900m);
        cold.Add(D(2026, 9, 26), 437.097m);
        var hot = b.AddMeter(Meter(D(2020, 1, 1), 6));
        hot.Add(D(2026, 8, 26), 371.927m);
        hot.Add(D(2026, 9, 26), 380.844m);
        return (b, cold, hot);
    }

    [Fact]
    public void Water_and_sewerage_from_both_meters()
    {
        // quantity 11.197 + 8.917 = 20.114 m3
        // Water supply: 20.114 * 1.41 = 28.36074 -> net 28.36; VAT 21% = 5.9557 -> 5.96; gross 34.32
        // Sewerage:     20.114 * 0.86 = 17.29804 -> net 17.30; VAT 21% = 3.6326 -> 3.63; gross 20.93
        // total net 45.66, VAT 9.59, gross 55.25
        var (b, _, _) = WaterFixture();
        b.Add("Water supply", ChargeBasis.PerUnit, 1.41m, sortOrder: 1);
        b.Add("Sewerage", ChargeBasis.PerUnit, 0.86m, sortOrder: 2);

        var charge = b.Calculate(Sep2026);

        Assert.True(charge.IsComplete);
        Assert.Empty(charge.Warnings);
        Assert.Equal(["Water supply", "Sewerage"], charge.Lines.Select(l => l.ComponentName));
        var water = charge.Lines[0];
        Assert.Equal(20.114m, water.Quantity);
        Assert.Equal(1.41m, water.UnitPrice);
        Assert.Equal((28.36m, 21m, 5.96m, 34.32m), (water.Net, water.VatPercent, water.Vat, water.Gross));
        Assert.Equal(2, water.Consumption.Count);
        var sewer = charge.Lines[1];
        Assert.Equal((17.30m, 3.63m, 20.93m), (sewer.Net, sewer.Vat, sewer.Gross));
        Assert.Equal((45.66m, 9.59m, 55.25m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
        Assert.Equal(Currency.EUR, charge.Currency);
    }

    [Fact]
    public void Components_are_ordered_by_sort_order_then_name()
    {
        var (b, _, _) = WaterFixture();
        b.Add("B", ChargeBasis.FixedMonthly, 1m, sortOrder: 1);
        b.Add("C", ChargeBasis.FixedMonthly, 1m, sortOrder: 0);
        b.Add("A", ChargeBasis.FixedMonthly, 1m, sortOrder: 1);
        b.Add("Deleted", ChargeBasis.FixedMonthly, 1m).IsDeleted = true;

        Assert.Equal(["C", "A", "B"], b.Calculate(Sep2026).Lines.Select(l => l.ComponentName));
    }

    [Fact]
    public void Heating_of_hot_water_uses_only_linked_hot_meter_and_adds_from_bill_line()
    {
        // 8.917 m3 * 4.56 = 40.66152 -> net 40.66; VAT 12% = 4.8792 -> 4.88; gross 45.54
        var b = new ChargeBuilder().Window(20, 26);
        var hot = b.AddMeter(Meter(D(2020, 1, 1), 6));
        hot.Add(D(2026, 8, 26), 371.927m);
        hot.Add(D(2026, 9, 26), 380.844m);
        b.Add("Heating of hot water", ChargeBasis.PerUnit, 4.56m, sortOrder: 1).VatPercentOverride = 12m;
        b.Add("Heating", ChargeBasis.FromBill, 0m, sortOrder: 2);

        var charge = b.Calculate(Sep2026);

        var heating = charge.Lines[0];
        Assert.Equal(8.917m, heating.Quantity);
        Assert.Equal((40.66m, 12m, 4.88m, 45.54m), (heating.Net, heating.VatPercent, heating.Vat, heating.Gross));
        var fromBill = charge.Lines[1];
        Assert.True(fromBill.IsFromBill);
        Assert.True(fromBill.IsComplete);
        Assert.Null(fromBill.Quantity);
        Assert.Null(fromBill.UnitPrice);
        Assert.Equal((0m, 0m, 0m), (fromBill.Net, fromBill.Vat, fromBill.Gross));
        Assert.Equal(45.54m, charge.TotalGross);
        Assert.True(charge.IsComplete);
    }

    [Fact]
    public void Maintenance_per_area()
    {
        // 52.30 m2 * 0.6711 = 35.09853 -> net 35.10; VAT 0% = 0; gross 35.10
        var b = new ChargeBuilder();
        b.AddParameter(PropertyParameterKind.TotalArea, D(2020, 1, 1), 52.30m);
        b.Add("Maintenance", ChargeBasis.PerArea, 0.6711m, "ZERO");

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal(52.30m, line.Quantity);
        Assert.Equal((35.10m, 0m, 35.10m), (line.Net, line.Vat, line.Gross));
    }

    [Fact]
    public void Area_component_can_use_another_parameter()
    {
        // heated area 40 m2 * 0.50 = 20.00; VAT 21% = 4.20; gross 24.20
        var b = new ChargeBuilder();
        b.AddParameter(PropertyParameterKind.TotalArea, D(2020, 1, 1), 52.30m);
        b.AddParameter(PropertyParameterKind.HeatedArea, D(2020, 1, 1), 40m);
        b.Add("Heating fee", ChargeBasis.PerArea, 0.50m).QuantityParameter = PropertyParameterKind.HeatedArea;

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal(40m, line.Quantity);
        Assert.Equal((20.00m, 4.20m, 24.20m), (line.Net, line.Vat, line.Gross));
    }

    [Fact]
    public void Custom_parameter_is_found_by_name()
    {
        // 4 units * 2.50 = 10.00; VAT 21% = 2.10; gross 12.10
        var b = new ChargeBuilder();
        b.AddParameter(PropertyParameterKind.Custom, D(2020, 1, 1), 4m, "Parking places");
        var component = b.Add("Parking", ChargeBasis.PerArea, 2.50m);
        component.QuantityParameter = PropertyParameterKind.Custom;
        component.QuantityParameterName = "Parking places";

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal((10.00m, 2.10m, 12.10m), (line.Net, line.Vat, line.Gross));
    }

    [Theory]
    [InlineData(5, 3, 22.50, 4.73, 27.23)] // 3 * 7.50 = 22.50; VAT 21% = 4.725 -> 4.73; gross 27.23
    [InlineData(6, 2, 15.00, 3.15, 18.15)] // 2 * 7.50 = 15.00; VAT 21% = 3.15; gross 18.15
    public void Waste_per_resident_follows_resident_changes(int month, int residents, double net, double vat, double gross)
    {
        var b = new ChargeBuilder();
        b.AddParameter(PropertyParameterKind.DeclaredResidents, D(2020, 1, 1), 3m);
        b.AddParameter(PropertyParameterKind.DeclaredResidents, D(2026, 6, 1), 2m);
        b.Add("Waste", ChargeBasis.PerResident, 7.50m);

        var line = b.Calculate(new YearMonth(2026, month)).Lines.Single();

        Assert.Equal(residents, line.Quantity);
        Assert.Equal(((decimal)net, (decimal)vat, (decimal)gross), (line.Net, line.Vat, line.Gross));
    }

    [Fact]
    public void Fixed_monthly_price_including_vat()
    {
        // 22.95 gross; net = 22.95 / 1.21 = 18.9669 -> 18.97; VAT 3.98
        var b = new ChargeBuilder();
        b.Add("Internet", ChargeBasis.FixedMonthly, 22.95m, includesVat: true);

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal(1m, line.Quantity);
        Assert.Equal(22.95m, line.UnitPrice);
        Assert.Equal((18.97m, 3.98m, 22.95m), (line.Net, line.Vat, line.Gross));
    }

    [Fact]
    public void Net_export_is_a_negative_line()
    {
        // export 1450 - 1200 = 250 kWh; -250 * 0.05 = -12.50; VAT 21% = -2.625 -> -2.63; gross -15.13
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 7, null, RegisterKind.Total, RegisterKind.Export));
        meter.Add(D(2026, 8, 31), 5000m);
        meter.Add(D(2026, 9, 30), 5300m);
        meter.Add(D(2026, 8, 31), 1200m, register: RegisterKind.Export);
        meter.Add(D(2026, 9, 30), 1450m, register: RegisterKind.Export);
        b.Add("Solar export", ChargeBasis.NetExport, 0.05m);

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal(-250m, line.Quantity);
        Assert.Equal((-12.50m, -2.63m, -15.13m), (line.Net, line.Vat, line.Gross));
    }

    [Fact]
    public void Net_export_with_tiers_is_invalid()
    {
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 7, null, RegisterKind.Export));
        meter.Add(D(2026, 8, 31), 1200m, register: RegisterKind.Export);
        meter.Add(D(2026, 9, 30), 1450m, register: RegisterKind.Export);
        var component = b.Add("Solar export", ChargeBasis.NetExport, 0.05m);
        component.Rates[0].Tiers = [new(100m, 0.05m), new(null, 0.04m)];
        component.Rates[0].TierPeriod = TierPeriod.Year;

        var charge = b.Calculate(Sep2026);

        Assert.False(charge.IsComplete);
        Assert.Equal([ChargeWarningCodes.InvalidTariff], Codes(charge));
    }

    [Fact]
    public void Dual_tariff_components_count_their_own_registers()
    {
        // Day: 180 - 100 = 80 kWh * 0.20 = 16.00; VAT 3.36; gross 19.36
        // Night: 90 - 50 = 40 kWh * 0.10 = 4.00; VAT 0.84; gross 4.84
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night));
        meter.Add(D(2026, 8, 31), 100m, register: RegisterKind.Day);
        meter.Add(D(2026, 9, 30), 180m, register: RegisterKind.Day);
        meter.Add(D(2026, 8, 31), 50m, register: RegisterKind.Night);
        meter.Add(D(2026, 9, 30), 90m, register: RegisterKind.Night);
        b.Add("Energy day", ChargeBasis.PerUnit, 0.20m, sortOrder: 1, registers: RegisterKind.Day);
        b.Add("Energy night", ChargeBasis.PerUnit, 0.10m, sortOrder: 2, registers: RegisterKind.Night);
        b.Add("Energy all", ChargeBasis.PerUnit, 1m, sortOrder: 3);

        var lines = b.Calculate(Sep2026).Lines;

        Assert.Equal((80m, 16.00m, 3.36m, 19.36m), (lines[0].Quantity, lines[0].Net, lines[0].Vat, lines[0].Gross));
        Assert.Equal((40m, 4.00m, 0.84m, 4.84m), (lines[1].Quantity, lines[1].Net, lines[1].Vat, lines[1].Gross));
        Assert.Equal(120m, lines[2].Quantity); // no register selection: every import register
    }

    [Fact]
    public void Yearly_tiers_continue_from_consumption_earlier_in_the_year()
    {
        // used before the period: 11150 - 10000 = 1150; this period: 11350 - 11150 = 200
        // tier 1 (up to 1200): 50 * 0.10 = 5.00; VAT 1.05
        // tier 2 (unlimited): 150 * 0.15 = 22.50; VAT 4.725 -> 4.73
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 6));
        meter.Add(D(2026, 1, 1), 10000m);
        meter.Add(D(2026, 8, 31), 11150m);
        meter.Add(D(2026, 9, 30), 11350m);
        var component = b.Add("Energy", ChargeBasis.PerUnit, 0m);
        component.Rates[0].Tiers = [new(1200m, 0.10m), new(null, 0.15m)];
        component.Rates[0].TierPeriod = TierPeriod.Year;

        var charge = b.Calculate(Sep2026);

        Assert.Empty(charge.Warnings);
        Assert.Equal(2, charge.Lines.Count);
        Assert.All(charge.Lines, l => Assert.Equal(component.Id, l.ComponentId));
        Assert.Equal((50m, 0.10m, 5.00m, 1.05m, 6.05m), Tuple(charge.Lines[0]));
        Assert.Equal((150m, 0.15m, 22.50m, 4.73m, 27.23m), Tuple(charge.Lines[1]));
        Assert.Equal(27.50m, charge.TotalNet);
    }

    [Fact]
    public void Monthly_tiers_start_at_zero_every_month()
    {
        // 200 kWh: tier 1 (up to 150): 150 * 0.10 = 15.00; tier 2: 50 * 0.20 = 10.00
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 6));
        meter.Add(D(2026, 8, 31), 1000m);
        meter.Add(D(2026, 9, 30), 1200m);
        var component = b.Add("Energy", ChargeBasis.PerUnit, 0m);
        component.Rates[0].Tiers = [new(150m, 0.10m), new(null, 0.20m)];
        component.Rates[0].TierPeriod = TierPeriod.Month;

        var charge = b.Calculate(Sep2026);

        Assert.Equal([15.00m, 10.00m], charge.Lines.Select(l => l.Net));
        Assert.Empty(charge.Warnings);
    }

    [Fact]
    public void Yearly_tiers_without_january_reading_warn_and_start_at_zero()
    {
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2026, 3, 1), 6));
        meter.Add(D(2026, 8, 31), 11150m);
        meter.Add(D(2026, 9, 30), 11350m);
        var component = b.Add("Energy", ChargeBasis.PerUnit, 0m);
        component.Rates[0].Tiers = [new(1200m, 0.10m), new(null, 0.15m)];
        component.Rates[0].TierPeriod = TierPeriod.Year;

        var charge = b.Calculate(Sep2026);

        Assert.Equal([ChargeWarningCodes.TierUsageUnknown], Codes(charge));
        Assert.Equal([20.00m], charge.Lines.Select(l => l.Net)); // 200 * 0.10
    }

    [Fact]
    public void Tiered_component_without_consumption_still_yields_one_zero_line()
    {
        var b = new ChargeBuilder();
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 6));
        meter.Add(D(2026, 8, 31), 1000m);
        meter.Add(D(2026, 9, 30), 1000m);
        var component = b.Add("Energy", ChargeBasis.PerUnit, 0m);
        component.Rates[0].Tiers = [new(150m, 0.10m), new(null, 0.20m)];
        component.Rates[0].TierPeriod = TierPeriod.Month;

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal((0m, 0m), (line.Quantity, line.Net));
    }

    private static (decimal?, decimal?, decimal, decimal, decimal) Tuple(ChargeLine l) => (l.Quantity, l.UnitPrice, l.Net, l.Vat, l.Gross);

    [Fact]
    public void Rate_valid_on_the_end_reading_date_is_used()
    {
        // 1.30 from 2026-01-01, 1.41 from 2026-09-15; end reading 2026-09-26 -> 1.41; 10 m3 * 1.41 = 14.10
        var b = new ChargeBuilder().Window(20, 26);
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 6));
        meter.Add(D(2026, 8, 26), 100m);
        meter.Add(D(2026, 9, 26), 110m);
        var component = b.Add("Water supply", ChargeBasis.PerUnit, 1.30m);
        component.Rates.Add(ChargeBuilder.Rate(component, D(2026, 9, 15), 1.41m));

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal(1.41m, line.UnitPrice);
        Assert.Equal(14.10m, line.Net);
    }

    [Fact]
    public void Rate_is_chosen_on_first_day_for_non_metered_bases()
    {
        var b = new ChargeBuilder();
        var component = b.Add("Internet", ChargeBasis.FixedMonthly, 10m);
        component.Rates.Add(ChargeBuilder.Rate(component, D(2026, 9, 2), 12m));

        Assert.Equal(10m, b.Calculate(Sep2026).Lines.Single().UnitPrice);
        Assert.Equal(12m, b.Calculate(new YearMonth(2026, 10)).Lines.Single().UnitPrice);
    }

    [Fact]
    public void Meter_replacement_inside_the_period_is_summed_although_the_new_meter_is_not_linked()
    {
        // old: 112 - 100 = 12; new: 5 - 0 = 5; total 17 m3 * 1.41 = 23.97
        var b = new ChargeBuilder().Window(20, 26);
        var old = b.AddMeter(Meter(D(2020, 1, 1), 6));
        old.Add(D(2026, 8, 26), 100m);
        old.Add(D(2026, 9, 10), 112m, ReadingKind.Final);
        old.RemovedOn = D(2026, 9, 10);
        var fresh = b.AddMeter(Meter(D(2026, 9, 10), 6, old.Id), link: false);
        fresh.Add(D(2026, 9, 10), 0m, ReadingKind.Initial);
        fresh.Add(D(2026, 9, 26), 5m);
        b.Add("Water supply", ChargeBasis.PerUnit, 1.41m);

        var charge = b.Calculate(Sep2026);

        Assert.Empty(charge.Warnings);
        Assert.Equal(17m, charge.Lines.Single().Quantity);
        Assert.Equal(23.97m, charge.Lines.Single().Net);
    }

    [Fact]
    public void Links_to_two_meters_of_one_chain_count_once()
    {
        // both links point into the chain old -> fresh; quantity 12 + 5 = 17, not 34
        var b = new ChargeBuilder().Window(20, 26);
        var old = b.AddMeter(Meter(D(2020, 1, 1), 6));
        old.Add(D(2026, 8, 26), 100m);
        old.Add(D(2026, 9, 10), 112m, ReadingKind.Final);
        var fresh = b.AddMeter(Meter(D(2026, 9, 10), 6, old.Id));
        fresh.Add(D(2026, 9, 10), 0m, ReadingKind.Initial);
        fresh.Add(D(2026, 9, 26), 5m);
        b.Add("Water supply", ChargeBasis.PerUnit, 1m);

        var line = b.Calculate(Sep2026).Lines.Single();

        Assert.Equal(17m, line.Quantity);
        Assert.Single(line.Consumption);
    }

    [Fact]
    public void Meter_ids_of_the_component_restrict_the_meters()
    {
        var (b, cold, _) = WaterFixture();
        b.Add("Cold only", ChargeBasis.PerUnit, 1m).MeterIds = [cold.Id];

        Assert.Equal(11.197m, b.Calculate(Sep2026).Lines.Single().Quantity);
    }

    [Fact]
    public void Links_that_are_not_active_at_period_end_are_ignored()
    {
        var (b, cold, hot) = WaterFixture();
        b.Links.Single(l => l.MeterId == hot.Id).ValidTo = D(2026, 9, 1);
        b.Add("Water", ChargeBasis.PerUnit, 1m);

        Assert.Equal(11.197m, b.Calculate(Sep2026).Lines.Single().Quantity);
    }

    [Fact]
    public void Missing_end_reading_gives_incomplete_zero_quantity_line()
    {
        var b = new ChargeBuilder().Window(20, 26);
        var meter = b.AddMeter(Meter(D(2020, 1, 1), 6));
        meter.Add(D(2026, 8, 26), 100m);
        b.Add("Water supply", ChargeBasis.PerUnit, 1.41m);

        var charge = b.Calculate(Sep2026);

        var line = charge.Lines.Single();
        Assert.Equal(0m, line.Quantity);
        Assert.Equal(0m, line.Net);
        Assert.False(line.IsComplete);
        Assert.False(charge.IsComplete);
        Assert.Equal([ChargeWarningCodes.ConsumptionIncomplete], Codes(charge));
        Assert.Equal(b.Components[0].Id, charge.Warnings[0].ComponentId);
    }

    [Fact]
    public void Per_unit_component_without_meters_is_incomplete()
    {
        var b = new ChargeBuilder();
        b.Add("Water supply", ChargeBasis.PerUnit, 1.41m);

        var charge = b.Calculate(Sep2026);

        Assert.False(charge.IsComplete);
        Assert.Equal([ChargeWarningCodes.NoMeters], Codes(charge));
    }

    [Fact]
    public void No_rate_for_date_gives_zero_incomplete_line()
    {
        var b = new ChargeBuilder();
        b.Add("Internet", ChargeBasis.FixedMonthly, 10m).Rates[0].ValidFrom = D(2026, 10, 1);

        var charge = b.Calculate(Sep2026);

        var line = charge.Lines.Single();
        Assert.Equal((0m, 0m, 0m, false), (line.Net, line.Vat, line.Gross, line.IsComplete));
        Assert.Equal([ChargeWarningCodes.NoRateForDate], Codes(charge));
        Assert.False(charge.IsComplete);
    }

    [Fact]
    public void Unknown_vat_code_gives_zero_incomplete_line()
    {
        var b = new ChargeBuilder();
        b.Add("Internet", ChargeBasis.FixedMonthly, 10m, "NO-SUCH-CODE");

        var charge = b.Calculate(Sep2026);

        Assert.Equal(0m, charge.Lines.Single().Gross);
        Assert.False(charge.Lines.Single().IsComplete);
        Assert.Equal([ChargeWarningCodes.UnknownVat], Codes(charge));
    }

    [Fact]
    public void Currency_mismatch_gives_zero_incomplete_line()
    {
        var b = new ChargeBuilder();
        b.Add("Internet", ChargeBasis.FixedMonthly, 10m).Rates[0].Currency = Currency.LVL;

        var charge = b.Calculate(Sep2026);

        Assert.Equal(0m, charge.Lines.Single().Gross);
        Assert.False(charge.IsComplete);
        Assert.Equal([ChargeWarningCodes.CurrencyMismatch], Codes(charge));
    }

    [Fact]
    public void Missing_parameter_gives_zero_incomplete_line()
    {
        var b = new ChargeBuilder();
        b.Add("Waste", ChargeBasis.PerResident, 7.50m);

        var charge = b.Calculate(Sep2026);

        var line = charge.Lines.Single();
        Assert.Null(line.Quantity);
        Assert.Equal(0m, line.Gross);
        Assert.False(line.IsComplete);
        Assert.Equal([ChargeWarningCodes.MissingParameter], Codes(charge));
    }

    [Theory]
    [InlineData(2026, 7, 31, 2026, 9)] // ended before the period
    [InlineData(2026, 10, 1, 2026, 9)] // starts after the period
    public void Inactive_contract_gives_empty_complete_charge(int y, int m, int d, int py, int pm)
    {
        var b = new ChargeBuilder();
        if (m == 7)
        {
            b.Contract.ValidTo = D(y, m, d);
        }
        else
        {
            b.Contract.ValidFrom = D(y, m, d);
        }

        b.Add("Internet", ChargeBasis.FixedMonthly, 10m);

        var charge = b.Calculate(new YearMonth(py, pm));

        Assert.Empty(charge.Lines);
        Assert.True(charge.IsComplete);
        Assert.Equal(0m, charge.TotalGross);
        Assert.Equal([ChargeWarningCodes.ContractInactive], Codes(charge));
    }

    [Fact]
    public void Contract_active_for_part_of_the_period_is_calculated()
    {
        var b = new ChargeBuilder();
        b.Contract.ValidTo = D(2026, 9, 15);
        b.Add("Internet", ChargeBasis.FixedMonthly, 10m);

        Assert.Single(b.Calculate(Sep2026).Lines);
    }
}
