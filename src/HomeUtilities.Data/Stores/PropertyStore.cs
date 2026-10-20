using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IPropertyStore"/>.</summary>
public sealed class PropertyStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Property>(factory), IPropertyStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Property>> ListAsync(bool includeArchived, CancellationToken ct = default) =>
        ListAsync(q => includeArchived ? q : q.Where(p => !p.IsArchived), ct);

    /// <inheritdoc />
    protected override IQueryable<Property> Include(IQueryable<Property> query) => query.Include(p => p.Parameters);

    /// <inheritdoc />
    protected override IQueryable<Property> Order(IQueryable<Property> query) =>
        query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name);

    /// <inheritdoc />
    protected override Property Normalize(Property aggregate)
    {
        aggregate.Parameters = [.. aggregate.Parameters.OrderBy(p => p.Kind).ThenBy(p => p.Name).ThenBy(p => p.ValidFrom)];
        return aggregate;
    }
}
