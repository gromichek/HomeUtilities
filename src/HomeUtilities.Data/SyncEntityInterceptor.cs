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
        notifier?.Notify();
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        notifier?.Notify();
        return ValueTask.FromResult(result);
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

        foreach (var entry in context.ChangeTracker.Entries<SyncEntity>().ToList())
        {
            var entity = entry.Entity;
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
    }
}
