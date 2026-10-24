namespace HomeUtilities.ViewModels;

/// <summary>Opens a stored file (photo, PDF) in the system viewer.</summary>
public interface IFileViewer
{
    Task OpenAsync(string path, string contentType);
}
