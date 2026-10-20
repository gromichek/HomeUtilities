using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IVatRateStore"/>.</summary>
public sealed class VatRateStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<VatRate>(factory), IVatRateStore
{
    /// <inheritdoc />
    protected override IQueryable<VatRate> Include(IQueryable<VatRate> query) => query.Include(v => v.Versions);

    /// <inheritdoc />
    protected override IQueryable<VatRate> Order(IQueryable<VatRate> query) => query.OrderBy(v => v.Code);

    /// <inheritdoc />
    protected override VatRate Normalize(VatRate aggregate)
    {
        aggregate.Versions = [.. aggregate.Versions.OrderBy(v => v.ValidFrom)];
        return aggregate;
    }
}
