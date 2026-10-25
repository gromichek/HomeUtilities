using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class BillComparisonTests
{
    [Fact]
    public void Bill_above_calculation()
    {
        // 59.49 - 55.25 = 4.24; 4.24 / 55.25 = 7.67% -> 7.7
        var result = BillComparison.Compare(BillFor(Contract(), 59.49m), Charge(55.25m), null);
        Assert.Equal(new BillComparisonResult(4.24m, 7.7m, null), result);
    }

    [Fact]
    public void Bill_below_calculation_is_negative()
    {
        // 50.00 - 55.25 = -5.25; -5.25 / 55.25 = -9.50% -> -9.5
        var result = BillComparison.Compare(BillFor(Contract(), 50m), Charge(55.25m), null);
        Assert.Equal(new BillComparisonResult(-5.25m, -9.5m, null), result);
    }

    [Fact]
    public void Quantity_difference_is_billed_minus_metered()
    {
        var bill = BillFor(Contract(), 59.49m);
        bill.ConsumptionOnBill = 21.0m;
        // 21.0 - 20.114 = 0.886
        Assert.Equal(0.886m, BillComparison.Compare(bill, Charge(55.25m), 20.114m).QuantityDifference);
    }

    [Fact]
    public void Quantity_difference_needs_both_values()
    {
        var bill = BillFor(Contract(), 59.49m);
        Assert.Null(BillComparison.Compare(bill, Charge(55.25m), 20.114m).QuantityDifference);
        bill.ConsumptionOnBill = 21m;
        Assert.Null(BillComparison.Compare(bill, Charge(55.25m), null).QuantityDifference);
    }

    [Fact]
    public void Missing_or_incomplete_calculation_gives_no_difference()
    {
        var bill = BillFor(Contract(), 59.49m);
        Assert.Equal(new BillComparisonResult(null, null, null), BillComparison.Compare(bill, null, null));
        Assert.Equal(new BillComparisonResult(null, null, null), BillComparison.Compare(bill, Charge(55.25m, complete: false), null));
    }

    [Fact]
    public void From_bill_only_calculation_gives_no_difference() =>
        Assert.Null(BillComparison.Compare(BillFor(Contract(), 59.49m), Charge(59.49m, fromBill: true), null).Difference);

    [Fact]
    public void Zero_calculated_total_gives_no_percent()
    {
        var result = BillComparison.Compare(BillFor(Contract(), 10m), Charge(0m), null);
        Assert.Equal(new BillComparisonResult(10m, null, null), result);
    }

    [Theory]
    [InlineData(null, DifferenceTone.Neutral)]
    [InlineData(5.0, DifferenceTone.Neutral)]
    [InlineData(5.1, DifferenceTone.Higher)]
    [InlineData(-5.0, DifferenceTone.Neutral)]
    [InlineData(-5.1, DifferenceTone.Lower)]
    [InlineData(0.0, DifferenceTone.Neutral)]
    [InlineData(2.0, DifferenceTone.Neutral)]
    public void Tone_is_red_above_plus_5_and_green_below_minus_5(double? percent, DifferenceTone expected) =>
        Assert.Equal(expected, BillComparison.ToneOf(percent is null ? null : (decimal)percent));

    private static ChargeLine PerUnit(Guid component, decimal? quantity, ChargeBasis basis = ChargeBasis.PerUnit, bool fromBill = false, bool complete = true) =>
        new(component, "line", ServiceKind.ColdWater, Guid.NewGuid(), basis, quantity, 1m, 1m, 21m, 0.21m, 1.21m, fromBill, complete, []);

    private static CalculatedCharge Calc(params ChargeLine[] lines) =>
        new(Guid.NewGuid(), new YearMonth(2026, 9), Currency.EUR, lines, 0m, 0m, 0m, true, []);

    [Fact]
    public void Metered_quantity_is_the_quantity_of_the_first_per_unit_component()
    {
        var water = Guid.NewGuid();
        var sewer = Guid.NewGuid();

        // water and sewerage both count the same 12.150 m3
        Assert.Equal(12.150m, BillComparison.MeteredQuantity(Calc(PerUnit(water, 12.150m), PerUnit(sewer, 12.150m))));
    }

    [Fact]
    public void Metered_quantity_adds_up_the_slices_of_a_stepped_price()
    {
        var component = Guid.NewGuid();

        Assert.Equal(150m, BillComparison.MeteredQuantity(Calc(PerUnit(component, 100m), PerUnit(component, 50m))));
    }

    [Fact]
    public void Metered_quantity_skips_lines_without_a_metered_quantity()
    {
        var other = Guid.NewGuid();
        var metered = Guid.NewGuid();
        var calc = Calc(
            PerUnit(Guid.NewGuid(), 40m, ChargeBasis.PerArea),
            PerUnit(Guid.NewGuid(), null),
            PerUnit(Guid.NewGuid(), 3m, fromBill: true),
            PerUnit(other, 9m, complete: false),
            PerUnit(metered, 7.5m));

        Assert.Equal(7.5m, BillComparison.MeteredQuantity(calc));
    }

    [Fact]
    public void Metered_quantity_is_unknown_without_a_calculation_or_per_unit_line()
    {
        Assert.Null(BillComparison.MeteredQuantity(null));
        Assert.Null(BillComparison.MeteredQuantity(Calc(PerUnit(Guid.NewGuid(), 40m, ChargeBasis.PerArea))));
    }

    [Fact]
    public void Water_bill_46_02_against_calculated_45_10_is_plus_0_92_or_2_0_percent()
    {
        // 46.02 - 45.10 = 0.92; 0.92 / 45.10 = 2.04% -> 2.0; bill 12.500 m3 vs metered 12.150 m3 -> 0.350 m3
        var bill = BillFor(Contract(), 46.02m);
        bill.ConsumptionOnBill = 12.500m;
        var calculated = Charge(45.10m);

        var result = BillComparison.Compare(bill, calculated, 12.150m);

        Assert.Equal(new BillComparisonResult(0.92m, 2.0m, 0.350m), result);
        Assert.Equal(DifferenceTone.Neutral, BillComparison.ToneOf(result.DifferencePercent));
    }
}
