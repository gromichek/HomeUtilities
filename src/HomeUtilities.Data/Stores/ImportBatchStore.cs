using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IImportBatchStore"/>; an import and an undo are each one transaction.</summary>
public sealed class ImportBatchStore(IDbContextFactory<HomeUtilitiesDbContext> factory) : IImportBatchStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportBatch>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var batches = await db.ImportBatches.AsNoTracking().ToListAsync(ct);
        return [.. batches.OrderByDescending(b => b.ImportedAt)];
    }

    /// <inheritdoc />
    public async Task<ImportBatch?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<ImportBatch> ApplyAsync(ImportApplyRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Plan.Problems.Count > 0)
        {
            throw new InvalidOperationException("The import plan has problems and cannot be applied.");
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        var counts = request.Plan.Counts;
        var batch = new ImportBatch
        {
            ProfileId = request.ProfileId,
            FileName = Truncate(request.FileName, 200),
            SheetName = Truncate(request.SheetName, 200),
            ContractId = request.ContractId,
            ReadingCount = counts.Readings,
            BillCount = counts.Bills,
            PaymentCount = counts.Payments,
            SkippedCount = counts.SkippedRows,
            ReplacementCount = counts.Replacements,
        };
        db.ImportBatches.Add(batch);

        await AddReplacementsAsync(db, request.Plan, batch, ct);

        foreach (var row in request.Plan.Rows.Where(r => !r.HasErrors))
        {
            foreach (var planned in row.Readings)
            {
                planned.Reading.ImportBatchId = batch.Id;
                db.MeterReadings.Add(planned.Reading);
            }

            if (row.Bill is { } bill)
            {
                bill.ImportBatchId = batch.Id;
                db.Bills.Add(bill);
            }

            if (row.Payment is { } payment)
            {
                payment.ImportBatchId = batch.Id;
                db.Payments.Add(payment);
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);

        // Every record of the import got the same save timestamp (the batch's), so a later edit is any UpdatedAt above it.
        batch.ImportedAt = batch.UpdatedAt;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        db.ChangeTracker.Clear();
        return batch;
    }

    // New meters of automatic replacements with their final / initial readings; the old meters end, their links move to the new meter.
    private static async Task AddReplacementsAsync(HomeUtilitiesDbContext db, ImportPlan plan, ImportBatch batch, CancellationToken ct)
    {
        foreach (var replacement in plan.Replacements.OrderBy(r => r.Date).ThenBy(r => r.RowNumber))
        {
            replacement.NewMeter.ImportBatchId = batch.Id;
            db.Meters.Add(replacement.NewMeter);

            if (!replacement.OldMeterIsPlanned)
            {
                var old = await db.Meters.FirstAsync(m => m.Id == replacement.OldMeterId, ct);
                old.RemovedOn = replacement.Date;
            }

            foreach (var reading in replacement.OwnReadings)
            {
                reading.ImportBatchId = batch.Id;
                db.MeterReadings.Add(reading);
            }

            foreach (var closed in replacement.LinksToClose)
            {
                var link = await db.ContractMeters.FirstAsync(l => l.Id == closed.Id, ct);
                link.ValidTo = closed.ValidTo;
            }

            foreach (var created in replacement.LinksToCreate)
            {
                created.ImportBatchId = batch.Id;
                db.ContractMeters.Add(created);
            }
        }
    }

    /// <inheritdoc />
    public async Task<UndoImportResult> UndoAsync(Guid batchId, bool confirmEdited, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var batch = await db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            return new(UndoImportOutcome.NotFound, 0);
        }

        var bills = await db.Bills.Include(b => b.Lines).Where(b => b.ImportBatchId == batchId).ToListAsync(ct);
        var payments = await db.Payments.Include(p => p.Allocations).Where(p => p.ImportBatchId == batchId).ToListAsync(ct);
        var readings = await db.MeterReadings.Where(r => r.ImportBatchId == batchId).ToListAsync(ct);

        var meters = await db.Meters.Include(m => m.Registers).Where(m => m.ImportBatchId == batchId).ToListAsync(ct);
        var links = await db.ContractMeters.Where(l => l.ImportBatchId == batchId).ToListAsync(ct);
        var registerIds = meters.SelectMany(m => m.Registers).Select(r => r.Id).ToList();
        // Readings the owner added to a meter of the import afterwards go with the meter (and count as edits).
        var ownReadings = registerIds.Count == 0
            ? []
            : (await db.MeterReadings.Where(r => registerIds.Contains(r.RegisterId) && r.ImportBatchId != batchId).ToListAsync(ct));

        // Payments of the owner that were allocated to an imported bill since; they lose that allocation, the money stays.
        var billIds = bills.Select(b => b.Id).ToList();
        var batchPaymentIds = payments.Select(p => p.Id).ToHashSet();
        var foreign = billIds.Count == 0
            ? []
            : (await db.PaymentAllocations.Where(a => billIds.Contains(a.BillId)).ToListAsync(ct))
                .Where(a => !batchPaymentIds.Contains(a.PaymentId))
                .ToList();

        var edited = bills.Count(b => b.UpdatedAt > batch.ImportedAt)
            + payments.Count(p => p.UpdatedAt > batch.ImportedAt)
            + readings.Count(r => r.UpdatedAt > batch.ImportedAt)
            + meters.Count(m => m.UpdatedAt > batch.ImportedAt)
            + links.Count(l => l.UpdatedAt > batch.ImportedAt)
            + ownReadings.Count
            + foreign.Count;
        if (edited > 0 && !confirmEdited)
        {
            return new(UndoImportOutcome.NeedsConfirmation, edited);
        }

        await ReopenReplacedMetersAsync(db, meters, batchId, ct);

        db.RemoveRange(foreign);
        db.RemoveRange(ownReadings);
        db.RemoveRange(links);
        db.RemoveRange(meters);
        db.RemoveRange(payments);
        db.RemoveRange(bills);
        db.RemoveRange(readings);
        db.Remove(batch);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(UndoImportOutcome.Done, edited);
    }

    // A meter the import replaced is in use again, with the contract links it had before.
    private static async Task ReopenReplacedMetersAsync(HomeUtilitiesDbContext db, List<Meter> batchMeters, Guid batchId, CancellationToken ct)
    {
        foreach (var meter in batchMeters.Where(m => m.ReplacedMeterId is not null))
        {
            var old = await db.Meters.FirstOrDefaultAsync(m => m.Id == meter.ReplacedMeterId, ct);
            if (old is null || old.ImportBatchId == batchId)
            {
                continue;
            }

            if (old.RemovedOn == meter.InstalledOn)
            {
                old.RemovedOn = null;
            }

            var end = meter.InstalledOn.AddDays(-1);
            var closed = await db.ContractMeters
                .Where(l => l.MeterId == old.Id && l.ImportBatchId == null && l.ValidTo != null)
                .ToListAsync(ct);
            foreach (var link in closed.Where(l => l.ValidTo == (end < l.ValidFrom ? l.ValidFrom : end)))
            {
                link.ValidTo = null;
            }
        }
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
}
