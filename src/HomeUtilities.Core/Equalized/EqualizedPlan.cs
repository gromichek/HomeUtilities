using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Equalized;

/// <summary>The fixed monthly amount of an equalized contract (electricity, gas) for a period of time.</summary>
public class EqualizedPlan : SyncEntity, IVersioned
{
    /// <summary>Owning contract.</summary>
    public Guid ContractId { get; set; }

    /// <summary>First day the plan applies.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Last day the plan applies; null = open-ended.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>The amount billed every month, gross (including VAT).</summary>
    public decimal MonthlyAmount { get; set; }

    /// <summary>
    /// The "difference from the previous period" line the supplier adds to every monthly payment of this plan (gross, signed as
    /// printed: negative = refund of an overpayment, positive = surcharge); null = none. It is already part of
    /// <see cref="MonthlyAmount"/>, so the settlement counts the billed amount without it.
    /// </summary>
    public decimal? CarryOverMonthly { get; set; }

    /// <summary>Free-text comment.</summary>
    public string? Notes { get; set; }
}
