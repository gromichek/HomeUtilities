namespace HomeUtilities.ViewModels;

/// <summary>
/// The filters of the Payments screen, remembered for the running session (the screen's view model is recreated when the
/// user leaves and comes back). Registered as a singleton.
/// </summary>
public sealed class PaymentFilterState
{
    /// <summary>Only the payments of this property; null = all properties.</summary>
    public Guid? PropertyId { get; set; }

    /// <summary>The year of the payment dates; null = the current year.</summary>
    public int? Year { get; set; }

    /// <summary>Show the payments of every year.</summary>
    public bool AllYears { get; set; }

    /// <summary>Only the payments of this contract; null = all contracts.</summary>
    public Guid? ContractId { get; set; }
}
