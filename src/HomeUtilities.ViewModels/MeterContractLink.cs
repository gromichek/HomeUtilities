using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels;

/// <summary>A contract of the property together with the open link to the meter being edited or replaced.</summary>
/// <param name="Contract">The contract.</param>
/// <param name="OpenLinks">Links between the meter and the contract that have not ended yet (normally zero or one).</param>
public sealed record MeterContractLink(Contract Contract, IReadOnlyList<ContractMeter> OpenLinks)
{
    /// <summary>True if the meter is linked to the contract.</summary>
    public bool IsLinked => OpenLinks.Count > 0;

    /// <summary>A link that has not ended: not deleted, and no end date or an end date that is not in the past.</summary>
    public static bool IsOpen(ContractMeter link, DateOnly today) =>
        !link.IsDeleted && (link.ValidTo is null || link.ValidTo >= today);
}
