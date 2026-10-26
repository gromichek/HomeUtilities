using HomeUtilities.Core.Billing;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class PaymentRulesTests
{
    private static readonly DateOnly PayDay = new(2026, 10, 20);

    [Fact]
    public void Unallocated_is_amount_minus_active_allocations()
    {
        var contract = Contract();
        var bill = BillFor(contract, 57.70m);
        var payment = Pay(70m, PayDay, contract.Id, null, (bill, 57.70m));

        // 70.00 - 57.70 = 12.30 prepayment
        Assert.Equal(57.70m, PaymentRules.Allocated(payment));
        Assert.Equal(12.30m, PaymentRules.Unallocated(payment));
    }

    [Fact]
    public void Deleted_allocations_do_not_count()
    {
        var contract = Contract();
        var bill = BillFor(contract, 40m);
        var payment = Pay(40m, PayDay, contract.Id, null, (bill, 40m));
        payment.Allocations[0].IsDeleted = true;

        Assert.Equal(0m, PaymentRules.Allocated(payment));
        Assert.Equal(40m, PaymentRules.Unallocated(payment));
    }

    [Fact]
    public void Refund_allocated_in_full_has_nothing_unallocated()
    {
        var contract = Contract();
        var correction = BillFor(contract, -12m);
        var refund = Pay(-12m, PayDay, contract.Id, null, (correction, -12m));

        Assert.Equal(0m, PaymentRules.Unallocated(refund));
    }

    [Fact]
    public void Quick_payment_of_one_bill_keeps_the_flag()
    {
        var contract = Contract();
        var bill = BillFor(contract, 46.02m);
        var payment = Pay(46.02m, PayDay, contract.Id, null, (bill, 46.02m));
        payment.CreatedByQuickAction = true;

        PaymentRules.ApplyQuickFlagRule(payment);

        Assert.True(payment.CreatedByQuickAction);
    }

    [Fact]
    public void Quick_payment_allocated_to_two_bills_loses_the_flag()
    {
        var contract = Contract();
        var first = BillFor(contract, 30m);
        var second = BillFor(contract, 16.02m);
        var payment = Pay(46.02m, PayDay, contract.Id, null, (first, 30m), (second, 16.02m));
        payment.CreatedByQuickAction = true;

        PaymentRules.ApplyQuickFlagRule(payment);

        Assert.False(payment.CreatedByQuickAction);
    }

    [Fact]
    public void Quick_payment_whose_amount_differs_from_its_allocation_loses_the_flag()
    {
        var contract = Contract();
        var bill = BillFor(contract, 46.02m);
        var payment = Pay(50m, PayDay, contract.Id, null, (bill, 46.02m));
        payment.CreatedByQuickAction = true;

        PaymentRules.ApplyQuickFlagRule(payment);

        Assert.False(payment.CreatedByQuickAction);
    }

    [Fact]
    public void Quick_payment_without_allocation_loses_the_flag()
    {
        var payment = Pay(46.02m, PayDay);
        payment.CreatedByQuickAction = true;

        PaymentRules.ApplyQuickFlagRule(payment);

        Assert.False(payment.CreatedByQuickAction);
    }

    [Fact]
    public void Quick_payment_with_a_changed_date_keeps_the_flag()
    {
        var contract = Contract();
        var bill = BillFor(contract, 46.02m);
        var payment = Pay(46.02m, PayDay, contract.Id, null, (bill, 46.02m));
        payment.CreatedByQuickAction = true;
        payment.PaidOn = PayDay.AddDays(-5);

        PaymentRules.ApplyQuickFlagRule(payment);

        Assert.True(payment.CreatedByQuickAction);
    }

    [Fact]
    public void Normal_payment_never_gains_the_flag()
    {
        var contract = Contract();
        var bill = BillFor(contract, 46.02m);
        var payment = Pay(46.02m, PayDay, contract.Id, null, (bill, 46.02m));

        PaymentRules.ApplyQuickFlagRule(payment);

        Assert.False(payment.CreatedByQuickAction);
    }
}
