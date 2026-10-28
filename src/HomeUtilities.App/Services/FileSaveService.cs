using CommunityToolkit.Maui.Storage;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Both platforms: the system save dialog (Android: choose a folder such as Downloads or Drive; Windows: the file
/// dialog). Fallbacks when the dialog fails or is cancelled with an error: Android — the share sheet;
/// Windows — <c>Documents\HomeUtilities\&lt;folder&gt;</c>. Used for backups and report exports.
/// </summary>
public sealed class FileSaveService : IFileSaveService
{
    private const string CacheFolderName = "file-share";

    public async Task<FileSaveResult> SaveAsync(string fileName, string contentType, string folder, Func<Stream, Task> write)
    {
        var cacheFolder = Path.Combine(FileSystem.CacheDirectory, CacheFolderName);
        ClearFolder(cacheFolder);
        Directory.CreateDirectory(cacheFolder);
        var cachePath = Path.Combine(cacheFolder, fileName);
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
                return new FileSaveResult(true, result.FilePath);
            }

            if (result.Exception is null)
            {
                return new FileSaveResult(false, null);
            }

            System.Diagnostics.Debug.WriteLine($"Save dialog failed: {result.Exception}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save dialog failed: {ex}");
        }

#if WINDOWS
        var documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HomeUtilities", folder);
        Directory.CreateDirectory(documents);
        var target = Path.Combine(documents, fileName);
        File.Copy(cachePath, target, overwrite: true);
        return new FileSaveResult(true, target);
#else
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File = new ShareFile(cachePath, contentType),
        });
        return new FileSaveResult(true, null);
#endif
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
