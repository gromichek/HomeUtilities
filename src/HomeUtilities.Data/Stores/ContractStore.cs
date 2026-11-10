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
    public async Task<ContractCategoryRecords> CountCategoryRecordsAsync(Guid contractId, Guid oldCategoryId, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var bills = await db.Bills.CountAsync(b => b.ContractId == contractId && b.CategoryId == oldCategoryId, ct);
        var templates = await db.BillTemplates.CountAsync(t => t.ContractId == contractId && t.CategoryId == oldCategoryId, ct);
        return new ContractCategoryRecords(bills, templates);
    }

    /// <inheritdoc />
    public async Task<ContractCategoryRecords> SaveWithCategoryChangeAsync(Contract contract, Guid oldCategoryId, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AggregateMerger.SaveAsync(db, contract, Include, ct);

        // Soft-deleted rows are hidden by the global filter; the interceptor stamps UpdatedAt so sync carries the change.
        var bills = await db.Bills.Where(b => b.ContractId == contract.Id && b.CategoryId == oldCategoryId).ToListAsync(ct);
        var templates = await db.BillTemplates.Where(t => t.ContractId == contract.Id && t.CategoryId == oldCategoryId).ToListAsync(ct);
        foreach (var bill in bills)
        {
            bill.CategoryId = contract.CategoryId;
        }

        foreach (var template in templates)
        {
            template.CategoryId = contract.CategoryId;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ContractCategoryRecords(bills.Count, templates.Count);
    }

    /// <inheritdoc />
    protected override IQueryable<Contract> Include(IQueryable<Contract> query) => query;

    /// <inheritdoc />
    protected override IQueryable<Contract> Order(IQueryable<Contract> query) =>
        query.OrderBy(c => c.Title).ThenBy(c => c.ValidFrom);
}
