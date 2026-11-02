using HomeUtilities.Core.Sync;

namespace HomeUtilities.App.Services;

/// <summary>Opens the sign-in page in the system browser (never an embedded web view: Google does not allow it).</summary>
public sealed class UrlOpener : IUrlOpener
{
    public Task<bool> OpenAsync(Uri url) => Launcher.Default.OpenAsync(url);
}
