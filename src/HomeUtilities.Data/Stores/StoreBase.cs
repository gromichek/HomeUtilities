using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>
/// Shared implementation of <see cref="IStore{T}"/>: a new short-lived context per call, no-tracking reads with
/// split queries, graph saves through <see cref="AggregateMerger"/>.
/// </summary>
public abstract class StoreBase<T>(IDbContextFactory<HomeUtilitiesDbContext> factory) : IStore<T>
    where T : SyncEntity
{
    /// <summary>The context factory.</summary>
    protected IDbContextFactory<HomeUtilitiesDbContext> Factory { get; } = factory;

    /// <summary>Adds the <c>Include</c>s that load the whole aggregate.</summary>
    protected abstract IQueryable<T> Include(IQueryable<T> query);

    /// <summary>Default order of lists.</summary>
    protected abstract IQueryable<T> Order(IQueryable<T> query);

    /// <inheritdoc />
    public async Task<T?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var result = await Read(db).FirstOrDefaultAsync(e => e.Id == id, ct);
        return result is null ? null : Normalize(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) => ListAsync(q => q, ct);

    /// <inheritdoc />
    public async Task SaveAsync(T aggregate, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        await AggregateMerger.SaveAsync(db, aggregate, Include, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var existing = await Include(db.Set<T>()).AsSplitQuery().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existing is null)
        {
            return;
        }

        db.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Lists the aggregates matching a filter, in the default order.</summary>
    protected async Task<IReadOnlyList<T>> ListAsync(Func<IQueryable<T>, IQueryable<T>> filter, CancellationToken ct)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var result = await Order(filter(Read(db))).ToListAsync(ct);
        return [.. result.Select(Normalize)];
    }

    /// <summary>Sorts the child collections of a loaded aggregate; the default does nothing.</summary>
    protected virtual T Normalize(T aggregate) => aggregate;

    private IQueryable<T> Read(HomeUtilitiesDbContext db) =>
        Include(db.Set<T>().AsNoTracking()).AsSplitQuery();
}
