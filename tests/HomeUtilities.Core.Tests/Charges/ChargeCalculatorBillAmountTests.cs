using System.Globalization;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Charges;

/// <summary>The optional line amount typed from the bill for a quantity-from-the-bill component.</summary>
public class ChargeCalculatorBillAmountTests
{
    private static readonly YearMonth Sep2026 = new(2026, 9);

    private static ChargeBuilder HeatOnly(out TariffComponent heat, bool includesVat = false, decimal price = 73.76m)
    {
        var b = new ChargeBuilder();
        heat = b.Add("Heat", ChargeBasis.QuantityFromBill, price, "LV-REDUCED", includesVat);
        heat.QuantityUnit = MeterUnit.MWh;
        return b;
    }

    [Fact]
    public void Without_an_amount_the_line_is_quantity_times_price()
    {
        // 0.233 MWh * 73.76 = 17.18608 -> 17.19
        var b = HeatOnly(out var heat);
        b.Quantities[heat.Id] = 0.233m;

        var charge = b.Calculate(Sep2026);

        var line = Assert.Single(charge.Lines);
        Assert.Equal((0.233m, 73.76m, 17.19m), (line.Quantity, line.UnitPrice, line.Net));
        Assert.False(line.AmountFromBill);
        Assert.Empty(charge.Warnings);
    }

    [Fact]
    public void An_amount_from_the_bill_replaces_quantity_times_price()
    {
        // The supplier bills the unrounded quantity: 17.22 on the bill, quantity * price alone would say 17.19.
        // Range: (0.233 - 0.0005) * 73.76 = 17.1492 and (0.233 + 0.0005) * 73.76 = 17.2230, widened by 0.01 -> 17.14 ... 17.23
        var b = HeatOnly(out var heat);
        b.Quantities[heat.Id] = 0.233m;
        b.Amounts[heat.Id] = 17.22m;

        var charge = b.Calculate(Sep2026);

        var line = Assert.Single(charge.Lines);
        Assert.Equal((0.233m, 73.76m, 17.22m), (line.Quantity, line.UnitPrice, line.Net));
        Assert.True(line.AmountFromBill);
        Assert.True(line.IsComplete);
        Assert.Empty(charge.Warnings);

        // VAT 12 % of 17.22 = 2.0664 -> 2.07; total 19.29
        Assert.Equal((17.22m, 2.07m, 19.29m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Theory]
    [InlineData("17.14", false)]
    [InlineData("17.23", false)]
    [InlineData("17.13", true)]
    [InlineData("17.24", true)]
    [InlineData("18.00", true)]
    [InlineData("0", true)]
    public void The_amount_is_checked_against_the_range_of_the_unrounded_quantity(string amount, bool warns)
    {
        // Range for 0.233 MWh * 73.76: 17.1392 ... 17.2330 (including the 0.01 widening)
        var typed = decimal.Parse(amount, CultureInfo.InvariantCulture);
        var b = HeatOnly(out var heat);
        b.Quantities[heat.Id] = 0.233m;
        b.Amounts[heat.Id] = typed;

        var charge = b.Calculate(Sep2026);

        var warning = charge.Warnings.SingleOrDefault(w => w.Code == ChargeWarningCodes.AmountMismatch);
        Assert.Equal(warns, warning is not null);
        if (warning is not null)
        {
            Assert.Equal(heat.Id, warning.ComponentId);
        }

        // The typed amount is still used: the bill is the reference.
        Assert.Equal(typed, Assert.Single(charge.Lines).Net);
    }

    [Fact]
    public void An_amount_far_from_quantity_times_price_warns_that_the_price_may_have_changed()
    {
        // 0.233 * 73.76 = 17.19, the bill says 18.00
        var b = HeatOnly(out var heat);
        b.Quantities[heat.Id] = 0.233m;
        b.Amounts[heat.Id] = 18.00m;

        var charge = b.Calculate(Sep2026);

        var warning = Assert.Single(charge.Warnings);
        Assert.Equal((ChargeWarningCodes.AmountMismatch, heat.Id), (warning.Code, warning.ComponentId));
        Assert.True(charge.IsComplete);
        Assert.Equal(18.00m, charge.Lines[0].Net);
    }

    [Fact]
    public void An_amount_without_a_quantity_is_ignored_and_the_quantity_is_still_asked_for()
    {
        var b = HeatOnly(out var heat);
        b.Amounts[heat.Id] = 17.22m;

        var charge = b.Calculate(Sep2026);

        Assert.False(charge.IsComplete);
        var warning = Assert.Single(charge.Warnings);
        Assert.Equal(ChargeWarningCodes.QuantityFromBill, warning.Code);
    }

    [Fact]
    public void An_amount_is_rounded_to_cents()
    {
        // 17.224 -> 17.22
        var b = HeatOnly(out var heat);
        b.Quantities[heat.Id] = 0.233m;
        b.Amounts[heat.Id] = 17.224m;

        Assert.Equal(17.22m, Assert.Single(b.Calculate(Sep2026).Lines).Net);
    }

    [Fact]
    public void Amount_of_a_gross_priced_line_is_the_gross_of_the_line()
    {
        // Price 82.61 incl. 12 % VAT: 0.233 * 82.61 = 19.24813 -> 19.25 without the amount.
        // Bill amount 19.28 (range 19.1968 ... 19.2994): net = round(19.28 / 1.12) = round(17.2143) = 17.21; VAT 19.28 - 17.21 = 2.07
        var b = HeatOnly(out var heat, includesVat: true, price: 82.61m);
        b.Quantities[heat.Id] = 0.233m;

        Assert.Equal(19.25m, Assert.Single(b.Calculate(Sep2026).Lines).Gross);

        b.Amounts[heat.Id] = 19.28m;
        var charge = b.Calculate(Sep2026);

        var line = Assert.Single(charge.Lines);
        Assert.Equal((17.21m, 2.07m, 19.28m), (line.Net, line.Vat, line.Gross));
        Assert.True(line.PriceIncludesVat);
        Assert.Empty(charge.Warnings);
        Assert.Equal((17.21m, 2.07m, 19.28m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Fact]
    public void Full_heating_bill_with_an_amount_from_the_bill()
    {
        // Own meters   8.370 m3 * 3.47 = 29.0439 -> 29.04
        // Heat         0.233 MWh, amount from the bill 17.22 (quantity * price would be 17.19)
        // Difference   0.164 m3 * 3.47 = 0.56908 -> 0.57
        // Net 29.04 + 17.22 + 0.57 = 46.83; VAT 12 % of 46.83 = 5.6196 -> 5.62; total 52.45
        var b = new ChargeBuilder().Window(20, 26);
        var hot = b.AddMeter(Meter(D(2020, 1, 1), 6));
        hot.Add(D(2026, 8, 26), 100.000m);
        hot.Add(D(2026, 9, 26), 108.370m);
        b.Add("Own hot water", ChargeBasis.PerUnit, 3.47m, "LV-REDUCED", sortOrder: 0);
        var heat = b.Add("Heat", ChargeBasis.QuantityFromBill, 73.76m, "LV-REDUCED", sortOrder: 1);
        heat.QuantityUnit = MeterUnit.MWh;
        var difference = b.Add("Hot water difference", ChargeBasis.QuantityFromBill, 3.47m, "LV-REDUCED", sortOrder: 2);
        difference.QuantityUnit = MeterUnit.M3;
        b.Quantities[heat.Id] = 0.233m;
        b.Quantities[difference.Id] = 0.164m;
        b.Amounts[heat.Id] = 17.22m;

        var charge = b.Calculate(Sep2026);

        Assert.True(charge.IsComplete);
        Assert.Empty(charge.Warnings);
        Assert.Equal([29.04m, 17.22m, 0.57m], charge.Lines.Select(l => l.Net));
        var group = Assert.Single(charge.VatGroups!);
        Assert.Equal((12m, 46.83m, 5.62m, 52.45m), (group.VatPercent, group.Net, group.Vat, group.Gross));
        Assert.Equal((46.83m, 5.62m, 52.45m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }

    [Fact]
    public void A_bill_saved_before_amounts_existed_calculates_as_before()
    {
        // No amounts at all (such a bill's input has AmountsFromBill = null): 0.233 * 73.76 = 17.19; VAT 2.0628 -> 2.06; total 19.25
        var b = HeatOnly(out var heat);
        var quantities = new Dictionary<Guid, decimal> { [heat.Id] = 0.233m };

        var charge = ChargeCalculator.Calculate(new ChargeInput(b.Contract, b.Property, b.Components, b.Links, b.Meters, b.Vat, Sep2026, quantities));

        Assert.Empty(charge.Warnings);
        Assert.False(Assert.Single(charge.Lines).AmountFromBill);
        Assert.Equal((17.19m, 2.06m, 19.25m), (charge.TotalNet, charge.TotalVat, charge.TotalGross));
    }
}
