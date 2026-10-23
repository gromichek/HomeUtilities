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

    /// <summary>
    /// Adds new readings to existing registers in one transaction: either all of them are stored or none.
    /// <see cref="MeterReading.RegisterId"/> is set from the pair.
    /// </summary>
    /// <param name="readings">The registers and the new readings to store in them.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">A register does not exist.</exception>
    Task AddReadingsAsync(IEnumerable<(Guid RegisterId, MeterReading Reading)> readings, CancellationToken ct = default);

    /// <summary>
    /// Updates stored readings (date, value, kind, submission date, note, photo) in one transaction.
    /// </summary>
    /// <param name="readings">The readings with their new values; matched by id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">A reading does not exist.</exception>
    Task UpdateReadingsAsync(IEnumerable<MeterReading> readings, CancellationToken ct = default);

    /// <summary>Soft-deletes one reading. Does nothing if it does not exist.</summary>
    Task DeleteReadingAsync(Guid readingId, CancellationToken ct = default);
}
