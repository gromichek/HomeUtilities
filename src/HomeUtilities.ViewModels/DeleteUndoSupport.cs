using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core.Abstractions;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The "deleted · Undo" snackbar of a delete (UI_GUIDELINES §5.1). A delete is recorded with <see cref="Begin"/>; the snackbar
/// then offers Undo, which restores exactly the recorded rows (the record, its child rows and attachments, and every row
/// the delete touched, e.g. released payment allocations).
/// </summary>
public sealed class DeleteUndoSupport(IDeleteUndoService service, ISnackbarService snackbar, ILocalizer localizer)
{
    /// <summary>How long the Undo snackbar stays (UI_GUIDELINES §5.1).</summary>
    public static TimeSpan Duration { get; } = TimeSpan.FromSeconds(6);

    /// <summary>Starts recording the rows soft-deleted by the current flow; dispose the recording when the delete is done.</summary>
    public IDeleteRecording Begin() => service.BeginRecording();

    /// <summary>
    /// Shows "«name» deleted · Undo" and completes when the snackbar is gone. A tap on Undo restores
    /// <paramref name="rows"/> and tells the visible lists to reload. Returns true if the delete was undone.
    /// </summary>
    public async Task<bool> OfferUndoAsync(string name, IReadOnlyCollection<DeletedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return false;
        }

        var undo = await snackbar.ShowAsync(localizer.Get("DeletedUndoMessage", name), localizer["Undo"], Duration);
        if (!undo)
        {
            return false;
        }

        await service.RestoreAsync(rows);
        WeakReferenceMessenger.Default.Send(new RecordsRestoredMessage());
        return true;
    }
}
