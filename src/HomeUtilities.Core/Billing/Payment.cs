using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>Money paid. The part not allocated to a bill is a prepayment.</summary>
public class Payment : SyncEntity
{
    /// <summary>Contract the payment is for; null for a generic payment that covers several contracts.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Property of a payment without a contract.</summary>
    public Guid? PropertyId { get; set; }

    /// <summary>Date of payment.</summary>
    public DateOnly PaidOn { get; set; }

    /// <summary>Amount paid; negative for a refund received.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency of the amount.</summary>
    public Currency Currency { get; set; } = Currency.EUR;

    /// <summary>How the payment was made.</summary>
    public PaymentMethod Method { get; set; }

    /// <summary>
    /// True for a payment created by the "Paid" quick action of a bill. Such a payment covers exactly one bill, so
    /// removing it later deletes the payment as a whole.
    /// </summary>
    public bool CreatedByQuickAction { get; set; }

    /// <summary>Payment reference or purpose text.</summary>
    public string? Reference { get; set; }

    /// <summary>The bills this payment settles.</summary>
    public List<PaymentAllocation> Allocations { get; set; } = [];

    /// <summary>The Excel import that created the record; null for records entered by hand.</summary>
    public Guid? ImportBatchId { get; set; }

    /// <summary>Free-text comment.</summary>
    public string? Notes { get; set; }
}
