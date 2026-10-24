namespace HomeUtilities.ViewModels;

public interface IAppInfo
{
    string Version { get; }

    /// <summary>True in builds compiled with <c>DEBUG</c>; the test-data tools are shown only then.</summary>
    bool IsDebugBuild { get; }
}
