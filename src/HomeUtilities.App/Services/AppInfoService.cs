namespace HomeUtilities.App.Services;

public sealed class AppInfoService : ViewModels.IAppInfo
{
    public string Version => AppInfo.Current.VersionString;
}
