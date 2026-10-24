using CommunityToolkit.Maui.Storage;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Both platforms: the system save dialog (Android: choose a folder such as Downloads or Drive; Windows: the file
/// dialog). Fallbacks when the dialog fails or is cancelled with an error: Android — the share sheet;
/// Windows — <c>Documents\HomeUtilities\Backups</c>.
/// </summary>
public sealed class BackupFileService : IBackupFileService
{
    private const string CacheFolderName = "backup-share";

    public async Task<BackupSaveResult> SaveAsync(string fileName, Func<Stream, Task> write)
    {
        var folder = Path.Combine(FileSystem.CacheDirectory, CacheFolderName);
        ClearFolder(folder);
        Directory.CreateDirectory(folder);
        var cachePath = Path.Combine(folder, fileName);
        await using (var file = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await write(file);
        }

        try
        {
            await using var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
            if (result.IsSuccessful)
            {
                return new BackupSaveResult(true, result.FilePath);
            }

            if (result.Exception is null)
            {
                return new BackupSaveResult(false, null);
            }

            System.Diagnostics.Debug.WriteLine($"Save dialog failed: {result.Exception}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save dialog failed: {ex}");
        }

#if WINDOWS
        var documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HomeUtilities", "Backups");
        Directory.CreateDirectory(documents);
        var target = Path.Combine(documents, fileName);
        File.Copy(cachePath, target, overwrite: true);
        return new BackupSaveResult(true, target);
#else
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File = new ShareFile(cachePath, "application/zip"),
        });
        return new BackupSaveResult(true, null);
#endif
    }

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

    private static void ClearFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clearing {folder} failed: {ex}");
        }
    }
}
