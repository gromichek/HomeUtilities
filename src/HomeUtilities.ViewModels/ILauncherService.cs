namespace HomeUtilities.ViewModels;

/// <summary>Opens a URI in the system browser.</summary>
public interface ILauncherService
{
    Task OpenAsync(Uri uri);
}
