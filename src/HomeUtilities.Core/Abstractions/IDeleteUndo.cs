namespace HomeUtilities.Core.Abstractions;

/// <summary>One row that a delete soft-deleted: its table and id.</summary>
/// <param name="Table">The database table of the row.</param>
/// <param name="Id">The row's id.</param>
public readonly record struct DeletedRow(string Table, Guid Id);

/// <summary>Collects the rows that were soft-deleted while the recording is open (see <see cref="IDeleteUndoService.BeginRecording"/>).</summary>
public interface IDeleteRecording : IDisposable
{
    /// <summary>
    /// The rows soft-deleted inside the recording so far: the record, its child rows, its attachments and every other row
    /// the delete touched (e.g. payment allocations released from a bill). Rows that were deleted before are not listed.
    /// </summary>
    IReadOnlyList<DeletedRow> Rows { get; }
}

/// <summary>
/// "Undo" of a delete (UI_GUIDELINES §5.1): remembers exactly which rows a delete soft-deleted and brings back exactly those.
/// </summary>
public interface IDeleteUndoService
{
    /// <summary>
    /// Starts recording every soft delete made by the current async flow until the recording is disposed. Recordings do not
    /// see deletes of other flows.
    /// </summary>
    IDeleteRecording BeginRecording();

    /// <summary>Un-deletes the rows (<c>IsDeleted = false</c>, newer <c>UpdatedAt</c> so that the sync carries the restore).</summary>
    Task RestoreAsync(IReadOnlyCollection<DeletedRow> rows, CancellationToken ct = default);
}
