namespace HomeUtilities.Core.Billing;

/// <summary>Derived figures and editing rules of a <see cref="Payment"/>.</summary>
public static class PaymentRules
{
    /// <summary>Sum of the non-deleted allocations (same sign as the payment).</summary>
    public static decimal Allocated(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return payment.Allocations.Where(a => !a.IsDeleted).Sum(a => a.Amount);
    }

    /// <summary>
    /// The part of the payment that settles no bill: the prepayment of a positive payment (zero when the payment is
    /// fully allocated; negative when it is over-allocated).
    /// </summary>
    public static decimal Unallocated(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return payment.Amount - Allocated(payment);
    }

    /// <summary>
    /// A quick-action payment is "the Paid tap of one bill": one allocation that equals the payment amount. Once it covers
    /// several bills, or its amount differs from its allocation (it was edited into a partial payment or a prepayment), it
    /// is a normal payment and the flag goes. Payments that never had the flag stay as they are.
    /// </summary>
    public static bool KeepsQuickFlag(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!payment.CreatedByQuickAction)
        {
            return false;
        }

        var active = payment.Allocations.Where(a => !a.IsDeleted).ToList();
        return active.Count == 1 && active[0].Amount == payment.Amount;
    }

    /// <summary>Clears <see cref="Payment.CreatedByQuickAction"/> when <see cref="KeepsQuickFlag"/> says the payment is no longer a quick one.</summary>
    public static void ApplyQuickFlagRule(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        payment.CreatedByQuickAction = KeepsQuickFlag(payment);
    }
}
