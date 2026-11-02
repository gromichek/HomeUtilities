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
        var many = await LoadManyAsync([(contractId, period)], ct);
        return many.GetValueOrDefault((contractId, period));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<(Guid ContractId, YearMonth Period), ChargeInput>> LoadManyAsync(
        IReadOnlyCollection<(Guid ContractId, YearMonth Period)> requests, CancellationToken ct = default)
    {
        var result = new Dictionary<(Guid, YearMonth), ChargeInput>();
        var wanted = requests.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return result;
        }

        var contractIds = wanted.Select(r => r.ContractId).Distinct().ToList();
        await using var db = await factory.CreateDbContextAsync(ct);

        // One query per table for all requested contracts and months; the pairs are assembled in memory.
        var contracts = (await db.Contracts.AsNoTracking().Where(c => contractIds.Contains(c.Id)).ToListAsync(ct))
            .ToDictionary(c => c.Id);
        var propertyIds = contracts.Values.Select(c => c.PropertyId).Distinct().ToList();
        var properties = (await db.Properties.AsNoTracking().Include(p => p.Parameters)
                .AsSplitQuery().Where(p => propertyIds.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);
        var components = (await TariffStore.IncludeAll(db.TariffComponents.AsNoTracking())
                .Where(t => contractIds.Contains(t.ContractId))
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .AsSplitQuery().ToListAsync(ct))
            .Select(TariffStore.SortChildren)
            .ToLookup(t => t.ContractId);
        var contractMeters = (await db.ContractMeters.AsNoTracking()
                .Where(c => contractIds.Contains(c.ContractId))
                .OrderBy(c => c.ValidFrom)
                .ToListAsync(ct))
            .ToLookup(c => c.ContractId);
        var meters = (await MeterStore.IncludeAll(db.Meters.AsNoTracking())
                .Where(m => propertyIds.Contains(m.PropertyId))
                .OrderBy(m => m.InstalledOn).ThenBy(m => m.Id)
                .AsSplitQuery().ToListAsync(ct))
            .Select(MeterStore.SortChildren)
            .ToLookup(m => m.PropertyId);
        var vatCatalog = await db.VatRates.AsNoTracking().Include(v => v.Versions)
            .OrderBy(v => v.Code)
            .AsSplitQuery().ToListAsync(ct);

        // Quantities typed from the bill(s) of the period; the newest bill wins per component.
        var periods = wanted.Select(r => r.Period).ToHashSet();
        var bills = (await db.Bills.AsNoTracking()
                .Where(b => b.ContractId != null && contractIds.Contains(b.ContractId.Value) && !b.IsDraft)
                .OrderBy(b => b.CreatedAt)
                .ToListAsync(ct))
            .Where(b => periods.Contains(b.Period))
            .ToLookup(b => (b.ContractId!.Value, b.Period));

        foreach (var (contractId, period) in wanted)
        {
            if (!contracts.TryGetValue(contractId, out var contract) || !properties.TryGetValue(contract.PropertyId, out var property))
            {
                continue;
            }

            var (quantities, amounts) = BillQuantitySelection.From([.. bills[(contractId, period)]]);
            result[(contractId, period)] = new ChargeInput(
                contract,
                property,
                [.. components[contractId]],
                [.. contractMeters[contractId]],
                [.. meters[contract.PropertyId]],
                vatCatalog,
                period,
                quantities,
                amounts);
        }

        return result;
    }
}
