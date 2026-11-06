using System.Reflection;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>EF Core implementation of <see cref="IDeleteUndoService"/>.</summary>
public sealed class DeleteUndoService(IDbContextFactory<HomeUtilitiesDbContext> factory) : IDeleteUndoService
{
    private static readonly MethodInfo RestoreTable =
        typeof(DeleteUndoService).GetMethod(nameof(RestoreTableAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <inheritdoc />
    public IDeleteRecording BeginRecording() => DeleteRecording.Begin();

    /// <inheritdoc />
    public async Task RestoreAsync(IReadOnlyCollection<DeletedRow> rows, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var table in rows.GroupBy(r => r.Table))
        {
            var entityType = db.Model.GetEntityTypes().FirstOrDefault(t => t.GetTableName() == table.Key && t.ClrType.IsAssignableTo(typeof(SyncEntity)));
            if (entityType is null)
            {
                continue;
            }

            var ids = table.Select(r => r.Id).Distinct().ToList();
            var task = (Task)RestoreTable.MakeGenericMethod(entityType.ClrType).Invoke(null, [db, ids, ct])!;
            await task;
        }

        // One save: the interceptor stamps UpdatedAt, fills the sync outbox and tells the screens to reload.
        await db.SaveChangesAsync(ct);
    }

    private static async Task RestoreTableAsync<T>(HomeUtilitiesDbContext db, List<Guid> ids, CancellationToken ct)
        where T : SyncEntity
    {
        var deleted = await db.Set<T>().IgnoreQueryFilters().Where(e => ids.Contains(e.Id) && e.IsDeleted).ToListAsync(ct);
        foreach (var row in deleted)
        {
            row.IsDeleted = false;
        }
    }
}
