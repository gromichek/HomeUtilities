namespace HomeUtilities.ViewModels;

/// <summary>Which bills the status filter of the Bills screen shows.</summary>
public enum BillStatusFilter
{
    /// <summary>Every bill.</summary>
    All,

    /// <summary>Bills with an open amount (unpaid, partially paid, overdue).</summary>
    Open,

    /// <summary>Open bills past their due date.</summary>
    Overdue,

    /// <summary>Bills with nothing open (paid and prepaid).</summary>
    Paid,
}

/// <summary>
/// The filters of the Bills screen, remembered for the running session (the screen's view model is recreated when the
/// user leaves and comes back). Registered as a singleton.
/// </summary>
public sealed class BillFilterState
{
    /// <summary>Only the bills of this property; null = all properties.</summary>
    public Guid? PropertyId { get; set; }

    /// <summary>The period filter on the bills' periods; null = the current year.</summary>
    public PeriodFilter? Period { get; set; }

    /// <summary>The status filter.</summary>
    public BillStatusFilter Status { get; set; }
}
