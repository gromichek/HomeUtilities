namespace HomeUtilities.Core.Billing;

/// <summary>Payment status of a bill, derived from payments.</summary>
/// <param name="Paid">Sum of the allocations to the bill.</param>
/// <param name="Open">Gross minus paid.</param>
/// <param name="IsPaid">True when nothing is open.</param>
/// <param name="IsOverdue">True when something is open and the due date has passed.</param>
/// <param name="IsPrepaid">True when the bill is paid and belongs to a future month.</param>
public sealed record BillState(decimal Paid, decimal Open, bool IsPaid, bool IsOverdue, bool IsPrepaid);
