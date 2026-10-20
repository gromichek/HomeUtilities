using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="ISupplierStore"/>.</summary>
public sealed class SupplierStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Supplier>(factory), ISupplierStore
{
    /// <inheritdoc />
    protected override IQueryable<Supplier> Include(IQueryable<Supplier> query) => query;

    /// <inheritdoc />
    protected override IQueryable<Supplier> Order(IQueryable<Supplier> query) => query.OrderBy(s => s.Name);
}
