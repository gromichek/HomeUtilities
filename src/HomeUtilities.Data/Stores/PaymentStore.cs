using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IPaymentStore"/>.</summary>
public sealed class PaymentStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : StoreBase<Payment>(factory), IPaymentStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Payment>> ListByContractAsync(Guid contractId, CancellationToken ct = default)
    {
        // The query filters hide deleted bills and allocations inside the subqueries too.
        await using var db = await Factory.CreateDbContextAsync(ct);
        var result = await Query(db)
            .Where(p => p.ContractId == contractId
                || p.Allocations.Any(a => db.Bills.Any(b => b.Id == a.BillId && b.ContractId == contractId)))
            .OrderBy(p => p.PaidOn).ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Payment>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var result = await Query(db)
            .Where(p => p.PropertyId == propertyId
                || db.Contracts.Any(c => c.Id == p.ContractId && c.PropertyId == propertyId)
                || p.Allocations.Any(a => db.Bills.Any(b => b.Id == a.BillId && b.PropertyId == propertyId)))
            .OrderBy(p => p.PaidOn).ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);
        return result;
    }

    /// <inheritdoc />
    protected override IQueryable<Payment> Include(IQueryable<Payment> query) => query.Include(p => p.Allocations);

    /// <inheritdoc />
    protected override IQueryable<Payment> Order(IQueryable<Payment> query) =>
        query.OrderBy(p => p.PaidOn).ThenBy(p => p.CreatedAt);

    private IQueryable<Payment> Query(HomeUtilitiesDbContext db) =>
        Include(db.Payments.AsNoTracking()).AsSplitQuery();
}
