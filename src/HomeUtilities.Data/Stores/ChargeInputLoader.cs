using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IChargeInputLoader"/>.</summary>
public sealed class ChargeInputLoader(IDbContextFactory<HomeUtilitiesDbContext> factory) : IChargeInputLoader
{
    /// <inheritdoc />
    public async Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null)
        {
            return null;
        }

        var property = await db.Properties.AsNoTracking().Include(p => p.Parameters)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.Id == contract.PropertyId, ct);
        if (property is null)
        {
            return null;
        }

        var components = await TariffStore.IncludeAll(db.TariffComponents.AsNoTracking())
            .Where(t => t.ContractId == contractId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .AsSplitQuery().ToListAsync(ct);
        var contractMeters = await db.ContractMeters.AsNoTracking()
            .Where(c => c.ContractId == contractId)
            .OrderBy(c => c.ValidFrom)
            .ToListAsync(ct);
        var meters = await MeterStore.IncludeAll(db.Meters.AsNoTracking())
            .Where(m => m.PropertyId == contract.PropertyId)
            .OrderBy(m => m.InstalledOn).ThenBy(m => m.Id)
            .AsSplitQuery().ToListAsync(ct);
        var vatCatalog = await db.VatRates.AsNoTracking().Include(v => v.Versions)
            .OrderBy(v => v.Code)
            .AsSplitQuery().ToListAsync(ct);

        // Quantities typed from the bill(s) of the period; the newest bill wins per component.
        var quantityBills = await db.Bills.AsNoTracking()
            .Where(b => b.ContractId == contractId && b.Period == period)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);
        var (quantities, amounts) = BillQuantitySelection.From(quantityBills);

        return new ChargeInput(
            contract,
            property,
            [.. components.Select(TariffStore.SortChildren)],
            contractMeters,
            [.. meters.Select(MeterStore.SortChildren)],
            vatCatalog,
            period,
            quantities,
            amounts);
    }
}
