using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class PaymentValidatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 20);

    private static string[] Codes(Payment payment, params Bill[] bills) =>
        [.. PaymentValidator.Validate(payment, bills.ToDictionary(b => b.Id)).Errors.Select(e => e.Code)];

    [Fact]
    public void Valid_payment_passes()
    {
        var bill = BillFor(Contract(), 60m);
        Assert.Empty(Codes(Pay(100m, Day, allocations: (bill, 60m)), bill));
    }

    [Fact]
    public void Unallocated_payment_passes() => Assert.Empty(Codes(Pay(100m, Day)));

    [Fact]
    public void Zero_amount_is_rejected() => Assert.Equal([ErrorCodes.PaymentAmountZero], Codes(Pay(0m, Day)));

    [Fact]
    public void Zero_and_wrong_sign_allocations_are_rejected()
    {
        var a = BillFor(Contract(), 60m);
        var b = BillFor(Contract(), 60m);
        var payment = Pay(100m, Day, allocations: [(a, 0m), (b, -10m)]);
        Assert.Equal([ErrorCodes.PaymentAllocationAmountZero, ErrorCodes.PaymentAllocationSignMismatch], Codes(payment, a, b));
    }

    [Fact]
    public void Negative_payment_takes_negative_allocations()
    {
        var bill = BillFor(Contract(), -30m);
        bill.Kind = BillKind.Correction;
        Assert.Empty(Codes(Pay(-50m, Day, allocations: (bill, -30m)), bill));
        Assert.Equal([ErrorCodes.PaymentAllocationSignMismatch], Codes(Pay(-50m, Day, allocations: (bill, 30m)), bill));
    }

    [Fact]
    public void Over_allocation_is_rejected_and_exact_allocation_passes()
    {
        var a = BillFor(Contract(), 60m);
        var b = BillFor(Contract(), 60m);
        Assert.Equal([ErrorCodes.PaymentOverAllocated], Codes(Pay(100m, Day, allocations: [(a, 60m), (b, 50m)]), a, b));
        Assert.Empty(Codes(Pay(100m, Day, allocations: [(a, 60m), (b, 40m)]), a, b));
    }

    [Fact]
    public void Duplicate_bill_is_rejected()
    {
        var bill = BillFor(Contract(), 60m);
        Assert.Equal([ErrorCodes.PaymentAllocationDuplicateBill], Codes(Pay(100m, Day, allocations: [(bill, 20m), (bill, 20m)]), bill));
    }

    [Fact]
    public void Unknown_or_deleted_bill_is_rejected()
    {
        var bill = BillFor(Contract(), 60m);
        Assert.Equal([ErrorCodes.PaymentAllocationBillUnknown], Codes(Pay(100m, Day, allocations: (bill, 20m))));

        bill.IsDeleted = true;
        Assert.Equal([ErrorCodes.PaymentAllocationBillUnknown], Codes(Pay(100m, Day, allocations: (bill, 20m)), bill));
    }

    [Fact]
    public void Currency_mismatch_is_rejected()
    {
        var bill = BillFor(Contract(), 60m);
        var payment = Pay(100m, Day, allocations: (bill, 20m));
        payment.Currency = Currency.LVL;
        Assert.Equal([ErrorCodes.PaymentAllocationCurrencyMismatch], Codes(payment, bill));
    }

    [Fact]
    public void Deleted_allocations_are_ignored_and_notes_are_limited()
    {
        var bill = BillFor(Contract(), 60m);
        var payment = Pay(100m, Day, allocations: [(bill, 500m)]);
        payment.Allocations[0].IsDeleted = true;
        payment.Notes = new string('x', 2001);
        Assert.Equal([ErrorCodes.NotesTooLong], Codes(payment, bill));
    }
}
