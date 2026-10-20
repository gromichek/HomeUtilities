using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class AppPaths : IAppPaths
{
    public string DatabasePath { get; } = Path.Combine(FileSystem.AppDataDirectory, "homeutilities.db");
}
