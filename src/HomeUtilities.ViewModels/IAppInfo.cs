namespace HomeUtilities.ViewModels;

public interface IAppInfo
{
    /// <summary>The display version, <c>yyyy.M.d</c> of the build date (e.g. <c>2026.11.5</c>).</summary>
    string Version { get; }

    /// <summary>The build number (Android <c>versionCode</c>: minutes since 2026-01-01 UTC); <c>null</c> when unknown.</summary>
    string? BuildNumber => null;

    /// <summary>True in builds compiled with <c>DEBUG</c>; the test-data tools are shown only then.</summary>
    bool IsDebugBuild { get; }
}
