using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of meters with their registers and readings.</summary>
public interface IMeterStore : IStore<Meter>
{
    /// <summary>Non-deleted meters of a property including registers and readings.</summary>
    Task<IReadOnlyList<Meter>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default);

    /// <summary>
    /// Saves a meter replacement in one transaction: the old meter (with its Final readings and removal date), the new
    /// meter (with its Initial readings), the old meter's contract links that end and the new links that start.
    /// Either everything is stored or nothing.
    /// </summary>
    /// <param name="oldMeter">The replaced meter as returned by <see cref="MeterReplacement"/>.</param>
    /// <param name="newMeter">The new meter as returned by <see cref="MeterReplacement"/>.</param>
    /// <param name="linksToClose">Existing links of the old meter with their <c>ValidTo</c> set.</param>
    /// <param name="linksToCreate">New links of the new meter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveReplacementAsync(
        Meter oldMeter,
        Meter newMeter,
        IReadOnlyList<ContractMeter> linksToClose,
        IReadOnlyList<ContractMeter> linksToCreate,
        CancellationToken ct = default);
}
