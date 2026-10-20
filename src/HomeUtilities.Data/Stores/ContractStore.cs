using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IContractStore"/>.</summary>
public sealed class ContractStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Contract>(factory), IContractStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Contract>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        ListAsync(q => q.Where(c => c.PropertyId == propertyId), ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContractMeter>> ListContractMetersAsync(Guid contractId, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        return await db.ContractMeters.AsNoTracking()
            .Where(c => c.ContractId == contractId)
            .OrderBy(c => c.ValidFrom)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task SaveContractMeterAsync(ContractMeter contractMeter, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        await AggregateMerger.SaveAsync(db, contractMeter, q => q, ct);
    }

    /// <inheritdoc />
    protected override IQueryable<Contract> Include(IQueryable<Contract> query) => query;

    /// <inheritdoc />
    protected override IQueryable<Contract> Order(IQueryable<Contract> query) =>
        query.OrderBy(c => c.Title).ThenBy(c => c.ValidFrom);
}
