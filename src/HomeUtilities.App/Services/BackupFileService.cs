using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Backups: saved through the shared <see cref="IFileSaveService"/> (folder "Backups"), picked with the file picker.</summary>
public sealed class BackupFileService(IFileSaveService files) : IBackupFileService
{
    public Task<FileSaveResult> SaveAsync(string fileName, Func<Stream, Task> write) =>
        files.SaveAsync(fileName, "application/zip", "Backups", write);

    public async Task<Stream?> PickAsync()
    {
        var zip = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["application/zip", "application/x-zip-compressed", "application/octet-stream"],
            [DevicePlatform.WinUI] = [".zip"],
            [DevicePlatform.iOS] = ["public.zip-archive"],
            [DevicePlatform.MacCatalyst] = ["public.zip-archive"],
        });
        var picked = await FilePicker.Default.PickAsync(new PickOptions { FileTypes = zip });
        if (picked is null)
        {
            return null;
        }

        // The picked stream may not be seekable (content URI on Android); the backup check needs a seekable one.
        var copy = new FileStream(
            Path.Combine(FileSystem.CacheDirectory, "picked-" + Guid.NewGuid().ToString("N") + ".zip"),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose);
        try
        {
            await using (var source = await picked.OpenReadAsync())
            {
                await source.CopyToAsync(copy);
            }

            copy.Position = 0;
            return copy;
        }
        catch
        {
            await copy.DisposeAsync();
            throw;
        }
    }
}
