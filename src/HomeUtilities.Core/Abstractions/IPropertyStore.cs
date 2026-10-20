using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of properties.</summary>
public interface IPropertyStore : IStore<Property>
{
    /// <summary>Properties ordered by sort order then name; archived ones only if requested.</summary>
    Task<IReadOnlyList<Property>> ListAsync(bool includeArchived, CancellationToken ct = default);
}
