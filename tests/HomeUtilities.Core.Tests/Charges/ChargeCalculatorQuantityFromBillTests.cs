using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Charges;

/// <summary>Quantity-from-the-bill components and VAT on the total (T-020a).</summary>
public class ChargeCalculatorQuantityFromBillTests
{
    private static readonly YearMonth Sep2026 = new(2026, 9);

    private static ChargeBuilder HeatingFixture(out TariffComponent heat, out TariffComponent difference)
    {
        // Hot water by the flat's meter: 100.000 -> 107.500 = 7.500 m3.
        var b = new ChargeBuilder().Window(20, 26);
        var hot = b.AddMeter(Meter(D(2020, 1, 1), 6));
        hot.Add(D(2026, 8, 26), 100.000m);
        hot.Add(D(2026, 9, 26), 107.500m);
        b.Add("Hot water", ChargeBasis.PerUnit, 3.20m, "LV-REDUCED", sortOrder: 0);
        heat = b.Add("Heat for heating/circulation", ChargeBasis.QuantityFromBill, 70.00m, "LV-REDUCED", sortOrder: 1);
        heat.QuantityUnit = MeterUnit.MWh;
        difference = b.Add("Hot water difference", ChargeBasis.QuantityFromBill, 3.20m, "LV-REDUCED", sortOrder: 2);
        difference.QuantityUnit = MeterUnit.M3;
        return b;
    }

    [Fact]
    public void Heating_bill_of_the_owner_vat_is_calculated_on_the_total()
    {
        // Hot water      7.500 m3 * 3.20 = 24.00
        // Heat           0.210 MWh * 70.00 = 14.70
        // Difference     0.150 m3 * 3.20 = 0.48
        // Net 24.00 + 14.70 + 0.48 = 39.18; VAT 12% of 39.18 = 4.7016 -> 4.70; total 43.88
        var b = HeatingFixture(out var heat, out var difference);
        b.Quantities[heat.Id] = 0.210m;
        b.Quantities[difference.Id] = 0.150m;

        var charge = b.Calculate(Sep2026);

        Assert.True(charge.IsComplete);
        Assert.Empty(charge.Warnings);
        Assert.Equal([24.00m, 14.70m, 0.48m], charge.Lines.Select(l => l.Net));
        Assert.Equal(0.210m, charge.Lines[1].Quantity);
        Assert.Equal(70.00m, charge.Lines[1].UnitPrice);
        Assert.Equal(ChargeBasis.QuantityFromBill, charge.Lines[1].Basis);
        var group = Assert.Single(charge.VatGroups!);
        Assert.Equal((12m, 39.18m, 4.70m, 43.88m), (group.VatPercent, group.Net, group.Vat, group.Gross));
        Assert.Equal((39.18m, 4.70m, 43.88m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Fact]
    public void A_bill_equal_to_the_calculated_total_has_no_difference()
    {
        // The owner's bill total 43.88 against the calculation 43.88: difference 0.00 (0.0 %)
        var b = HeatingFixture(out var heat, out var difference);
        b.Quantities[heat.Id] = 0.210m;
        b.Quantities[difference.Id] = 0.150m;

        var result = BillComparison.Compare(new Bill { Gross = 43.88m }, b.Calculate(Sep2026), null);

        Assert.Equal((0m, 0.0m), (result.Difference, result.DifferencePercent));
    }

    [Fact]
    public void Quantity_from_the_bill_is_rounded_to_three_decimals()
    {
        // 0.2104 -> 0.210 MWh * 70.00 = 14.70
        var b = HeatingFixture(out var heat, out var difference);
        b.Quantities[heat.Id] = 0.2104m;
        b.Quantities[difference.Id] = 0m;

        var line = b.Calculate(Sep2026).Lines[1];

        Assert.Equal(0.210m, line.Quantity);
        Assert.Equal(14.70m, line.Net);
    }

    [Fact]
    public void Missing_quantity_gives_an_incomplete_line_and_a_warning()
    {
        // Only the heat quantity is typed; the difference is missing: its line is incomplete with no amounts.
        var b = HeatingFixture(out var heat, out var difference);
        b.Quantities[heat.Id] = 0.210m;

        var charge = b.Calculate(Sep2026);

        Assert.False(charge.IsComplete);
        var warning = Assert.Single(charge.Warnings);
        Assert.Equal((ChargeWarningCodes.QuantityFromBill, difference.Id), (warning.Code, warning.ComponentId));
        var missing = charge.Lines[2];
        Assert.False(missing.IsComplete);
        Assert.Null(missing.Quantity);
        Assert.Equal(0m, missing.Gross);

        // 24.00 + 14.70 = 38.70; VAT 12% = 4.644 -> 4.64; total 43.34
        Assert.Equal((38.70m, 4.64m, 43.34m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Fact]
    public void Without_any_quantity_the_loader_default_of_null_means_not_entered()
    {
        var b = HeatingFixture(out _, out _);

        var charge = ChargeCalculator.Calculate(new ChargeInput(b.Contract, b.Property, b.Components, b.Links, b.Meters, b.Vat, Sep2026));

        Assert.False(charge.IsComplete);
        Assert.Equal(2, charge.Warnings.Count(w => w.Code == ChargeWarningCodes.QuantityFromBill));
    }

    [Fact]
    public void Three_lines_of_two_cents_differ_between_on_total_and_per_line()
    {
        // 3 lines of 0.02 EUR at 21%: line VAT 0.0042 -> 0.00 each.
        // PerLine:  VAT 0.00, gross 0.06
        // OnTotal:  0.06 * 21% = 0.0126 -> 0.01, gross 0.07
        var b = new ChargeBuilder();
        for (var i = 0; i < 3; i++)
        {
            b.Add($"Fee {i}", ChargeBasis.FixedMonthly, 0.02m, sortOrder: i);
        }

        var onTotal = b.Calculate(Sep2026);
        b.Contract.VatCalculation = VatCalculation.PerLine;
        var perLine = b.Calculate(Sep2026);

        Assert.All(onTotal.Lines, l => Assert.Equal(0.00m, l.Vat));
        Assert.Equal((0.06m, 0.01m, 0.07m), (onTotal.TotalNet, onTotal.TotalVat, onTotal.TotalGross));
        Assert.Equal((0.06m, 0.00m, 0.06m), (perLine.TotalNet, perLine.TotalVat, perLine.TotalGross));
    }

    [Fact]
    public void New_contracts_calculate_vat_on_the_total_by_default()
    {
        Assert.Equal(VatCalculation.OnTotal, new Contract().VatCalculation);
    }

    [Fact]
    public void Group_priced_including_vat_is_split_from_its_gross_sum()
    {
        // 3 lines of 0.07 incl. 21% VAT: per line net 0.06 (0.0579) and VAT 0.01 -> sum net 0.18, VAT 0.03
        // OnTotal: gross 0.21; net = round(0.21 / 1.21) = round(0.1736) = 0.17; VAT = 0.21 - 0.17 = 0.04
        var b = new ChargeBuilder();
        for (var i = 0; i < 3; i++)
        {
            b.Add($"Fee {i}", ChargeBasis.FixedMonthly, 0.07m, includesVat: true, sortOrder: i);
        }

        var onTotal = b.Calculate(Sep2026);
        b.Contract.VatCalculation = VatCalculation.PerLine;
        var perLine = b.Calculate(Sep2026);

        Assert.Equal((0.17m, 0.04m, 0.21m), (onTotal.TotalNet, onTotal.TotalVat, onTotal.TotalGross));
        Assert.Equal((0.18m, 0.03m, 0.21m), (perLine.TotalNet, perLine.TotalVat, perLine.TotalGross));
    }

    [Fact]
    public void Mixed_vat_percents_form_one_group_each()
    {
        // 21%: 10.00 -> VAT 2.10, gross 12.10; 12%: 5.00 -> VAT 0.60, gross 5.60
        // Total net 15.00, VAT 2.70, gross 17.70; groups ordered by percent
        var b = new ChargeBuilder();
        b.Add("Standard", ChargeBasis.FixedMonthly, 10.00m, sortOrder: 0);
        b.Add("Reduced", ChargeBasis.FixedMonthly, 5.00m, "LV-REDUCED", sortOrder: 1);

        var charge = b.Calculate(Sep2026);

        Assert.Equal(
            [new VatGroup(12m, 5.00m, 0.60m, 5.60m), new VatGroup(21m, 10.00m, 2.10m, 12.10m)],
            charge.VatGroups);
        Assert.Equal((15.00m, 2.70m, 17.70m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Fact]
    public void Water_example_is_unchanged()
    {
        // 10 m3 * 1.41 = 14.10; VAT 21% = 2.961 -> 2.96; total 17.06
        var b = new ChargeBuilder().Window(20, 26);
        var cold = b.AddMeter(Meter(D(2020, 1, 1), 6));
        cold.Add(D(2026, 8, 26), 100m);
        cold.Add(D(2026, 9, 26), 110m);
        b.Add("Water", ChargeBasis.PerUnit, 1.41m);

        var charge = b.Calculate(Sep2026);

        Assert.Equal((14.10m, 2.96m, 17.06m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
        Assert.Equal([new VatGroup(21m, 14.10m, 2.96m, 17.06m)], charge.VatGroups);
    }

    [Fact]
    public void Lines_without_amounts_form_no_vat_group()
    {
        var b = new ChargeBuilder();
        b.Add("Heating", ChargeBasis.FromBill, 0m);

        var charge = b.Calculate(Sep2026);

        Assert.Empty(charge.VatGroups!);
        Assert.Equal(0m, charge.TotalGross);
    }
}
