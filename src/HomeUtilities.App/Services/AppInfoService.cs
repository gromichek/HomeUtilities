namespace HomeUtilities.App.Services;

public sealed class AppInfoService : ViewModels.IAppInfo
{
    public string Version => AppInfo.Current.VersionString;

#if DEBUG
    public bool IsDebugBuild => true;
#else
    public bool IsDebugBuild => false;
#endif
}
