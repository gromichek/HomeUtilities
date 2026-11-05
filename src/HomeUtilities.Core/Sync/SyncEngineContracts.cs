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

    /// <summary>Another device replaced the data in the cloud (C9); nothing is synced until this device is asked to replace its data.</summary>
    CloudReplaced,
}

/// <summary>Why a run was started; the automatic triggers never show error dialogs.</summary>
public enum SyncTrigger
{
    Start,
    Resume,
    Debounced,
    Leaving,
    Manual,

    /// <summary>The network came back while the app was open (C10).</summary>
    Network,

    /// <summary>The periodic retry while the status is "waiting for the network" or "failed" (C10).</summary>
    Retry,
}

/// <summary>The status line of the Sync page.</summary>
/// <param name="Kind">What is going on.</param>
/// <param name="LastSyncAt">When the last run finished successfully (UTC), if ever.</param>
/// <param name="LastAttemptAt">When the last run started (UTC), if ever.</param>
/// <param name="Error">The error kind when <see cref="Kind"/> is <see cref="SyncStateKind.Failed"/>.</param>
/// <param name="ConflictCount">Conflicts met in the last run (unique keys, bad files).</param>
/// <param name="ClockSkew">True when the device clock differs from the server's by more than five minutes.</param>
/// <param name="Trigger">What started the run this status belongs to (for the messages, C13).</param>
/// <param name="Received">Rows received in the run (valid for <see cref="SyncStateKind.Synced"/>).</param>
/// <param name="Sent">Rows sent in the run (valid for <see cref="SyncStateKind.Synced"/>).</param>
public sealed record SyncStatus(
    SyncStateKind Kind,
    DateTimeOffset? LastSyncAt = null,
    DateTimeOffset? LastAttemptAt = null,
    SyncErrorKind? Error = null,
    int ConflictCount = 0,
    bool ClockSkew = false,
    SyncTrigger? Trigger = null,
    int Received = 0,
    int Sent = 0)
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

    /// <summary>Keep both: records entered separately on both devices appear twice; records synced before merge by the later edit.</summary>
    Merge,

    /// <summary>
    /// Continue after a pause (C14): same device id, applied sequences and outbox; no question. Only valid when
    /// <see cref="FirstJoinInfo.CanResume"/> is true.
    /// </summary>
    Resume,

    /// <summary>
    /// This device becomes the source (C9): it uploads a fresh snapshot with a new cloud generation and removes the other
    /// snapshots and change files; the other devices are asked to replace their data.
    /// </summary>
    ReplaceCloud,
}

/// <summary>What the first join has to decide.</summary>
/// <param name="CloudHasData">The cloud folder holds a snapshot or change files.</param>
/// <param name="LocalHasData">This device holds user data (anything besides the built-in categories and VAT rates).</param>
/// <param name="CanResume">This device joined this cloud with this account before and was only paused: no question (C14).</param>
/// <param name="NeedsRejoin">A restore or import replaced the local data after the join (C9): replace the local or the cloud data.</param>
/// <param name="CloudReplaced">Another device replaced the data in the cloud since this device joined (C9).</param>
public sealed record FirstJoinInfo(bool CloudHasData, bool LocalHasData, bool CanResume = false, bool NeedsRejoin = false, bool CloudReplaced = false)
{
    /// <summary>True when the user has to choose between Replace and Merge (a first join of a device with data to a cloud with data).</summary>
    public bool NeedsChoice => !CanResume && !NeedsRejoin && !CloudReplaced && CloudHasData && LocalHasData;
}

/// <summary>Change files of one device in the cloud (C12).</summary>
/// <param name="DeviceId">Full device id.</param>
/// <param name="IsThisDevice">The files of this device.</param>
/// <param name="Count">Number of change files.</param>
/// <param name="Bytes">Total size.</param>
/// <param name="Newest">Newest modification date, if the provider reports one.</param>
public sealed record CloudDeviceFiles(string DeviceId, bool IsThisDevice, int Count, long Bytes, DateTimeOffset? Newest)
{
    /// <summary>First 8 characters of the id, as shown to the user.</summary>
    public string ShortId => DeviceId.Length > 8 ? DeviceId[..8] : DeviceId;
}

/// <summary>A snapshot in the cloud (C12).</summary>
public sealed record CloudSnapshotFile(string DeviceId, DateTimeOffset At, long Bytes, int Generation);

/// <summary>The read-only overview "Files in the cloud" (C12). Nothing can be deleted from it.</summary>
/// <param name="Devices">Change files grouped by device.</param>
/// <param name="Snapshots">Snapshots, newest first.</param>
/// <param name="AttachmentCount">Attachment files.</param>
/// <param name="AttachmentBytes">Their total size.</param>
/// <param name="ForeignCount">Files this app does not know.</param>
/// <param name="ForeignBytes">Their total size.</param>
public sealed record CloudFilesReport(
    IReadOnlyList<CloudDeviceFiles> Devices,
    IReadOnlyList<CloudSnapshotFile> Snapshots,
    int AttachmentCount,
    long AttachmentBytes,
    int ForeignCount,
    long ForeignBytes)
{
    /// <summary>Size of everything in the cloud folder.</summary>
    public long TotalBytes => Devices.Sum(d => d.Bytes) + Snapshots.Sum(s => s.Bytes) + AttachmentBytes + ForeignBytes;

    /// <summary>Number of files in the cloud folder.</summary>
    public int TotalCount => Devices.Sum(d => d.Count) + Snapshots.Count + AttachmentCount + ForeignCount;
}

/// <summary>
/// Network state for the sync (C10, Part B): behind an interface so the view models stay free of MAUI.
/// </summary>
public interface ISyncConnectivity
{
    /// <summary>Any internet connection.</summary>
    bool IsConnected { get; }

    /// <summary>Wi-Fi or Ethernet (not mobile data): attachments are downloaded automatically only then.</summary>
    bool IsUnmetered { get; }

    /// <summary>Raised (on any thread) when the connection changed.</summary>
    event EventHandler? Changed;
}

/// <summary>Runs an action on the UI thread (C11). Implemented in the app; tests run the action at once or on a chosen thread.</summary>
public interface IUiDispatcher
{
    /// <summary>Runs <paramref name="action"/> on the UI thread (at once when already there).</summary>
    void Post(Action action);
}

/// <summary>Downloads the file of a synced attachment on demand (Part B).</summary>
public interface IAttachmentFileSync
{
    /// <summary>
    /// Makes sure the file of the attachment is on this device: true when it is there afterwards. False when the cloud does not
    /// have it (yet) or it is damaged. Throws <see cref="SyncException"/> when the cloud cannot be reached.
    /// </summary>
    Task<bool> EnsureDownloadedAsync(Guid attachmentId, CancellationToken ct = default);
}

/// <summary>What a line of the Details list says. The UI shows each kind with its own localized short wording (R-43).</summary>
public enum SyncDetailKind
{
    /// <summary>This device started the sync with a snapshot.</summary>
    StartedWithSnapshot,

    /// <summary>This device replaced the data in the cloud.</summary>
    ReplacedCloud,

    /// <summary>Sync was resumed.</summary>
    Resumed,

    /// <summary>The data in the cloud was replaced on another device.</summary>
    CloudReplaced,

    /// <summary>The clock of the device differs from the server's.</summary>
    ClockSkew,

    /// <summary>A snapshot of another device was applied (<see cref="SyncDetail.Count"/> records).</summary>
    AppliedSnapshot,

    /// <summary>Changes of another device were applied (<see cref="SyncDetail.Count"/> records).</summary>
    AppliedChanges,

    /// <summary>Changes of this device were uploaded (<see cref="SyncDetail.Count"/> records).</summary>
    UploadedChanges,

    /// <summary>A snapshot was written to the cloud.</summary>
    WroteSnapshot,

    /// <summary>Old change files covered by a snapshot were removed from the cloud.</summary>
    DeletedCovered,

    /// <summary>A conflict was met while merging.</summary>
    Conflict,

    /// <summary>The file of an attachment was uploaded.</summary>
    AttachmentUploaded,

    /// <summary>The file of an attachment was downloaded.</summary>
    AttachmentDownloaded,

    /// <summary>A downloaded attachment file was damaged (checksum); another attempt follows.</summary>
    AttachmentChecksum,

    /// <summary>An old deleted attachment was removed from the cloud.</summary>
    AttachmentDeleted,
}

/// <summary>One line of the Details list: the kind and a number, no file names or ids (those go to the error log only).</summary>
/// <param name="At">When it happened (UTC).</param>
/// <param name="Kind">What happened; the UI maps it to a localized short text.</param>
/// <param name="Count">Number of records for the kinds that have one; else 0.</param>
public sealed record SyncDetail(DateTimeOffset At, SyncDetailKind Kind, int Count = 0);

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

    /// <summary>True after a restore or import (C9): sync is off and needs the re-join question.</summary>
    bool NeedsRejoin { get; }

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

    /// <summary>Pauses sync (C14): device id, applied sequences and the outbox are kept; local data and the cloud files stay.</summary>
    Task DisableAsync(CancellationToken ct = default);

    /// <summary>Forgets that this device joined the cloud (Disconnect): switching on again is a first join.</summary>
    Task ForgetJoinAsync(CancellationToken ct = default);

    /// <summary>Lists the files in the cloud folder, grouped (C12). Throws <see cref="SyncException"/>.</summary>
    Task<CloudFilesReport> ListCloudFilesAsync(CancellationToken ct = default);

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
