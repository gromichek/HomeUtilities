using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>Derives the payment status of a bill.</summary>
public static class BillStatus
{
    /// <summary>
    /// Status of a bill on a day. Only non-deleted allocations of non-deleted payments count.
    /// Overdue means open &gt; 0 and the due date is before <paramref name="today"/>;
    /// prepaid means paid and the bill's period is after the current month.
    /// </summary>
    public static BillState Of(Bill bill, IEnumerable<Payment> payments, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(payments);

        var paid = payments
            .Where(p => !p.IsDeleted)
            .SelectMany(p => p.Allocations)
            .Where(a => !a.IsDeleted && a.BillId == bill.Id)
            .Sum(a => a.Amount);
        var open = bill.Gross - paid;
        var isPaid = open == 0;
        var isOverdue = open > 0 && bill.DueOn is { } due && due < today;
        var isPrepaid = isPaid && bill.Period > YearMonth.From(today);
        return new(paid, open, isPaid, isOverdue, isPrepaid);
    }
}
