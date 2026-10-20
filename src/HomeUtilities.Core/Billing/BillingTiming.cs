namespace HomeUtilities.Core.Billing;

/// <summary>Whether a contract is billed after or before the service month.</summary>
public enum BillingTiming
{
    /// <summary>Billed after the service month; new bills default to the previous month.</summary>
    InArrears = 0,

    /// <summary>Paid in advance (internet, TV); new bills default to the next month.</summary>
    InAdvance = 1,
}
