using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Opens a stored photo or PDF in the system's own viewer.</summary>
public sealed class FileViewer : IFileViewer
{
    public Task OpenAsync(string path, string contentType) =>
        Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path, contentType)));
}
