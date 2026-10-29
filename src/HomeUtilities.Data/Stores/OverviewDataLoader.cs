using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Overview;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IOverviewDataLoader"/>: one query per table, no query per contract.</summary>
public sealed class OverviewDataLoader(IDbContextFactory<HomeUtilitiesDbContext> factory, IChargeInputLoader chargeLoader) : IOverviewDataLoader
{
    /// <summary>Readings older than this many days are not loaded: the newest reading window is at most about two months back.</summary>
    public const int ReadingHistoryDays = 100;

    /// <inheritdoc />
    public async Task<OverviewData> LoadAsync(DateOnly today, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var properties = await db.Properties.AsNoTracking()
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .ToListAsync(ct);
        var contracts = await db.Contracts.AsNoTracking().ToListAsync(ct);
        var contractMeters = await db.ContractMeters.AsNoTracking().ToListAsync(ct);
        var cutoff = today.AddDays(-ReadingHistoryDays);
        var meters = await db.Meters.AsNoTracking()
            .Include(m => m.Registers).ThenInclude(r => r.Readings.Where(x => x.Date >= cutoff))
            .AsSplitQuery()
            .ToListAsync(ct);
        var allBills = await db.Bills.AsNoTracking().ToListAsync(ct);
        var bills = allBills.Where(b => !b.IsDraft).ToList();
        var drafts = allBills.Where(b => b.IsDraft).ToList();
        var payments = await db.Payments.AsNoTracking().Include(p => p.Allocations).AsSplitQuery().ToListAsync(ct);
        var templates = await db.BillTemplates.AsNoTracking().Where(t => !t.IsDeleted).ToListAsync(ct);
        var plans = await db.EqualizedPlans.AsNoTracking().ToListAsync(ct);

        var settlements = new Dictionary<Guid, decimal>();
        var current = YearMonth.From(today);
        foreach (var contract in contracts.Where(c => c.BillingMode == BillingMode.Equalized && !c.IsArchived && c.IsActiveOn(today)))
        {
            var contractPlans = plans.Where(p => p.ContractId == contract.Id).OrderBy(p => p.ValidFrom).ToList();
            var contractBills = bills.Where(b => b.ContractId == contract.Id).ToList();
            var (from, to) = EqualizedSettlement.Range(contractPlans, contractBills, current);

            var charges = new Dictionary<YearMonth, CalculatedCharge>();
            for (var month = from; month <= to; month = month.AddMonths(1))
            {
                if (await chargeLoader.LoadAsync(contract.Id, month, ct) is { } input)
                {
                    charges[month] = ChargeCalculator.Calculate(input);
                }
            }

            settlements[contract.Id] = EqualizedSettlement.Calculate(contractPlans, contractBills, charges, from, to).Expected;
        }

        return new OverviewData(properties, contracts, contractMeters, meters, bills, payments, settlements, drafts) { BillTemplates = templates };
    }
}
