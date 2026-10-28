using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IBillTemplateStore"/>.</summary>
public sealed class BillTemplateStore(IDbContextFactory<HomeUtilitiesDbContext> factory, IClock clock)
    : StoreBase<BillTemplate>(factory), IBillTemplateStore
{
    private readonly SemaphoreSlim _generating = new(1, 1);

    /// <inheritdoc />
    public async Task<int> GenerateDraftsAsync(DateOnly today, CancellationToken ct = default)
    {
        // Overview and Bills may both ask on start; one run at a time keeps the check against existing bills reliable.
        await _generating.WaitAsync(ct);
        try
        {
            await using var db = await Factory.CreateDbContextAsync(ct);
            var templates = (await db.BillTemplates.Where(t => t.IsActive).ToListAsync(ct))
                .Where(t => t.Schedule.Kind != ScheduleKind.Once)
                .ToList();

            var created = 0;
            foreach (var template in templates)
            {
                // Deleted drafts count too, so a draft the owner deleted does not come back.
                var existing = await db.Bills.IgnoreQueryFilters()
                    .Where(b => b.TemplateId == template.Id)
                    .Select(b => b.Period)
                    .ToListAsync(ct);
                var due = BillTemplateScheduler.DuePeriods(template, today, existing);
                if (due.Count == 0)
                {
                    continue;
                }

                var contract = template.ContractId is { } contractId
                    ? await db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, ct)
                    : null;
                foreach (var period in due)
                {
                    db.Bills.Add(DraftFactory.Create(template, period, clock, contract));
                    created++;
                }

                template.LastGeneratedPeriod = due[^1];
            }

            if (created > 0 || db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync(ct);
            }

            return created;
        }
        finally
        {
            _generating.Release();
        }
    }

    /// <inheritdoc />
    protected override IQueryable<BillTemplate> Include(IQueryable<BillTemplate> query) => query;

    /// <inheritdoc />
    protected override IQueryable<BillTemplate> Order(IQueryable<BillTemplate> query) =>
        query.OrderBy(t => t.Name).ThenBy(t => t.CreatedAt);
}
