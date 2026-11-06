using HomeUtilities.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>EF Core implementation of <see cref="IDeleteImpactService"/>: counts rows with SQL, loads nothing.</summary>
public sealed class DeleteImpactService(IDbContextFactory<HomeUtilitiesDbContext> factory) : IDeleteImpactService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Dependant>> ForPropertyAsync(Guid propertyId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var contractIds = db.Contracts.Where(c => c.PropertyId == propertyId).Select(c => c.Id);
        return Build(
            (DependantKind.Contracts, await db.Contracts.CountAsync(c => c.PropertyId == propertyId, ct)),
            (DependantKind.Meters, await db.Meters.CountAsync(m => m.PropertyId == propertyId, ct)),
            (DependantKind.Bills, await db.Bills.CountAsync(b => b.PropertyId == propertyId && !b.IsDraft, ct)),
            (DependantKind.Payments, await db.Payments.CountAsync(
                p => p.PropertyId == propertyId || (p.ContractId != null && contractIds.Contains(p.ContractId.Value)), ct)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dependant>> ForContractAsync(Guid contractId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return Build(
            (DependantKind.Bills, await db.Bills.CountAsync(b => b.ContractId == contractId && !b.IsDraft, ct)),
            (DependantKind.Payments, await db.Payments.CountAsync(p => p.ContractId == contractId, ct)),
            (DependantKind.Tariffs, await db.TariffComponents.CountAsync(t => t.ContractId == contractId, ct)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dependant>> ForSupplierAsync(Guid supplierId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var contractIds = db.Contracts.Where(c => c.SupplierId == supplierId).Select(c => c.Id);
        return Build(
            (DependantKind.Contracts, await db.Contracts.CountAsync(c => c.SupplierId == supplierId, ct)),
            (DependantKind.Bills, await db.Bills.CountAsync(
                b => !b.IsDraft && (b.SupplierId == supplierId || (b.ContractId != null && contractIds.Contains(b.ContractId.Value))), ct)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dependant>> ForMeterAsync(Guid meterId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return Build(
            (DependantKind.Readings, await db.MeterReadings.CountAsync(
                r => db.MeterRegisters.Any(g => g.Id == r.RegisterId && g.MeterId == meterId), ct)),
            (DependantKind.Links, await db.ContractMeters.CountAsync(l => l.MeterId == meterId, ct)));
    }

    private static IReadOnlyList<Dependant> Build(params (DependantKind Kind, int Count)[] counts) =>
        [.. counts.Where(c => c.Count > 0).Select(c => new Dependant(c.Kind, c.Count))];
}
