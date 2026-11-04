using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Finds the local file of an attachment for viewing. A synced attachment whose file has not arrived yet (mobile data, or the
/// other device has not uploaded it) shows "Not downloaded yet" with a Download button (T-032 part B).
/// </summary>
internal static class AttachmentFileResolver
{
    /// <summary>The path of the file, or null after the user was told why it cannot be opened.</summary>
    internal static async Task<string?> ResolveAsync(
        IAttachmentStore store, IAttachmentFileSync? sync, IDialogService dialogs, ILocalizer localizer, Guid attachmentId, string missingKey)
    {
        if (await store.GetFilePathAsync(attachmentId) is { } path)
        {
            return path;
        }

        if (sync is null || await store.GetAsync(attachmentId) is null)
        {
            await dialogs.AlertAsync(localizer["Dialog_Error_Title"], localizer[missingKey]);
            return null;
        }

        if (!await dialogs.ConfirmAsync(
                localizer["AttachmentNotDownloadedTitle"], localizer["AttachmentNotDownloaded"], localizer["AttachmentDownload"], localizer["Cancel"]))
        {
            return null;
        }

        try
        {
            if (await sync.EnsureDownloadedAsync(attachmentId) && await store.GetFilePathAsync(attachmentId) is { } downloaded)
            {
                return downloaded;
            }

            await dialogs.AlertAsync(localizer["Dialog_Error_Title"], localizer["AttachmentDownloadFailed"]);
        }
        catch (SyncException ex)
        {
            await dialogs.AlertAsync(localizer["Dialog_Error_Title"], localizer[$"SyncError_{ex.Kind}"]);
        }

        return null;
    }
}
