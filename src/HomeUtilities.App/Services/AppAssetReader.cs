using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Reads files bundled as <c>MauiAsset</c> (the user guide).</summary>
public sealed class AppAssetReader : IAppAssetReader
{
    public async Task<Stream> OpenReadAsync(string path)
    {
        try
        {
            return await FileSystem.OpenAppPackageFileAsync(path);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Android reports a missing asset with a platform-specific exception type.
            throw new FileNotFoundException($"Asset '{path}' is not part of the app package.", path, ex);
        }
    }
}
