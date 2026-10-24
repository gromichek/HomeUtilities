using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class AppLifecycle : IAppLifecycle
{
    public void Quit() => Application.Current?.Quit();
}
