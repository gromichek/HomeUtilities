using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HomeUtilities.Data.Stores;

/// <summary>
/// Saves a detached aggregate graph coming from the UI by merging it into the tracked database copy:
/// scalar values are copied, new children are added and children missing from the incoming graph are removed
/// (which the interceptor turns into a soft delete).
/// </summary>
internal static class AggregateMerger
{
    /// <summary>
    /// Inserts <paramref name="incoming"/> if its id is not in the database, otherwise merges it into the stored
    /// aggregate. Saves the changes.
    /// </summary>
    /// <param name="db">A context with tracking enabled.</param>
    /// <param name="incoming">The detached aggregate root.</param>
    /// <param name="include">Adds the <c>Include</c>s of the aggregate's children (non-deleted ones only are loaded).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync<T>(
        HomeUtilitiesDbContext db,
        T incoming,
        Func<IQueryable<T>, IQueryable<T>> include,
        CancellationToken ct)
        where T : SyncEntity
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var existing = await include(db.Set<T>()).AsSplitQuery().FirstOrDefaultAsync(e => e.Id == incoming.Id, ct);
        if (existing is null)
        {
            db.Add(incoming);
        }
        else
        {
            Merge(db, existing, incoming);
        }

        await db.SaveChangesAsync(ct);
    }

    private static void Merge(HomeUtilitiesDbContext db, SyncEntity existing, SyncEntity incoming)
    {
        var entry = db.Entry(existing);

        // Bookkeeping fields belong to the stored row, not to the caller's copy.
        var createdAt = existing.CreatedAt;
        CopyValues(entry, incoming);
        existing.CreatedAt = createdAt;
        existing.IsDeleted = false;

        foreach (var navigation in entry.Collections.Where(c => c.Metadata.TargetEntityType.ClrType.IsAssignableTo(typeof(SyncEntity))))
        {
            MergeChildren(db, navigation, incoming);
        }
    }

    // PropertyValues.SetValues cannot cope with optional complex properties (PaymentDetails), so copy by hand.
    private static void CopyValues(EntityEntry entry, SyncEntity incoming)
    {
        foreach (var property in entry.Metadata.GetProperties().Where(p => !p.IsPrimaryKey() && p.PropertyInfo is not null))
        {
            entry.Property(property.Name).CurrentValue = property.PropertyInfo!.GetValue(incoming);
        }

        foreach (var complex in entry.Metadata.GetComplexProperties())
        {
            entry.ComplexProperty(complex.Name).CurrentValue = complex.PropertyInfo!.GetValue(incoming);
        }
    }

    private static void MergeChildren(HomeUtilitiesDbContext db, CollectionEntry navigation, SyncEntity incomingParent)
    {
        var incomingChildren = ReadChildren(navigation.Metadata.PropertyInfo!.GetValue(incomingParent));
        var existingChildren = ReadChildren(navigation.CurrentValue).ToDictionary(c => c.Id);
        var incomingIds = incomingChildren.Select(c => c.Id).ToHashSet();

        foreach (var child in incomingChildren)
        {
            if (existingChildren.TryGetValue(child.Id, out var stored))
            {
                Merge(db, stored, child);
            }
            else
            {
                // Add tracks the new child with everything below it as Added.
                db.Add(child);
            }
        }

        foreach (var stored in existingChildren.Values.Where(c => !incomingIds.Contains(c.Id)))
        {
            db.Remove(stored);
        }
    }

    private static List<SyncEntity> ReadChildren(object? collection) =>
        collection is null ? [] : [.. ((System.Collections.IEnumerable)collection).Cast<SyncEntity>()];
}
