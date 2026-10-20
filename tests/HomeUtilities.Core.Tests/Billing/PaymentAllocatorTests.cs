using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class PaymentAllocatorTests
{
    private static readonly DateOnly PayDay = new(2026, 10, 20);

    private static Func<Guid, decimal> Open(params Bill[] bills) => id => bills.Single(b => b.Id == id).Gross;

    [Fact]
    public void Fills_oldest_bills_first()
    {
        var contract = Contract();
        var first = BillFor(contract, 30m, due: new DateOnly(2026, 9, 15));
        var second = BillFor(contract, 50m, due: new DateOnly(2026, 10, 15));
        var third = BillFor(contract, 40m, due: new DateOnly(2026, 11, 15));
        var payment = Pay(100m, PayDay, contract.Id);

        // 100 -> 30 + 50 + 20
        var result = PaymentAllocator.AutoAllocate(payment, [third, first, second], Open(first, second, third));

        Assert.Equal([(first.Id, 30m), (second.Id, 50m), (third.Id, 20m)], result.Select(a => (a.BillId, a.Amount)));
        Assert.All(result, a => Assert.Equal(payment.Id, a.PaymentId));
    }

    [Fact]
    public void Partially_paid_bill_uses_its_open_amount()
    {
        var contract = Contract();
        var first = BillFor(contract, 30m, due: new DateOnly(2026, 9, 15));
        var second = BillFor(contract, 50m, due: new DateOnly(2026, 10, 15));
        decimal OpenOf(Guid id) => id == first.Id ? 10m : 50m; // first was partly paid: 20 of 30

        var result = PaymentAllocator.AutoAllocate(Pay(40m, PayDay, contract.Id), [first, second], OpenOf);

        Assert.Equal([(first.Id, 10m), (second.Id, 30m)], result.Select(a => (a.BillId, a.Amount)));
    }

    [Fact]
    public void Excess_stays_unallocated_as_prepayment()
    {
        var contract = Contract();
        var bill = BillFor(contract, 30m, due: new DateOnly(2026, 9, 15));
        var result = PaymentAllocator.AutoAllocate(Pay(100m, PayDay, contract.Id), [bill], Open(bill));
        Assert.Equal(30m, Assert.Single(result).Amount);
    }

    [Fact]
    public void Property_level_payment_covers_two_contracts_of_the_property()
    {
        var water = Contract();
        var electricity = Contract();
        electricity.PropertyId = water.PropertyId;
        var other = Contract();
        var a = BillFor(water, 30m, due: new DateOnly(2026, 10, 1));
        var b = BillFor(electricity, 50m, due: new DateOnly(2026, 10, 5));
        var c = BillFor(other, 70m, due: new DateOnly(2026, 9, 1));

        var result = PaymentAllocator.AutoAllocate(Pay(100m, PayDay, propertyId: water.PropertyId), [a, b, c], Open(a, b, c));

        Assert.Equal([(a.Id, 30m), (b.Id, 50m)], result.Select(x => (x.BillId, x.Amount)));
    }

    [Fact]
    public void Contract_level_payment_ignores_other_contracts()
    {
        var water = Contract();
        var electricity = Contract();
        electricity.PropertyId = water.PropertyId;
        var a = BillFor(water, 30m, due: new DateOnly(2026, 10, 1));
        var b = BillFor(electricity, 50m, due: new DateOnly(2026, 9, 1));

        var result = PaymentAllocator.AutoAllocate(Pay(100m, PayDay, water.Id, water.PropertyId), [a, b], Open(a, b));

        Assert.Equal(a.Id, Assert.Single(result).BillId);
    }

    [Fact]
    public void Order_falls_back_to_issue_date_then_period_end()
    {
        var contract = Contract();
        var byPeriod = BillFor(contract, 10m, new YearMonth(2026, 7));                          // 2026-07-31
        var byIssue = BillFor(contract, 10m, new YearMonth(2026, 9), issued: new DateOnly(2026, 8, 5));
        var byDue = BillFor(contract, 10m, new YearMonth(2026, 10), due: new DateOnly(2026, 7, 1));

        var result = PaymentAllocator.AutoAllocate(Pay(30m, PayDay), [byIssue, byPeriod, byDue], Open(byIssue, byPeriod, byDue));

        Assert.Equal([byDue.Id, byPeriod.Id, byIssue.Id], result.Select(a => a.BillId));
    }

    [Fact]
    public void Skips_deleted_paid_and_other_currency_bills()
    {
        var contract = Contract();
        var deleted = BillFor(contract, 10m, due: new DateOnly(2026, 1, 1));
        deleted.IsDeleted = true;
        var paid = BillFor(contract, 10m, due: new DateOnly(2026, 2, 1));
        var lats = BillFor(contract, 10m, due: new DateOnly(2026, 3, 1));
        lats.Currency = Currency.LVL;
        var open = BillFor(contract, 10m, due: new DateOnly(2026, 4, 1));
        decimal OpenOf(Guid id) => id == paid.Id ? 0m : 10m;

        var result = PaymentAllocator.AutoAllocate(Pay(100m, PayDay), [deleted, paid, lats, open], OpenOf);

        Assert.Equal(open.Id, Assert.Single(result).BillId);
    }

    [Fact]
    public void Only_the_unallocated_part_is_allocated_and_allocated_bills_are_skipped()
    {
        var contract = Contract();
        var first = BillFor(contract, 30m, due: new DateOnly(2026, 9, 15));
        var second = BillFor(contract, 50m, due: new DateOnly(2026, 10, 15));
        var payment = Pay(60m, PayDay, contract.Id, allocations: (first, 30m));

        // 60 - 30 already allocated = 30 left; first is skipped
        var result = PaymentAllocator.AutoAllocate(payment, [first, second], id => id == first.Id ? 0m : 50m);

        Assert.Equal([(second.Id, 30m)], result.Select(a => (a.BillId, a.Amount)));
    }

    [Fact]
    public void Refund_is_allocated_to_negative_open_amounts()
    {
        var contract = Contract();
        var credit = BillFor(contract, -20m, due: new DateOnly(2026, 9, 1));
        credit.Kind = BillKind.Correction;
        var normal = BillFor(contract, 40m, due: new DateOnly(2026, 8, 1));

        var result = PaymentAllocator.AutoAllocate(Pay(-50m, PayDay, contract.Id), [normal, credit], Open(normal, credit));

        Assert.Equal([(credit.Id, -20m)], result.Select(a => (a.BillId, a.Amount)));
    }

    [Fact]
    public void Fully_allocated_payment_gets_nothing()
    {
        var contract = Contract();
        var bill = BillFor(contract, 30m);
        var other = BillFor(contract, 30m);
        Assert.Empty(PaymentAllocator.AutoAllocate(Pay(30m, PayDay, allocations: (bill, 30m)), [other], Open(other)));
    }
}
