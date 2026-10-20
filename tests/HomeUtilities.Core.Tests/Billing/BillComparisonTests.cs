using HomeUtilities.Core.Billing;
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
}
