namespace HomeUtilities.ViewModels;

/// <summary>
/// The filters of the Payments screen, remembered for the running session (the screen's view model is recreated when the
/// user leaves and comes back). Registered as a singleton.
/// </summary>
public sealed class PaymentFilterState
{
    /// <summary>Only the payments of this property; null = all properties.</summary>
    public Guid? PropertyId { get; set; }

    /// <summary>The period filter on the payment dates; null = the current year.</summary>
    public PeriodFilter? Period { get; set; }

    /// <summary>Only the payments of this contract; null = all contracts.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Whether the user expanded (true) or collapsed (false) the filter block; null = not chosen, follows the layout.</summary>
    public bool? FiltersExpanded { get; set; }
}
