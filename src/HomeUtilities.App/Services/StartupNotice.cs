namespace HomeUtilities.App.Services;

/// <summary>Something the startup step wants the owner to see once, e.g. that a staged restore failed.</summary>
public sealed class StartupNotice
{
    /// <summary>Plain-English reason of a failed restore; null if there is nothing to show.</summary>
    public string? RestoreFailure { get; init; }
}
