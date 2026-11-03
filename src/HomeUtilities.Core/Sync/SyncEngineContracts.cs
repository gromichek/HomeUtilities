namespace HomeUtilities.Core.Sync;

/// <summary>What the sync is doing or did last (T-031).</summary>
public enum SyncStateKind
{
    /// <summary>Sync is switched off on this device.</summary>
    Off,

    /// <summary>The last run worked.</summary>
    Synced,

    /// <summary>A run is in progress.</summary>
    Syncing,

    /// <summary>No network or the provider did not answer; the changes wait in the outbox.</summary>
    WaitingForNetwork,

    /// <summary>The last run failed; see <see cref="SyncStatus.Error"/>.</summary>
    Failed,

    /// <summary>Files from a newer version of the app were found; nothing was applied.</summary>
    UpdateRequired,
}

/// <summary>Why a run was started; the automatic triggers never show error dialogs.</summary>
public enum SyncTrigger
{
    Start,
    Resume,
    Debounced,
    Leaving,
    Manual,
}

/// <summary>The status line of the Sync page.</summary>
/// <param name="Kind">What is going on.</param>
/// <param name="LastSyncAt">When the last run finished successfully (UTC), if ever.</param>
/// <param name="LastAttemptAt">When the last run started (UTC), if ever.</param>
/// <param name="Error">The error kind when <see cref="Kind"/> is <see cref="SyncStateKind.Failed"/>.</param>
/// <param name="ConflictCount">Conflicts met in the last run (unique keys, bad files).</param>
/// <param name="ClockSkew">True when the device clock differs from the server's by more than five minutes.</param>
public sealed record SyncStatus(
    SyncStateKind Kind,
    DateTimeOffset? LastSyncAt = null,
    DateTimeOffset? LastAttemptAt = null,
    SyncErrorKind? Error = null,
    int ConflictCount = 0,
    bool ClockSkew = false)
{
    public static SyncStatus Off { get; } = new(SyncStateKind.Off);
}

/// <summary>What one run did.</summary>
/// <param name="FilesUploaded">Change files uploaded.</param>
/// <param name="FilesApplied">Change files and snapshots of other devices applied.</param>
/// <param name="RowsApplied">Rows written locally from other devices.</param>
/// <param name="Conflicts">Short texts of the conflicts met (also kept for the Details list).</param>
public sealed record SyncRunResult(int FilesUploaded, int FilesApplied, int RowsApplied, IReadOnlyList<string> Conflicts)
{
    public static SyncRunResult Empty { get; } = new(0, 0, 0, []);
}

/// <summary>How the first join of a device to a non-empty cloud is done (owner decision 2026-11-02).</summary>
public enum FirstJoinChoice
{
    /// <summary>The cloud is empty: this device's data becomes the first snapshot.</summary>
    StartHere,

    /// <summary>Replace this device's data with the cloud data (recommended, preselected).</summary>
    Replace,

    /// <summary>Keep both: records entered on both devices appear twice.</summary>
    Merge,
}

/// <summary>What the first join has to decide.</summary>
/// <param name="CloudHasData">The cloud folder holds a snapshot or change files.</param>
/// <param name="LocalHasData">This device holds user data (anything besides the built-in categories and VAT rates).</param>
public sealed record FirstJoinInfo(bool CloudHasData, bool LocalHasData)
{
    /// <summary>True when the user has to choose between Replace and Merge.</summary>
    public bool NeedsChoice => CloudHasData && LocalHasData;
}

/// <summary>One line of the Details list.</summary>
/// <param name="At">When it happened (UTC).</param>
/// <param name="Text">English text for the log; the UI shows the kind with its own wording.</param>
public sealed record SyncDetail(DateTimeOffset At, string Text);

/// <summary>
/// The synchronization of the local database with the other devices through <see cref="ISyncStorage"/> (ARCHITECTURE §8).
/// One run at a time; never throws for provider or network problems — they end up in <see cref="Status"/>.
/// </summary>
public interface ISyncEngine
{
    /// <summary>The current status.</summary>
    SyncStatus Status { get; }

    /// <summary>Raised (on any thread) when <see cref="Status"/> changed.</summary>
    event EventHandler? StatusChanged;

    /// <summary>Sync is switched on for this device.</summary>
    bool IsEnabled { get; }

    /// <summary>The newest lines of what was uploaded, applied or found conflicting (newest first, at most 30).</summary>
    IReadOnlyList<SyncDetail> Details { get; }

    /// <summary>Reads the stored state (switch, last sync). Call once at start.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Looks into the cloud folder and the local data to decide how the first join goes. Throws <see cref="SyncException"/>.</summary>
    Task<FirstJoinInfo> InspectAsync(CancellationToken ct = default);

    /// <summary>
    /// Switches sync on and does the first run with the chosen first-join variant. Throws <see cref="SyncException"/> when the cloud
    /// cannot be reached (sync stays off). The caller makes the backup copy before <see cref="FirstJoinChoice.Replace"/>.
    /// </summary>
    Task EnableAsync(FirstJoinChoice choice, CancellationToken ct = default);

    /// <summary>Switches sync off; local data and the cloud files stay.</summary>
    Task DisableAsync(CancellationToken ct = default);

    /// <summary>Runs one sync now (if enabled and no other run is active). Returns false when it was skipped.</summary>
    Task<bool> SyncAsync(SyncTrigger trigger, CancellationToken ct = default);
}

/// <summary>Optional: the time the storage provider reported with its last answer (HTTP <c>Date</c> header), for the clock check.</summary>
public interface IServerClock
{
    /// <summary>Server time at <see cref="ObservedAt"/>; null until a call was made.</summary>
    DateTimeOffset? ServerTime { get; }

    /// <summary>Local time when <see cref="ServerTime"/> was observed.</summary>
    DateTimeOffset? ObservedAt { get; }
}
