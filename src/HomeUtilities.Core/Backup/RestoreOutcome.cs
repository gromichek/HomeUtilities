namespace HomeUtilities.Core.Backup;

/// <summary>What the startup step found and did about a staged restore (or a staged wipe).</summary>
/// <param name="Succeeded">True if the staged data replaced the current data.</param>
/// <param name="Message">Reason in plain English when it failed; shown to the owner and written to the error log.</param>
/// <param name="Exception">The failure, if any.</param>
public sealed record RestoreOutcome(bool Succeeded, string? Message, Exception? Exception)
{
    /// <summary>A restore that went through.</summary>
    public static RestoreOutcome Success { get; } = new(true, null, null);

    /// <summary>A restore that failed and left the current data in place.</summary>
    public static RestoreOutcome Failure(Exception exception) =>
        new(false, exception?.Message ?? string.Empty, exception);
}
