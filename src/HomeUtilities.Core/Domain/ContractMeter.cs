using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Link between a contract and a meter whose readings it bills.</summary>
public class ContractMeter : SyncEntity, IVersioned
{
    /// <summary>The contract.</summary>
    public Guid ContractId { get; set; }

    /// <summary>The meter.</summary>
    public Guid MeterId { get; set; }

    /// <summary>First day the link applies.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Last day the link applies; null if open-ended.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>True if the link is not deleted and applies on the given date (both bounds inclusive).</summary>
    public bool IsActiveOn(DateOnly date) =>
        !IsDeleted && ValidFrom <= date && (ValidTo is null || date <= ValidTo);
}
