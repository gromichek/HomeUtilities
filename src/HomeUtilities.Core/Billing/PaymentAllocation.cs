using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>The part of a payment that settles a bill.</summary>
public class PaymentAllocation : SyncEntity
{
    /// <summary>The payment.</summary>
    public Guid PaymentId { get; set; }

    /// <summary>The bill that is settled.</summary>
    public Guid BillId { get; set; }

    /// <summary>Allocated amount; has the same sign as the payment.</summary>
    public decimal Amount { get; set; }

    /// <summary>Free-text comment.</summary>
    public string? Note { get; set; }
}
