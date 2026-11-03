using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HomeUtilities.Data;

/// <summary>
/// Stamps <see cref="SyncEntity.CreatedAt"/> / <see cref="SyncEntity.UpdatedAt"/> on save and turns every
/// delete into a soft delete. After a successful save it tells the optional <see cref="IDataChangeNotifier"/>.
/// </summary>
public sealed class SyncEntityInterceptor(IClock clock, IDataChangeNotifier? notifier = null) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfChanged(result);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        NotifyIfChanged(result);
        return ValueTask.FromResult(result);
    }

    // A save that wrote no rows is not a data change (T-028i): it must not make every section reload.
    private void NotifyIfChanged(int rowsWritten)
    {
        if (rowsWritten > 0)
        {
            notifier?.Notify();
        }
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var preserve = context is HomeUtilitiesDbContext { PreserveTimestamps: true };
        var now = clock.UtcNow;

        // Finds children removed from collections and changed JSON columns before the states are read.
        context.ChangeTracker.DetectChanges();

        var changed = new List<(string Table, Guid Id)>();
        foreach (var entry in context.ChangeTracker.Entries<SyncEntity>().ToList())
        {
            var entity = entry.Entity;
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Metadata.GetTableName() is { } tableName)
            {
                changed.Add((tableName, entity.Id));
            }

            switch (entry.State)
            {
                case EntityState.Added when !preserve:
                    entity.UpdatedAt = now;
                    if (entity.CreatedAt == default)
                    {
                        entity.CreatedAt = now;
                    }

                    break;

                case EntityState.Modified when !preserve:
                    entity.UpdatedAt = now;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    entity.IsDeleted = true;
                    if (!preserve)
                    {
                        entity.UpdatedAt = now;
                    }

                    entry.State = EntityState.Modified;
                    break;
            }
        }

        // T-031: the changed rows go to the sync outbox. Written before the save: a failed save leaves a harmless extra upload,
        // a crash after the save cannot lose a change.
        if (changed.Count > 0 && !(context is HomeUtilitiesDbContext { SuppressOutbox: true }))
        {
            SyncOutbox.Record(context, changed);
        }
    }
}
