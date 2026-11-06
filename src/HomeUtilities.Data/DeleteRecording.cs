using HomeUtilities.Core.Abstractions;

namespace HomeUtilities.Data;

/// <summary>
/// Ambient recording of soft deletes for the Undo of a delete. <see cref="SyncEntityInterceptor"/> adds every row it turns
/// into a soft delete to the recording of the current async flow, if there is one.
/// </summary>
internal sealed class DeleteRecording : IDeleteRecording
{
    private static readonly AsyncLocal<DeleteRecording?> CurrentRecording = new();

    private readonly DeleteRecording? _outer;
    private readonly List<DeletedRow> _rows = [];
    private readonly object _lock = new();

    private DeleteRecording(DeleteRecording? outer)
    {
        _outer = outer;
    }

    /// <inheritdoc />
    public IReadOnlyList<DeletedRow> Rows
    {
        get
        {
            lock (_lock)
            {
                return [.. _rows];
            }
        }
    }

    /// <summary>Opens a recording for the current flow; nested recordings also feed the outer one.</summary>
    internal static DeleteRecording Begin()
    {
        var recording = new DeleteRecording(CurrentRecording.Value);
        CurrentRecording.Value = recording;
        return recording;
    }

    /// <summary>Notes a soft-deleted row in the recording of the current flow (nothing happens without one).</summary>
    internal static void Note(string table, Guid id)
    {
        for (var recording = CurrentRecording.Value; recording is not null; recording = recording._outer)
        {
            lock (recording._lock)
            {
                recording._rows.Add(new DeletedRow(table, id));
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Restores the outer recording (or none) for the flow that opened this one.
        if (ReferenceEquals(CurrentRecording.Value, this))
        {
            CurrentRecording.Value = _outer;
        }
    }
}
