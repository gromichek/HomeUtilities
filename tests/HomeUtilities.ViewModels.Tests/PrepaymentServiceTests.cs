using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class PrepaymentServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    private readonly FakeBillStore _bills = new();
    private readonly FakePaymentStore _payments = new();
    private readonly Contract _contract = new() { PropertyId = Guid.NewGuid(), SupplierId = Guid.NewGuid(), Title = "Internet" };
    private readonly PrepaymentService _service;

    public PrepaymentServiceTests()
    {
        _payments.BillContractResolver = id => _bills.All.FirstOrDefault(b => b.Id == id)?.ContractId;
        _service = new PrepaymentService(_bills, _payments, new TestClock(Today));
    }

    private Bill AddBill(decimal gross, YearMonth period, Guid? contractId = null)
    {
        var bill = new Bill
        {
            PropertyId = _contract.PropertyId,
            CategoryId = Guid.NewGuid(),
            ContractId = contractId ?? _contract.Id,
            Period = period,
            Gross = gross,
            DueOn = period.LastDay,
        };
        _bills.All.Add(bill);
        return bill;
    }

    private Payment AddPayment(decimal amount, DateOnly paidOn, params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment { ContractId = _contract.Id, PaidOn = paidOn, Amount = amount };
        foreach (var (bill, allocated) in allocations)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = allocated });
        }

        _payments.Add(payment);
        return payment;
    }

    private async Task<BillState> StateOf(Bill bill) => BillStatus.Of(bill, await _payments.ListByBillAsync(bill.Id), Today);

    [Fact]
    public async Task Available_is_the_unallocated_money_of_the_contracts_own_payments()
    {
        var paid = AddBill(57.70m, new YearMonth(2026, 10));
        AddPayment(70m, Today.AddDays(-10), (paid, 57.70m));
        AddPayment(20m, Today.AddDays(-5));

        // (70.00 - 57.70) + 20.00 = 32.30
        Assert.Equal(32.30m, await _service.AvailableAsync(_contract.Id));
    }

    [Fact]
    public async Task Available_ignores_payments_of_other_contracts_and_deleted_payments()
    {
        var other = AddBill(10m, new YearMonth(2026, 10), Guid.NewGuid());
        _payments.Add(new Payment { ContractId = Guid.NewGuid(), PaidOn = Today, Amount = 50m });
        var deleted = AddPayment(40m, Today);
        deleted.IsDeleted = true;
        AddPayment(30m, Today, (other, 10m)); // 30 - 10 = 20 for this contract (the bill is of another contract, still this payment's money)

        Assert.Equal(20m, await _service.AvailableAsync(_contract.Id));
    }

    [Fact]
    public async Task UseForBill_70_for_a_57_70_bill_leaves_12_30_and_partly_pays_the_next_bill()
    {
        var october = AddBill(57.70m, new YearMonth(2026, 10));
        AddPayment(70m, Today.AddDays(-10), (october, 57.70m));
        var november = AddBill(46.02m, new YearMonth(2026, 11));

        var result = await _service.UseForBillAsync(november.Id);

        // 46.02 open - 12.30 prepayment = 33.72 open
        Assert.NotNull(result);
        Assert.Equal(12.30m, result.Amount);
        var state = await StateOf(november);
        Assert.Equal(12.30m, state.Paid);
        Assert.Equal(33.72m, state.Open);
        Assert.Equal(0m, await _service.AvailableAsync(_contract.Id));
        Assert.True((await StateOf(october)).IsPaid);
    }

    [Fact]
    public async Task UseForBill_never_allocates_more_than_the_open_amount()
    {
        AddPayment(100m, Today.AddDays(-10));
        var bill = AddBill(46.02m, new YearMonth(2026, 11));

        var result = await _service.UseForBillAsync(bill.Id);

        Assert.Equal(46.02m, result!.Amount);
        Assert.True((await StateOf(bill)).IsPaid);
        Assert.Equal(53.98m, await _service.AvailableAsync(_contract.Id));
    }

    [Fact]
    public async Task UseForBill_takes_the_oldest_payment_first_and_then_the_next()
    {
        var older = AddPayment(20m, Today.AddDays(-20));
        var newer = AddPayment(50m, Today.AddDays(-10));
        var bill = AddBill(60m, new YearMonth(2026, 11));

        var result = await _service.UseForBillAsync(bill.Id);

        // 20.00 from the older + 40.00 from the newer = 60.00
        Assert.Equal(60m, result!.Amount);
        Assert.Equal([older.Id, newer.Id], result.Changes.Select(c => c.PaymentId));
        Assert.Equal([20m, 40m], result.Changes.Select(c => c.Amount));
        Assert.Equal(10m, await _service.AvailableAsync(_contract.Id));
        Assert.True((await StateOf(bill)).IsPaid);
    }

    [Fact]
    public async Task UseForBill_adds_to_an_existing_allocation_of_the_same_payment()
    {
        var bill = AddBill(46.02m, new YearMonth(2026, 11));
        var payment = AddPayment(60m, Today.AddDays(-2), (bill, 30m));

        var result = await _service.UseForBillAsync(bill.Id);

        // open 46.02 - 30.00 = 16.02, taken from the unallocated 30.00
        Assert.Equal(16.02m, result!.Amount);
        var stored = Assert.Single(_payments.Stored);
        var allocation = Assert.Single(stored.Allocations, a => !a.IsDeleted);
        Assert.Equal(46.02m, allocation.Amount);
        Assert.Equal(payment.Id, stored.Id);
        Assert.Equal(13.98m, PaymentRules.Unallocated(stored));
    }

    [Fact]
    public async Task UseForBill_without_prepayment_or_open_amount_does_nothing()
    {
        var paid = AddBill(40m, new YearMonth(2026, 10));
        AddPayment(40m, Today, (paid, 40m));
        var open = AddBill(46.02m, new YearMonth(2026, 11));

        Assert.Null(await _service.UseForBillAsync(open.Id));
        Assert.Null(await _service.UseForBillAsync(paid.Id));
        Assert.Null(await _service.UseForBillAsync(Guid.NewGuid()));
        Assert.Equal(0m, (await StateOf(open)).Paid);
    }

    [Fact]
    public async Task UseForBill_of_a_bill_without_contract_does_nothing()
    {
        AddPayment(40m, Today);
        var oneOff = new Bill { PropertyId = _contract.PropertyId, CategoryId = Guid.NewGuid(), Kind = BillKind.OneOff, Title = "Repair", Gross = 20m, Period = new YearMonth(2026, 10) };
        _bills.All.Add(oneOff);

        Assert.Null(await _service.UseForBillAsync(oneOff.Id));
    }

    [Fact]
    public async Task Undo_takes_the_money_back_so_it_is_a_prepayment_again()
    {
        var older = AddPayment(20m, Today.AddDays(-20));
        var newer = AddPayment(50m, Today.AddDays(-10));
        var bill = AddBill(60m, new YearMonth(2026, 11));
        var result = await _service.UseForBillAsync(bill.Id);

        await _service.UndoAsync(result!);

        var state = await StateOf(bill);
        Assert.Equal(0m, state.Paid);
        Assert.Equal(60m, state.Open);
        Assert.Equal(70m, await _service.AvailableAsync(_contract.Id));
        Assert.Equal(20m, PaymentRules.Unallocated(_payments.Stored.Single(p => p.Id == older.Id)));
        Assert.Equal(50m, PaymentRules.Unallocated(_payments.Stored.Single(p => p.Id == newer.Id)));
    }

    [Fact]
    public async Task Undo_of_an_increased_allocation_restores_the_previous_amount()
    {
        var bill = AddBill(46.02m, new YearMonth(2026, 11));
        AddPayment(60m, Today.AddDays(-2), (bill, 30m));
        var result = await _service.UseForBillAsync(bill.Id);

        await _service.UndoAsync(result!);

        var allocation = Assert.Single(Assert.Single(_payments.Stored).Allocations, a => !a.IsDeleted);
        Assert.Equal(30m, allocation.Amount);
    }

    [Fact]
    public async Task UseForOpenBills_spreads_the_prepayment_over_the_oldest_open_bills_first()
    {
        var oct = AddBill(46.02m, new YearMonth(2026, 10));
        var nov = AddBill(46.02m, new YearMonth(2026, 11));
        var dec = AddBill(46.02m, new YearMonth(2026, 12));
        AddPayment(100m, Today.AddDays(-3));

        var result = await _service.UseForOpenBillsAsync(_contract.Id);

        // 100.00 -> 46.02 (Oct) + 46.02 (Nov) + 7.96 (Dec)
        Assert.Equal(100m, result!.Amount);
        Assert.True((await StateOf(oct)).IsPaid);
        Assert.True((await StateOf(nov)).IsPaid);
        var december = await StateOf(dec);
        Assert.Equal(7.96m, december.Paid);
        Assert.Equal(38.06m, december.Open);
        Assert.Equal(0m, await _service.AvailableAsync(_contract.Id));
    }

    [Fact]
    public async Task UseForOpenBills_uses_the_oldest_payment_first_and_counts_what_is_already_paid()
    {
        var oct = AddBill(50m, new YearMonth(2026, 10));
        var nov = AddBill(50m, new YearMonth(2026, 11));
        AddPayment(30m, Today.AddDays(-30), (oct, 30m)); // fully allocated: no prepayment
        AddPayment(25m, Today.AddDays(-5));

        var result = await _service.UseForOpenBillsAsync(_contract.Id);

        // October has 20.00 open (50.00 - 30.00): 20.00 go there, 5.00 to November
        Assert.Equal(25m, result!.Amount);
        Assert.True((await StateOf(oct)).IsPaid);
        Assert.Equal(5m, (await StateOf(nov)).Paid);
    }

    [Fact]
    public async Task UseForOpenBills_can_be_undone()
    {
        var oct = AddBill(46.02m, new YearMonth(2026, 10));
        AddPayment(100m, Today.AddDays(-3));
        var result = await _service.UseForOpenBillsAsync(_contract.Id);

        await _service.UndoAsync(result!);

        Assert.Equal(0m, (await StateOf(oct)).Paid);
        Assert.Equal(100m, await _service.AvailableAsync(_contract.Id));
    }

    [Fact]
    public async Task UseForOpenBills_without_prepayment_or_open_bills_returns_null()
    {
        Assert.Null(await _service.UseForOpenBillsAsync(_contract.Id));

        AddPayment(40m, Today);
        Assert.Null(await _service.UseForOpenBillsAsync(_contract.Id));
    }
}
