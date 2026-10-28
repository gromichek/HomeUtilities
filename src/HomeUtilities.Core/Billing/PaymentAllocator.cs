namespace HomeUtilities.Core.Billing;

/// <summary>Proposes how a payment is spread over open bills.</summary>
public static class PaymentAllocator
{
    /// <summary>
    /// Allocates the unallocated part of a payment to open bills, oldest first (by due date, else issue date,
    /// else the last day of the period; then by period). Candidates are non-deleted bills of the payment's
    /// currency with an open amount of the same sign, restricted to the payment's contract, or else to its
    /// property if there is one. A bill is never allocated more than its open amount, and a bill the payment
    /// already allocates to is skipped.
    /// </summary>
    /// <param name="payment">The payment.</param>
    /// <param name="candidates">Bills to choose from.</param>
    /// <param name="openAmountOf">Open amount of a bill by id.</param>
    /// <returns>New allocations; the caller adds them to the payment.</returns>
    public static IReadOnlyList<PaymentAllocation> AutoAllocate(
        Payment payment, IEnumerable<Bill> candidates, Func<Guid, decimal> openAmountOf)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(openAmountOf);

        var sign = Math.Sign(payment.Amount);
        var existing = payment.Allocations.Where(a => !a.IsDeleted).ToList();
        var remaining = Math.Abs(payment.Amount) - existing.Sum(a => Math.Abs(a.Amount));
        if (sign == 0 || remaining <= 0)
        {
            return [];
        }

        var taken = existing.Select(a => a.BillId).ToHashSet();
        var ordered = candidates
            .Where(b => !b.IsDeleted && !b.IsDraft && b.Currency == payment.Currency && !taken.Contains(b.Id))
            .Where(b => payment.ContractId is { } contractId
                ? b.ContractId == contractId
                : payment.PropertyId is not { } propertyId || b.PropertyId == propertyId)
            .OrderBy(b => b.DueOn ?? b.IssuedOn ?? b.Period.LastDay)
            .ThenBy(b => b.Period)
            .ThenBy(b => b.Id);

        var result = new List<PaymentAllocation>();
        foreach (var bill in ordered)
        {
            var open = openAmountOf(bill.Id);
            if (Math.Sign(open) != sign)
            {
                continue;
            }

            var amount = Math.Min(remaining, Math.Abs(open));
            result.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = amount * sign });
            remaining -= amount;
            if (remaining == 0)
            {
                break;
            }
        }

        return result;
    }
}
