using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IMeterStore"/>.</summary>
public sealed class MeterStore(IDbContextFactory<HomeUtilitiesDbContext> factory, IClock? clock = null) : StoreBase<Meter>(factory), IMeterStore
{
    private readonly IClock _clock = clock ?? new SystemClock();

    /// <summary>Loads the registers and their readings of a meter query.</summary>
    internal static IQueryable<Meter> IncludeAll(IQueryable<Meter> query) =>
        query.Include(m => m.Registers).ThenInclude(r => r.Readings);

    /// <summary>Sorts registers and readings of a loaded meter.</summary>
    internal static Meter SortChildren(Meter meter)
    {
        meter.Registers = [.. meter.Registers.OrderBy(r => r.Kind).ThenBy(r => r.Direction)];
        foreach (var register in meter.Registers)
        {
            register.Readings = [.. register.Readings.OrderBy(r => r.Date)];
        }

        return meter;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Meter>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        ListAsync(q => q.Where(m => m.PropertyId == propertyId), ct);

    /// <inheritdoc />
    public async Task SaveReplacementAsync(
        Meter oldMeter,
        Meter newMeter,
        IReadOnlyList<ContractMeter> linksToClose,
        IReadOnlyList<ContractMeter> linksToCreate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(oldMeter);
        ArgumentNullException.ThrowIfNull(newMeter);
        ArgumentNullException.ThrowIfNull(linksToClose);
        ArgumentNullException.ThrowIfNull(linksToCreate);

        await using var db = await Factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AggregateMerger.SaveAsync(db, oldMeter, Include, ct);
        await AggregateMerger.SaveAsync(db, newMeter, Include, ct);
        foreach (var link in linksToClose.Concat(linksToCreate))
        {
            await AggregateMerger.SaveAsync(db, link, query => query, ct);
        }

        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Soft-deletes the meter and ends its open contract links today (a link that starts in the future ends on its first
    /// day), all in one transaction.
    /// </summary>
    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var today = _clock.Today;
        var links = await db.ContractMeters.Where(l => l.MeterId == id).ToListAsync(ct);
        foreach (var link in links.Where(l => l.ValidTo is null || l.ValidTo > today))
        {
            link.ValidTo = link.ValidFrom > today ? link.ValidFrom : today;
        }

        // Saved and untracked first: a tracked link would be swept along (and rejected) by the meter's removal.
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var existing = await Include(db.Set<Meter>()).AsSplitQuery().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (existing is not null)
        {
            db.Remove(existing);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddReadingsAsync(IEnumerable<(Guid RegisterId, MeterReading Reading)> readings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var list = readings.ToList();
        await using var db = await Factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var registerIds = list.Select(r => r.RegisterId).Distinct().ToList();
        var found = await db.MeterRegisters.Where(r => registerIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
        foreach (var (registerId, reading) in list)
        {
            if (!found.Contains(registerId))
            {
                throw new InvalidOperationException($"Register {registerId} does not exist.");
            }

            reading.RegisterId = registerId;
            db.MeterReadings.Add(reading);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateReadingsAsync(IEnumerable<MeterReading> readings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var list = readings.ToList();
        await using var db = await Factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var ids = list.Select(r => r.Id).ToList();
        var stored = await db.MeterReadings.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        foreach (var incoming in list)
        {
            if (!stored.TryGetValue(incoming.Id, out var reading))
            {
                throw new InvalidOperationException($"Reading {incoming.Id} does not exist.");
            }

            reading.Date = incoming.Date;
            reading.Value = incoming.Value;
            reading.Kind = incoming.Kind;
            reading.SubmittedOn = incoming.SubmittedOn;
            reading.PhotoAttachmentId = incoming.PhotoAttachmentId;
            reading.Note = incoming.Note;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteReadingAsync(Guid readingId, CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var reading = await db.MeterReadings.FirstOrDefaultAsync(r => r.Id == readingId, ct);
        if (reading is null)
        {
            return;
        }

        db.Remove(reading);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    protected override IQueryable<Meter> Include(IQueryable<Meter> query) => IncludeAll(query);

    /// <inheritdoc />
    protected override IQueryable<Meter> Order(IQueryable<Meter> query) =>
        query.OrderBy(m => m.InstalledOn).ThenBy(m => m.Id);

    /// <inheritdoc />
    protected override Meter Normalize(Meter aggregate) => SortChildren(aggregate);
}
