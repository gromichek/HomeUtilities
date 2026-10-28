using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.ViewModels;

/// <summary>What happened to a payment when it was removed from a bill.</summary>
public enum RemovePaymentOutcome
{
    /// <summary>The payment was a quick payment of this bill only: it is deleted as a whole.</summary>
    PaymentDeleted,

    /// <summary>Only the bill's share was taken away; the rest of the money stays on the contract as prepayment.</summary>
    AllocationRemoved,
}

/// <summary>
/// The "Paid" quick action of a bill and its undo (docs/PAYMENT_TYPES.md, "Payment status of a bill"). Nothing is
/// ever hard-deleted, so a wrong tap never loses data.
/// </summary>
public sealed class QuickPayService(
    IBillStore bills,
    IPaymentStore payments,
    IContractStore contracts,
    ISupplierStore suppliers,
    IPropertyStore properties,
    IClock clock)
{
    // One quick action at a time, so a double tap on "Paid" cannot create two payments.
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Pays the open amount of the bill: one bank-transfer payment dated today with the contract's payment purpose,
    /// allocated to the bill, saved in one step. Returns null when nothing is open (already paid, e.g. a double tap).
    /// </summary>
    public async Task<Payment?> MarkPaidAsync(Guid billId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var bill = await bills.GetAsync(billId, ct);
            if (bill is null || bill.IsDraft)
            {
                // A draft is confirmed first; nothing is paid on it.
                return null;
            }

            var state = BillStatus.Of(bill, await payments.ListByBillAsync(billId, ct), clock.Today);
            if (state.Open == 0 || Math.Sign(state.Open) != Math.Sign(bill.Gross))
            {
                return null;
            }

            var payment = new Payment
            {
                ContractId = bill.ContractId,
                PropertyId = bill.PropertyId,
                PaidOn = clock.Today,
                Amount = state.Open,
                Currency = bill.Currency,
                Method = PaymentMethod.BankTransfer,
                Reference = await PurposeAsync(bill, state.Open, ct),
                CreatedByQuickAction = true,
            };
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = state.Open });
            await payments.SaveAsync(payment, ct);
            return payment;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Undo of <see cref="MarkPaidAsync"/>: soft-deletes the payment and its allocation.</summary>
    public async Task UndoAsync(Guid paymentId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await payments.DeleteAsync(paymentId, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>What <see cref="RemovePaymentAsync"/> will do with the payment: a quick payment of this bill only is deleted, anything else loses only this bill's allocation.</summary>
    public static RemovePaymentOutcome OutcomeFor(Payment payment, Guid billId)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var active = payment.Allocations.Where(a => !a.IsDeleted).ToList();
        return payment.CreatedByQuickAction && active.Count > 0 && active.All(a => a.BillId == billId)
            ? RemovePaymentOutcome.PaymentDeleted
            : RemovePaymentOutcome.AllocationRemoved;
    }

    /// <summary>"Remove payment" of a bill (PAYMENT_TYPES, "Cancel payment"). Returns what was done; null if the payment is not allocated to the bill.</summary>
    public async Task<RemovePaymentOutcome?> RemovePaymentAsync(Guid billId, Guid paymentId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var payment = await payments.GetAsync(paymentId, ct);
            if (payment is null || !payment.Allocations.Any(a => !a.IsDeleted && a.BillId == billId))
            {
                return null;
            }

            return await RemoveCoreAsync(payment, billId, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Takes every payment away from a bill that is about to be deleted: quick payments of this bill only are deleted,
    /// every other payment keeps its money as prepayment (only the bill's allocation goes).
    /// </summary>
    public async Task ReleasePaymentsAsync(Guid billId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            foreach (var payment in await payments.ListByBillAsync(billId, ct))
            {
                await RemoveCoreAsync(payment, billId, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RemovePaymentOutcome> RemoveCoreAsync(Payment payment, Guid billId, CancellationToken ct)
    {
        var outcome = OutcomeFor(payment, billId);
        if (outcome == RemovePaymentOutcome.PaymentDeleted)
        {
            await payments.DeleteAsync(payment.Id, ct);
        }
        else
        {
            payment.Allocations.RemoveAll(a => a.BillId == billId);
            await payments.SaveAsync(payment, ct);
        }

        return outcome;
    }

    private async Task<string?> PurposeAsync(Bill bill, decimal amount, CancellationToken ct)
    {
        if (bill.ContractId is not { } contractId || await contracts.GetAsync(contractId, ct) is not { } contract)
        {
            return bill.Title;
        }

        var supplier = await suppliers.GetAsync(contract.SupplierId, ct);
        var template = (supplier is null ? contract.PaymentDetails : PaymentDetails.Effective(contract, supplier))?.PurposeTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var property = await properties.GetAsync(bill.PropertyId, ct);
        var text = PaymentPurpose.Render(
            template,
            new PaymentPurposeContext(contract.CustomerNumber, contract.ContractNumber, bill.Number, bill.Period, amount, property?.Name));
        return text.Length > InputLimits.Notes ? text[..InputLimits.Notes] : text;
    }
}
