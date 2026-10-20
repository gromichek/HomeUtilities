using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="ICategoryStore"/>.</summary>
public sealed class CategoryStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Category>(factory), ICategoryStore
{
    /// <inheritdoc />
    protected override IQueryable<Category> Include(IQueryable<Category> query) => query;

    /// <inheritdoc />
    protected override IQueryable<Category> Order(IQueryable<Category> query) =>
        query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);
}
