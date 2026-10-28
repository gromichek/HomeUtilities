using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IBillStore"/>.</summary>
public sealed class BillStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Bill>(factory), IBillStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Bill>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        ListAsync(q => q.Where(b => b.ContractId == contractId && !b.IsDraft), ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<Bill>> ListByPropertyAsync(Guid propertyId, YearMonth? from, YearMonth? to, CancellationToken ct = default) =>
        ListAsync(
            q =>
            {
                q = q.Where(b => b.PropertyId == propertyId && !b.IsDraft);
                if (from is { } start)
                {
                    q = q.Where(b => b.Period >= start);
                }

                if (to is { } end)
                {
                    q = q.Where(b => b.Period <= end);
                }

                return q;
            },
            ct);

    /// <inheritdoc />
    protected override IQueryable<Bill> Include(IQueryable<Bill> query) => query.Include(b => b.Lines);

    /// <inheritdoc />
    protected override IQueryable<Bill> Order(IQueryable<Bill> query) =>
        query.OrderBy(b => b.Period).ThenBy(b => b.CreatedAt);

    /// <inheritdoc />
    protected override Bill Normalize(Bill aggregate)
    {
        aggregate.Lines = [.. aggregate.Lines.OrderBy(l => l.SortOrder)];
        return aggregate;
    }
}
