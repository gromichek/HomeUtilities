using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Loads and saves one aggregate root together with its child entities.</summary>
/// <typeparam name="T">The aggregate root.</typeparam>
public interface IStore<T>
    where T : SyncEntity
{
    /// <summary>The full aggregate including its children, or null if it does not exist or is deleted.</summary>
    Task<T?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>All non-deleted aggregates including their children.</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    /// <summary>Inserts or updates the whole graph; children missing from <paramref name="aggregate"/> are soft-deleted.</summary>
    Task SaveAsync(T aggregate, CancellationToken ct = default);

    /// <summary>Soft-deletes the aggregate root and its children.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
