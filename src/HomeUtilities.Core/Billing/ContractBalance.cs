namespace HomeUtilities.Core.Billing;

/// <summary>Derives the balance of a contract.</summary>
public static class ContractBalance
{
    /// <summary>
    /// Balance on <paramref name="date"/>. Billed = gross of the contract's non-deleted bills issued on or before
    /// the date (the first day of the period if no issue date). Paid = payments of the contract up to the date,
    /// plus the allocations to the contract's bills from payments without a contract, up to the date.
    /// </summary>
    public static Balance Of(Guid contractId, IEnumerable<Bill> bills, IEnumerable<Payment> payments, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(payments);

        var contractBills = bills.Where(b => !b.IsDeleted && !b.IsDraft && b.ContractId == contractId).ToList();
        var billIds = contractBills.Select(b => b.Id).ToHashSet();
        var billed = contractBills
            .Where(b => (b.IssuedOn ?? b.Period.FirstDay) <= date)
            .Sum(b => b.Gross);

        var current = payments.Where(p => !p.IsDeleted && p.PaidOn <= date).ToList();
        var paid = current.Where(p => p.ContractId == contractId).Sum(p => p.Amount)
            + current
                .Where(p => p.ContractId is null)
                .SelectMany(p => p.Allocations)
                .Where(a => !a.IsDeleted && billIds.Contains(a.BillId))
                .Sum(a => a.Amount);
        return new(billed, paid, paid - billed);
    }
}
