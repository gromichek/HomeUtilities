using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Equalized;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IEqualizedPlanStore"/>.</summary>
public sealed class EqualizedPlanStore(IDbContextFactory<HomeUtilitiesDbContext> factory)
    : StoreBase<EqualizedPlan>(factory), IEqualizedPlanStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<EqualizedPlan>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        ListAsync(q => q.Where(p => p.ContractId == contractId), ct);

    /// <inheritdoc />
    protected override IQueryable<EqualizedPlan> Include(IQueryable<EqualizedPlan> query) => query;

    /// <inheritdoc />
    protected override IQueryable<EqualizedPlan> Order(IQueryable<EqualizedPlan> query) =>
        query.OrderBy(p => p.ValidFrom).ThenBy(p => p.CreatedAt);
}
