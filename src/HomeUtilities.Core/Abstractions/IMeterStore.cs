using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of meters with their registers and readings.</summary>
public interface IMeterStore : IStore<Meter>
{
    /// <summary>Non-deleted meters of a property including registers and readings.</summary>
    Task<IReadOnlyList<Meter>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default);
}
