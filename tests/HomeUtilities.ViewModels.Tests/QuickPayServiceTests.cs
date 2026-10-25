using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.ViewModels;

namespace HomeUtilities.ViewModels.Tests;

public class QuickPayServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    private readonly FakeBillStore _bills = new();
    private readonly FakePaymentStore _payments = new();
    private readonly FakeContractStore _contracts = new();
    private readonly FakeSupplierStore _suppliers = new();
    private readonly FakePropertyStore _properties = new();
    private readonly Property _property = new() { Name = "Sample flat" };
    private readonly Supplier _supplier = new()
    {
        Name = "Water utility Ltd",
        PaymentDetails = new PaymentDetails(null, null, null, null, null, "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}"),
    };

    private readonly Contract _contract;
    private readonly QuickPayService _service;

    public QuickPayServiceTests()
    {
        _properties.Stored.Add(_property);
        _suppliers.Stored.Add(_supplier);
        _contract = new Contract { PropertyId = _property.Id, SupplierId = _supplier.Id, Title = "Water", CustomerNumber = "C-100" };
        _contracts.Stored.Add(_contract);
        _payments.BillContractResolver = id => _bills.All.FirstOrDefault(b => b.Id == id)?.ContractId;
        _service = new QuickPayService(_bills, _payments, _contracts, _suppliers, _properties, new TestClock(Today));
    }

    private Bill AddBill(decimal gross, YearMonth? period = null, string? number = null)
    {
        var bill = new Bill
        {
            PropertyId = _property.Id,
            CategoryId = Guid.NewGuid(),
            ContractId = _contract.Id,
            SupplierId = _supplier.Id,
            Period = period ?? new YearMonth(2026, 10),
            Gross = gross,
            DueOn = new DateOnly(2026, 11, 20),
            Number = number,
        };
        _bills.All.Add(bill);
        return bill;
    }

    private async Task<BillState> StateOf(Bill bill) =>
        BillStatus.Of(bill, await _payments.ListByBillAsync(bill.Id), Today);

    private Payment AddManualPayment(decimal amount, params (Bill Bill, decimal Amount)[] allocations)
    {
        var payment = new Payment { ContractId = _contract.Id, PaidOn = Today.AddDays(-5), Amount = amount };
        foreach (var (bill, allocated) in allocations)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = allocated });
        }

        _payments.Add(payment);
        return payment;
    }

    [Fact]
    public async Task MarkPaid_CreatesOnePaymentForTheOpenAmount_AllocatedToTheBill()
    {
        var bill = AddBill(46.02m, number: "W-77");

        var payment = await _service.MarkPaidAsync(bill.Id);

        Assert.NotNull(payment);
        var stored = Assert.Single(_payments.Stored);
        Assert.Equal(46.02m, stored.Amount);
        Assert.Equal(Today, stored.PaidOn);
        Assert.Equal(PaymentMethod.BankTransfer, stored.Method);
        Assert.Equal(bill.Currency, stored.Currency);
        Assert.Equal(_contract.Id, stored.ContractId);
        Assert.Equal(_property.Id, stored.PropertyId);
        Assert.True(stored.CreatedByQuickAction);
        var allocation = Assert.Single(stored.Allocations);
        Assert.Equal((bill.Id, 46.02m), (allocation.BillId, allocation.Amount));
        var state = await StateOf(bill);
        Assert.True(state.IsPaid);
        Assert.Equal(0m, state.Open);
    }

    [Fact]
    public async Task MarkPaid_UsesThePaymentPurposeOfTheContract_WithPeriodAndCustomerNumber()
    {
        var bill = AddBill(46.02m);

        await _service.MarkPaidAsync(bill.Id);

        Assert.Equal("Abon. Nr. C-100, 10.2026", Assert.Single(_payments.Stored).Reference);
    }

    [Fact]
    public async Task MarkPaid_ContractTemplateWinsOverTheSupplierOne_AndFillsBillNumberAndAmount()
    {
        _contract.PaymentDetails = new PaymentDetails(null, null, null, null, null, "Bill {BillNumber} {Amount} {PropertyName}");
        var bill = AddBill(46.02m, number: "W-77");

        await _service.MarkPaidAsync(bill.Id);

        Assert.Equal("Bill W-77 46.02 Sample flat", Assert.Single(_payments.Stored).Reference);
    }

    [Fact]
    public async Task MarkPaid_WithoutATemplate_HasNoReference()
    {
        _supplier.PaymentDetails = null;
        var bill = AddBill(10m);

        await _service.MarkPaidAsync(bill.Id);

        Assert.Null(Assert.Single(_payments.Stored).Reference);
    }

    [Fact]
    public async Task MarkPaid_OneOffExpense_UsesTheTitleAsReference()
    {
        var bill = Bill.CreateOneOff(_property.Id, Guid.NewGuid(), "Boiler repair", new YearMonth(2026, 10), new TestClock(Today));
        bill.Gross = 120m;
        _bills.All.Add(bill);

        var payment = await _service.MarkPaidAsync(bill.Id);

        Assert.NotNull(payment);
        Assert.Equal("Boiler repair", payment.Reference);
        Assert.Null(payment.ContractId);
        Assert.Equal(_property.Id, payment.PropertyId);
    }

    [Fact]
    public async Task MarkPaid_PartiallyPaidBill_PaysOnlyTheRest()
    {
        // 46.02 billed, 20.00 paid by hand earlier: 26.02 open
        var bill = AddBill(46.02m);
        AddManualPayment(20m, (bill, 20m));
        Assert.Equal(26.02m, (await StateOf(bill)).Open);

        await _service.MarkPaidAsync(bill.Id);

        var quick = Assert.Single(_payments.Stored, p => p.CreatedByQuickAction);
        Assert.Equal(26.02m, quick.Amount);
        Assert.Equal(26.02m, Assert.Single(quick.Allocations).Amount);
        Assert.True((await StateOf(bill)).IsPaid);
    }

    [Fact]
    public async Task MarkPaid_DoubleTap_CreatesOnlyOnePayment()
    {
        var bill = AddBill(46.02m);

        var results = await Task.WhenAll(_service.MarkPaidAsync(bill.Id), _service.MarkPaidAsync(bill.Id));

        Assert.Single(results, r => r is not null);
        Assert.Single(_payments.Stored);
        Assert.Equal(46.02m, (await StateOf(bill)).Paid);
    }

    [Fact]
    public async Task MarkPaid_AlreadyPaidBill_DoesNothing()
    {
        var bill = AddBill(46.02m);
        AddManualPayment(46.02m, (bill, 46.02m));

        Assert.Null(await _service.MarkPaidAsync(bill.Id));
        Assert.Single(_payments.Stored);
    }

    [Fact]
    public async Task MarkPaid_OverpaidBill_DoesNothing()
    {
        var bill = AddBill(40m);
        AddManualPayment(50m, (bill, 50m));

        Assert.Null(await _service.MarkPaidAsync(bill.Id));
        Assert.Single(_payments.Stored);
    }

    [Fact]
    public async Task MarkPaid_UnknownBill_DoesNothing()
    {
        Assert.Null(await _service.MarkPaidAsync(Guid.NewGuid()));
        Assert.Empty(_payments.All);
    }

    [Fact]
    public async Task Undo_SoftDeletesThePaymentAndItsAllocation_BillIsUnpaidAgain()
    {
        var bill = AddBill(46.02m);
        var payment = (await _service.MarkPaidAsync(bill.Id))!;
        Assert.True((await StateOf(bill)).IsPaid);

        await _service.UndoAsync(payment.Id);

        Assert.Empty(_payments.Stored);
        var kept = Assert.Single(_payments.All);
        Assert.True(kept.IsDeleted);
        Assert.All(kept.Allocations, a => Assert.True(a.IsDeleted));
        var state = await StateOf(bill);
        Assert.Equal((0m, 46.02m, false), (state.Paid, state.Open, state.IsPaid));
    }

    [Fact]
    public async Task PaidAfterUndo_CreatesAFreshPayment()
    {
        var bill = AddBill(46.02m);
        var first = (await _service.MarkPaidAsync(bill.Id))!;
        await _service.UndoAsync(first.Id);

        var second = await _service.MarkPaidAsync(bill.Id);

        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Single(_payments.Stored);
        Assert.True((await StateOf(bill)).IsPaid);
    }

    [Fact]
    public async Task RemovePayment_QuickPaymentOfThisBillOnly_IsSoftDeletedAsAWhole()
    {
        var bill = AddBill(46.02m);
        var payment = (await _service.MarkPaidAsync(bill.Id))!;

        var outcome = await _service.RemovePaymentAsync(bill.Id, payment.Id);

        Assert.Equal(RemovePaymentOutcome.PaymentDeleted, outcome);
        Assert.Empty(_payments.Stored);
        Assert.Single(_payments.All);
        Assert.False((await StateOf(bill)).IsPaid);
    }

    [Fact]
    public async Task RemovePayment_PaymentCoveringSeveralBills_RemovesOnlyThisBillsAllocation()
    {
        var october = AddBill(46.02m);
        var november = AddBill(30m, new YearMonth(2026, 11));
        var payment = AddManualPayment(100m, (october, 46.02m), (november, 30m));

        var outcome = await _service.RemovePaymentAsync(october.Id, payment.Id);

        Assert.Equal(RemovePaymentOutcome.AllocationRemoved, outcome);
        var kept = Assert.Single(_payments.Stored);
        Assert.Equal(100m, kept.Amount);
        Assert.Equal(november.Id, Assert.Single(kept.Allocations, a => !a.IsDeleted).BillId);
        Assert.Contains(kept.Allocations, a => a.BillId == october.Id && a.IsDeleted);
        Assert.False((await StateOf(october)).IsPaid);
        Assert.True((await StateOf(november)).IsPaid);
    }

    [Fact]
    public async Task RemovePayment_QuickPaymentThatNowCoversTwoBills_KeepsThePayment()
    {
        var october = AddBill(46.02m);
        var november = AddBill(30m, new YearMonth(2026, 11));
        var payment = (await _service.MarkPaidAsync(october.Id))!;
        var stored = _payments.All.Single(p => p.Id == payment.Id);
        stored.Amount += 30m;
        stored.Allocations.Add(new PaymentAllocation { PaymentId = stored.Id, BillId = november.Id, Amount = 30m });

        var outcome = await _service.RemovePaymentAsync(october.Id, payment.Id);

        Assert.Equal(RemovePaymentOutcome.AllocationRemoved, outcome);
        Assert.Single(_payments.Stored);
        Assert.True((await StateOf(november)).IsPaid);
        Assert.False((await StateOf(october)).IsPaid);
    }

    [Fact]
    public async Task RemovePayment_ManualPaymentOfThisBillOnly_StaysAsPrepayment()
    {
        var bill = AddBill(46.02m);
        var payment = AddManualPayment(46.02m, (bill, 46.02m));

        var outcome = await _service.RemovePaymentAsync(bill.Id, payment.Id);

        Assert.Equal(RemovePaymentOutcome.AllocationRemoved, outcome);
        Assert.Equal(46.02m, Assert.Single(_payments.Stored).Amount);
        Assert.False((await StateOf(bill)).IsPaid);
    }

    [Fact]
    public async Task RemovePayment_PaymentNotAllocatedToTheBill_DoesNothing()
    {
        var bill = AddBill(46.02m);
        var other = AddBill(10m, new YearMonth(2026, 9));
        var payment = AddManualPayment(10m, (other, 10m));

        Assert.Null(await _service.RemovePaymentAsync(bill.Id, payment.Id));
        Assert.Empty(_payments.Saved);
        Assert.Empty(_payments.Deleted);
    }

    [Fact]
    public async Task OutcomeFor_PredictsWhatRemoveDoes()
    {
        var bill = AddBill(46.02m);
        var other = AddBill(10m, new YearMonth(2026, 9));
        var quick = (await _service.MarkPaidAsync(bill.Id))!;
        var multi = AddManualPayment(60m, (bill, 50m), (other, 10m));

        Assert.Equal(RemovePaymentOutcome.PaymentDeleted, QuickPayService.OutcomeFor(quick, bill.Id));
        Assert.Equal(RemovePaymentOutcome.AllocationRemoved, QuickPayService.OutcomeFor(multi, bill.Id));
    }

    [Fact]
    public async Task ReleasePayments_ForADeletedBill_DeletesQuickPaymentsAndKeepsOtherMoneyAsPrepayment()
    {
        var bill = AddBill(100m);
        var other = AddBill(10m, new YearMonth(2026, 9));
        var manual = AddManualPayment(30m, (bill, 30m));
        var shared = AddManualPayment(80m, (bill, 70m), (other, 10m));
        var quick = (await _service.MarkPaidAsync(AddBill(25m, new YearMonth(2026, 8)).Id))!;
        var quickAllocation = Assert.Single(quick.Allocations);
        // The quick payment also pays the bill that is going away (an edited payment): it now covers two bills.
        var storedQuick = _payments.All.Single(p => p.Id == quick.Id);
        storedQuick.Allocations.Add(new PaymentAllocation { PaymentId = quick.Id, BillId = bill.Id, Amount = 0.01m });
        var quickOnly = (await _service.MarkPaidAsync(AddBill(5m, new YearMonth(2026, 7)).Id))!;

        await _service.ReleasePaymentsAsync(bill.Id);

        Assert.Empty(await _payments.ListByBillAsync(bill.Id));
        Assert.Contains(_payments.Stored, p => p.Id == manual.Id && p.Amount == 30m);
        Assert.Contains(_payments.Stored, p => p.Id == shared.Id && p.Amount == 80m);
        Assert.Contains(_payments.Stored, p => p.Id == quick.Id);
        Assert.Contains(_payments.Stored, p => p.Id == quickOnly.Id);
        Assert.Equal(quickAllocation.BillId, Assert.Single(_payments.Stored.Single(p => p.Id == quick.Id).Allocations, a => !a.IsDeleted).BillId);
    }

    [Fact]
    public async Task ReleasePayments_QuickPaymentOfOnlyThisBill_IsDeleted()
    {
        var bill = AddBill(100m);
        var quick = (await _service.MarkPaidAsync(bill.Id))!;

        await _service.ReleasePaymentsAsync(bill.Id);

        Assert.DoesNotContain(_payments.Stored, p => p.Id == quick.Id);
        Assert.Single(_payments.All);
        Assert.True(_payments.All[0].IsDeleted);
    }
}
