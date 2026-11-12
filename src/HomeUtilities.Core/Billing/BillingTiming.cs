namespace HomeUtilities.Core.Billing;

/// <summary>Whether a contract is billed after, before or in the service month.</summary>
public enum BillingTiming
{
    /// <summary>Billed after the service month; new bills default to the previous month.</summary>
    InArrears = 0,

    /// <summary>Paid in advance (internet, TV); new bills default to the next month.</summary>
    InAdvance = 1,

    /// <summary>The bill for a month arrives in that same month (e.g. house maintenance); new bills default to this month.</summary>
    InServiceMonth = 2,
}

/// <summary>Maps a <see cref="BillingTiming"/> to a month offset. The one place that does it.</summary>
public static class BillingTimingExtensions
{
    /// <summary>Months between the service period and the month the bill arrives: InArrears +1, InAdvance −1, InServiceMonth 0.</summary>
    public static int ArrivalOffset(this BillingTiming timing) => timing switch
    {
        BillingTiming.InAdvance => -1,
        BillingTiming.InServiceMonth => 0,
        _ => 1,
    };
}
