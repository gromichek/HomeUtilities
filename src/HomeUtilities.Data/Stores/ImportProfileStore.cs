using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Import;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IImportProfileStore"/>.</summary>
public sealed class ImportProfileStore(IDbContextFactory<HomeUtilitiesDbContext> factory)
    : StoreBase<SavedImportProfile>(factory), IImportProfileStore
{
    /// <inheritdoc />
    protected override IQueryable<SavedImportProfile> Include(IQueryable<SavedImportProfile> query) => query;

    /// <inheritdoc />
    protected override IQueryable<SavedImportProfile> Order(IQueryable<SavedImportProfile> query) =>
        query.OrderBy(p => p.Name).ThenBy(p => p.CreatedAt);
}
