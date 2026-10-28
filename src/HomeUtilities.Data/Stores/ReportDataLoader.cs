using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Reports;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IReportDataLoader"/>: one query per table, none per contract or month.</summary>
public sealed class ReportDataLoader(IDbContextFactory<HomeUtilitiesDbContext> factory) : IReportDataLoader
{
    /// <inheritdoc />
    public async Task<ReportData> LoadAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var properties = await db.Properties.AsNoTracking().Include(p => p.Parameters).AsSplitQuery().ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().ToListAsync(ct);
        var contracts = await db.Contracts.AsNoTracking().ToListAsync(ct);
        var contractMeters = await db.ContractMeters.AsNoTracking().ToListAsync(ct);
        var meters = await MeterStore.IncludeAll(db.Meters.AsNoTracking()).AsSplitQuery().ToListAsync(ct);
        var bills = await db.Bills.AsNoTracking().ToListAsync(ct);
        var components = await TariffStore.IncludeAll(db.TariffComponents.AsNoTracking()).AsSplitQuery().ToListAsync(ct);
        var vatCatalog = await db.VatRates.AsNoTracking().Include(v => v.Versions).AsSplitQuery().OrderBy(v => v.Code).ToListAsync(ct);

        return new ReportData(
            properties,
            categories,
            contracts,
            contractMeters,
            [.. meters.Select(MeterStore.SortChildren)],
            bills,
            [.. components.Select(TariffStore.SortChildren)],
            vatCatalog);
    }
}
