using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels;

/// <summary>One allocation that <see cref="PrepaymentService"/> added to a payment (or increased); the unit of its undo.</summary>
/// <param name="PaymentId">The payment whose unallocated money was used.</param>
/// <param name="AllocationId">The allocation that was added or increased.</param>
/// <param name="Amount">The amount that was added to the allocation.</param>
public sealed record PrepaymentChange(Guid PaymentId, Guid AllocationId, decimal Amount);

/// <summary>What <see cref="PrepaymentService"/> did: the total moved to bills and the changes, for the undo.</summary>
/// <param name="Amount">Total amount that was allocated.</param>
/// <param name="Changes">The allocations that were added or increased.</param>
public sealed record PrepaymentResult(decimal Amount, IReadOnlyList<PrepaymentChange> Changes);

/// <summary>
/// "Use prepayment" (docs/PAYMENT_TYPES.md, payments and balance): moves the unallocated money of a contract's payments to
/// its open bills. Nothing is deleted; the undo takes the moved amounts away from the allocations again.
/// </summary>
public sealed class PrepaymentService(IBillStore bills, IPaymentStore payments, IClock clock)
{
    // One change at a time, so a double tap cannot use the same money twice.
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>The unallocated money of the contract's own payments (the prepayment), oldest first.</summary>
    public async Task<decimal> AvailableAsync(Guid contractId, CancellationToken ct = default) =>
        (await SourcesAsync(contractId, null, ct)).Sum(p => PaymentRules.Unallocated(p));

    /// <summary>
    /// Allocates the oldest unallocated money of the bill's contract to the bill, never more than its open amount. Returns
    /// null when the bill has no contract, nothing is open or there is no prepayment.
    /// </summary>
    public async Task<PrepaymentResult?> UseForBillAsync(Guid billId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (await bills.GetAsync(billId, ct) is not { ContractId: { } contractId } bill)
            {
                return null;
            }

            var open = BillStatus.Of(bill, await payments.ListByBillAsync(billId, ct), clock.Today).Open;
            if (open <= 0)
            {
                return null;
            }

            var changes = new List<PrepaymentChange>();
            foreach (var payment in await SourcesAsync(contractId, bill.Currency, ct))
            {
                if (open == 0)
                {
                    break;
                }

                var take = Math.Min(PaymentRules.Unallocated(payment), open);
                var allocation = payment.Allocations.FirstOrDefault(a => !a.IsDeleted && a.BillId == billId);
                if (allocation is null)
                {
                    allocation = new PaymentAllocation { PaymentId = payment.Id, BillId = billId, Amount = take };
                    payment.Allocations.Add(allocation);
                }
                else
                {
                    allocation.Amount += take;
                }

                PaymentRules.ApplyQuickFlagRule(payment);
                await payments.SaveAsync(payment, ct);
                changes.Add(new PrepaymentChange(payment.Id, allocation.Id, take));
                open -= take;
            }

            return changes.Count == 0 ? null : new PrepaymentResult(changes.Sum(c => c.Amount), changes);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// "Use for open bills" of a contract: every payment with unallocated money, oldest first, is spread over the contract's
    /// open bills with <see cref="PaymentAllocator.AutoAllocate"/>. Returns null when nothing could be allocated.
    /// </summary>
    public async Task<PrepaymentResult?> UseForOpenBillsAsync(Guid contractId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var contractBills = await bills.ListByContractAsync(contractId, ct);
            var related = await payments.ListByContractAsync(contractId, ct);
            var open = contractBills.ToDictionary(
                b => b.Id,
                b => b.Gross - related.Where(p => !p.IsDeleted).SelectMany(p => p.Allocations)
                    .Where(a => !a.IsDeleted && a.BillId == b.Id).Sum(a => a.Amount));

            var changes = new List<PrepaymentChange>();
            foreach (var payment in await SourcesAsync(contractId, null, ct))
            {
                var added = PaymentAllocator.AutoAllocate(payment, contractBills, id => open.GetValueOrDefault(id));
                if (added.Count == 0)
                {
                    continue;
                }

                foreach (var allocation in added)
                {
                    payment.Allocations.Add(allocation);
                    open[allocation.BillId] -= allocation.Amount;
                    changes.Add(new PrepaymentChange(payment.Id, allocation.Id, allocation.Amount));
                }

                PaymentRules.ApplyQuickFlagRule(payment);
                await payments.SaveAsync(payment, ct);
            }

            return changes.Count == 0 ? null : new PrepaymentResult(changes.Sum(c => c.Amount), changes);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Undo of <see cref="UseForBillAsync"/> / <see cref="UseForOpenBillsAsync"/>: takes the moved amounts back, so the money is a prepayment again.</summary>
    public async Task UndoAsync(PrepaymentResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        await _gate.WaitAsync(ct);
        try
        {
            foreach (var group in result.Changes.GroupBy(c => c.PaymentId))
            {
                if (await payments.GetAsync(group.Key, ct) is not { } payment)
                {
                    continue;
                }

                foreach (var change in group)
                {
                    if (payment.Allocations.FirstOrDefault(a => !a.IsDeleted && a.Id == change.AllocationId) is not { } allocation)
                    {
                        continue;
                    }

                    allocation.Amount -= change.Amount;
                    if (allocation.Amount == 0)
                    {
                        payment.Allocations.Remove(allocation);
                    }
                }

                await payments.SaveAsync(payment, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The contract's own payments with unallocated money (positive), oldest first.</summary>
    private async Task<List<Payment>> SourcesAsync(Guid contractId, Core.Common.Currency? currency, CancellationToken ct) =>
    [
        .. (await payments.ListByContractAsync(contractId, ct))
            .Where(p => !p.IsDeleted && p.ContractId == contractId && (currency is null || p.Currency == currency) && PaymentRules.Unallocated(p) > 0)
            .OrderBy(p => p.PaidOn)
            .ThenBy(p => p.CreatedAt),
    ];
}
