using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class ContractBalanceTests
{
    private static readonly DateOnly Date = new(2026, 10, 20);

    [Fact]
    public void Overpayment_is_positive()
    {
        var contract = Contract();
        var bills = new[] { BillFor(contract, 55.25m, issued: new DateOnly(2026, 9, 1)), BillFor(contract, 59.49m, issued: new DateOnly(2026, 10, 1)) };
        var payments = new[] { Pay(60m, new DateOnly(2026, 9, 10), contract.Id), Pay(60m, new DateOnly(2026, 10, 10), contract.Id) };

        // billed 55.25 + 59.49 = 114.74; paid 120; balance +5.26
        Assert.Equal(new Balance(114.74m, 120m, 5.26m), ContractBalance.Of(contract.Id, bills, payments, Date));
    }

    [Fact]
    public void Debt_is_negative()
    {
        var contract = Contract();
        var bills = new[] { BillFor(contract, 55.25m, issued: new DateOnly(2026, 9, 1)) };
        Assert.Equal(-55.25m, ContractBalance.Of(contract.Id, bills, [], Date).Value);
    }

    [Fact]
    public void Property_level_payment_counts_only_its_allocation_to_this_contract()
    {
        var water = Contract();
        var power = Contract();
        var waterBill = BillFor(water, 30m, issued: new DateOnly(2026, 9, 1));
        var powerBill = BillFor(power, 50m, issued: new DateOnly(2026, 9, 1));
        var payment = Pay(100m, new DateOnly(2026, 9, 20), propertyId: water.PropertyId, allocations: [(waterBill, 30m), (powerBill, 50m)]);

        var balance = ContractBalance.Of(water.Id, [waterBill, powerBill], [payment], Date);

        Assert.Equal(new Balance(30m, 30m, 0m), balance); // the unallocated 20 belongs to nobody
    }

    [Fact]
    public void Contract_payment_counts_in_full_and_foreign_contract_payments_are_ignored()
    {
        var contract = Contract();
        var bill = BillFor(contract, 30m, issued: new DateOnly(2026, 9, 1));
        var mine = Pay(100m, new DateOnly(2026, 9, 20), contract.Id);
        var foreign = Pay(500m, new DateOnly(2026, 9, 20), Guid.CreateVersion7(), allocations: (bill, 30m));

        Assert.Equal(70m, ContractBalance.Of(contract.Id, [bill], [mine, foreign], Date).Value); // 100 - 30
    }

    [Fact]
    public void Bills_issued_after_the_date_are_excluded()
    {
        var contract = Contract();
        var past = BillFor(contract, 55.25m, issued: new DateOnly(2026, 10, 20));
        var future = BillFor(contract, 59.49m, issued: new DateOnly(2026, 10, 21));
        Assert.Equal(55.25m, ContractBalance.Of(contract.Id, [past, future], [], Date).Billed);
    }

    [Fact]
    public void Period_start_is_used_when_there_is_no_issue_date()
    {
        var contract = Contract();
        var thisMonth = BillFor(contract, 10m, new YearMonth(2026, 10));
        var nextMonth = BillFor(contract, 20m, new YearMonth(2026, 11));
        Assert.Equal(10m, ContractBalance.Of(contract.Id, [thisMonth, nextMonth], [], Date).Billed);
    }

    [Fact]
    public void Payments_after_the_date_deleted_items_and_other_contracts_are_excluded()
    {
        var contract = Contract();
        var bill = BillFor(contract, 10m, issued: new DateOnly(2026, 9, 1));
        var otherBill = BillFor(Contract(), 99m, issued: new DateOnly(2026, 9, 1));
        var deletedBill = BillFor(contract, 77m, issued: new DateOnly(2026, 9, 1));
        deletedBill.IsDeleted = true;
        var later = Pay(5m, new DateOnly(2026, 10, 21), contract.Id);
        var deleted = Pay(5m, Date, contract.Id);
        deleted.IsDeleted = true;

        var balance = ContractBalance.Of(contract.Id, [bill, otherBill, deletedBill], [later, deleted], Date);

        Assert.Equal(new Balance(10m, 0m, -10m), balance);
    }

    [Fact]
    public void Deleted_allocation_is_not_counted()
    {
        var contract = Contract();
        var bill = BillFor(contract, 30m, issued: new DateOnly(2026, 9, 1));
        var payment = Pay(30m, new DateOnly(2026, 9, 20), propertyId: contract.PropertyId, allocations: (bill, 30m));
        payment.Allocations[0].IsDeleted = true;
        Assert.Equal(0m, ContractBalance.Of(contract.Id, [bill], [payment], Date).Paid);
    }
}
