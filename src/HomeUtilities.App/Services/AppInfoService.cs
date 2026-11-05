using System.Reflection;

namespace HomeUtilities.App.Services;

public sealed class AppInfoService : ViewModels.IAppInfo
{
    // Both values are written into the assembly by the csproj (AssemblyMetadata), so Windows (unpackaged) and Android show
    // the same text; AppInfo is only the fallback.
    private static readonly string? DisplayVersion = ReadMetadata("DisplayVersion");

    private static readonly string? Build = ReadMetadata("BuildNumber");

    public string Version => DisplayVersion ?? AppInfo.Current.VersionString;

    public string? BuildNumber => Build ?? AppInfo.Current.BuildString;

#if DEBUG
    public bool IsDebugBuild => true;
#else
    public bool IsDebugBuild => false;
#endif

    private static string? ReadMetadata(string key) =>
        typeof(AppInfoService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value ? value : null;
}
