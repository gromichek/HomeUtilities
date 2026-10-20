using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Opens links in the system browser; the browser's own password manager handles logins.</summary>
public sealed class LauncherService : ILauncherService
{
    public Task OpenAsync(Uri uri) => Launcher.Default.OpenAsync(uri);
}
