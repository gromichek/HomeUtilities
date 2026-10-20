using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class BillStatusTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void Unpaid_bill_is_open()
    {
        var bill = BillFor(Contract(), 55.25m, due: new DateOnly(2026, 10, 25));
        var state = BillStatus.Of(bill, [], Today);
        Assert.Equal(new BillState(0m, 55.25m, false, false, false), state);
    }

    [Fact]
    public void Partial_payment_reduces_open_amount()
    {
        var bill = BillFor(Contract(), 55.25m);
        var state = BillStatus.Of(bill, [Pay(20m, Today, allocations: (bill, 20m))], Today);
        Assert.Equal((20m, 35.25m, false), (state.Paid, state.Open, state.IsPaid));
    }

    [Fact]
    public void Full_payment_closes_the_bill()
    {
        var bill = BillFor(Contract(), 55.25m, due: new DateOnly(2026, 10, 1));
        var state = BillStatus.Of(bill, [Pay(30m, Today, allocations: (bill, 30m)), Pay(25.25m, Today, allocations: (bill, 25.25m))], Today);
        Assert.Equal(new BillState(55.25m, 0m, true, false, false), state);
    }

    [Theory]
    [InlineData(19, true)]  // due yesterday -> overdue
    [InlineData(20, false)] // due today -> not yet overdue
    [InlineData(21, false)]
    public void Overdue_boundary(int dueDay, bool overdue)
    {
        var bill = BillFor(Contract(), 10m, due: new DateOnly(2026, 10, dueDay));
        Assert.Equal(overdue, BillStatus.Of(bill, [], Today).IsOverdue);
    }

    [Fact]
    public void Bill_without_due_date_is_never_overdue() =>
        Assert.False(BillStatus.Of(BillFor(Contract(), 10m), [], Today).IsOverdue);

    [Fact]
    public void Paid_bill_is_not_overdue()
    {
        var bill = BillFor(Contract(), 10m, due: new DateOnly(2026, 10, 1));
        Assert.False(BillStatus.Of(bill, [Pay(10m, Today, allocations: (bill, 10m))], Today).IsOverdue);
    }

    [Fact]
    public void Prepaid_internet_bill_for_next_month()
    {
        var bill = BillFor(Contract(timing: BillingTiming.InAdvance), 15m, new YearMonth(2026, 11));
        var payment = Pay(15m, Today, allocations: (bill, 15m));
        Assert.True(BillStatus.Of(bill, [payment], Today).IsPrepaid);
        Assert.False(BillStatus.Of(bill, [], Today).IsPrepaid); // unpaid is not prepaid
    }

    [Fact]
    public void Bill_of_current_or_past_month_is_not_prepaid()
    {
        var bill = BillFor(Contract(), 15m, new YearMonth(2026, 10));
        Assert.False(BillStatus.Of(bill, [Pay(15m, Today, allocations: (bill, 15m))], Today).IsPrepaid);
    }

    [Fact]
    public void Deleted_payment_and_deleted_allocation_are_not_counted()
    {
        var bill = BillFor(Contract(), 10m);
        var deletedPayment = Pay(10m, Today, allocations: (bill, 10m));
        deletedPayment.IsDeleted = true;
        var deletedAllocation = Pay(10m, Today, allocations: (bill, 10m));
        deletedAllocation.Allocations[0].IsDeleted = true;

        var state = BillStatus.Of(bill, [deletedPayment, deletedAllocation], Today);

        Assert.Equal((0m, 10m), (state.Paid, state.Open));
    }

    [Fact]
    public void Allocations_to_other_bills_are_ignored()
    {
        var bill = BillFor(Contract(), 10m);
        var other = BillFor(Contract(), 10m);
        Assert.Equal(0m, BillStatus.Of(bill, [Pay(10m, Today, allocations: (other, 10m))], Today).Paid);
    }
}
