using System.Globalization;
using System.Text;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Sync;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Sync;

/// <summary>Tunable numbers of the sync; the defaults are the ones of the task (T-031).</summary>
/// <param name="AppVersion">Written to the files.</param>
/// <param name="MaxRowsPerFile">Rows per change file.</param>
/// <param name="SnapshotEvery">A snapshot is written after this many own change files.</param>
/// <param name="KeepCoveredFiles">Change files covered by a snapshot older than this are deleted by their device.</param>
/// <param name="AttachmentsFolder">Folder of the local attachment files; null switches the attachment file sync off.</param>
public sealed record SyncOptions(
    string AppVersion = "0",
    int MaxRowsPerFile = 500,
    int SnapshotEvery = 200,
    TimeSpan? KeepCoveredFiles = null,
    string? AttachmentsFolder = null)
{
    internal TimeSpan Retention => KeepCoveredFiles ?? TimeSpan.FromDays(30);
}

/// <summary>
/// The sync engine (ARCHITECTURE §8, T-031): uploads the outbox as change files, applies the files of the other devices with
/// "newest <c>UpdatedAt</c> wins" per row, writes snapshots and handles the first join. All work goes through raw SQL in the
/// row shape of <see cref="DataExchangeService"/>; the storage is only <see cref="ISyncStorage"/>.
/// </summary>
public sealed partial class SyncEngine : ISyncEngine, IAttachmentFileSync
{
    private const int MaxDetails = 30;
    private static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<HomeUtilitiesDbContext> _factory;
    private readonly ISyncStorage _storage;
    private readonly IClock _clock;
    private readonly IDataChangeNotifier? _notifier;
    private readonly Action<string, Exception>? _log;
    private readonly SyncOptions _options;
    private readonly SyncStateStore _state;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<SyncDetail> _details = [];
    private SyncStatus _status = SyncStatus.Off;
    private readonly Func<string?>? _accountEmail;
    private readonly ISyncConnectivity? _connectivity;
    private readonly Func<bool>? _downloadOnMobile;
    private string _deviceId = string.Empty;
    private int _generation;

    public SyncEngine(
        IDbContextFactory<HomeUtilitiesDbContext> factory,
        ISyncStorage storage,
        IClock clock,
        IDataChangeNotifier? notifier = null,
        SyncOptions? options = null,
        Action<string, Exception>? log = null,
        Func<string?>? accountEmail = null,
        ISyncConnectivity? connectivity = null,
        Func<bool>? downloadOnMobile = null)
    {
        _accountEmail = accountEmail;
        _connectivity = connectivity;
        _downloadOnMobile = downloadOnMobile;
        _factory = factory;
        _storage = storage;
        _clock = clock;
        _notifier = notifier;
        _options = options ?? new SyncOptions();
        _log = log;
        _state = new SyncStateStore(factory);
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public SyncStatus Status => _status;

    /// <inheritdoc />
    public bool IsEnabled { get; private set; }

    /// <inheritdoc />
    public bool NeedsRejoin { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<SyncDetail> Details
    {
        get
        {
            lock (_details)
            {
                return [.. _details];
            }
        }
    }

    /// <summary>The id of this installation in the cloud files (empty until sync was switched on).</summary>
    public string DeviceId => _deviceId;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _deviceId = await _state.GetAsync(SyncStateStore.DeviceIdKey, ct) ?? string.Empty;
        NeedsRejoin = _deviceId.Length > 0 && await _state.GetAsync(SyncStateStore.NeedsRejoinKey, ct) == "1";
        _generation = (int)await _state.GetLongAsync(SyncStateStore.CloudGenerationKey, ct);
        IsEnabled = _deviceId.Length > 0 && !NeedsRejoin && await _state.GetAsync(SyncStateStore.EnabledKey, ct) == "1";
        var last = DateTimeOffset.TryParse(await _state.GetAsync(SyncStateStore.LastSyncKey, ct), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : (DateTimeOffset?)null;
        SetStatus(IsEnabled ? new SyncStatus(SyncStateKind.Synced, last) : SyncStatus.Off);
    }

    /// <inheritdoc />
    public async Task<FirstJoinInfo> InspectAsync(CancellationToken ct = default)
    {
        var files = await _storage.ListAsync(string.Empty, ct);
        var joined = _deviceId.Length > 0 && !NeedsRejoin
            && string.Equals(await _state.GetAsync(SyncStateStore.JoinedAccountKey, ct), CurrentAccount(), StringComparison.OrdinalIgnoreCase)
            && await _state.GetLongAsync(SyncStateStore.BootstrappedKey, ct) == 1;
        var replaced = joined && SyncFormat.MaxGeneration(files) > _generation;
        return new FirstJoinInfo(
            CloudHasData(files, ownDevice: null),
            await _state.HasUserDataAsync(ct),
            CanResume: joined && !replaced,
            NeedsRejoin: NeedsRejoin,
            CloudReplaced: replaced);
    }

    private string CurrentAccount() => _accountEmail?.Invoke() ?? string.Empty;

    /// <inheritdoc />
    public async Task EnableAsync(FirstJoinChoice choice, CancellationToken ct = default)
    {
        if (choice == FirstJoinChoice.Resume)
        {
            await ResumeCoreAsync(ct);
            await SyncAsync(SyncTrigger.Manual, ct);
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            var files = await _storage.ListAsync(string.Empty, ct);
            var cloudHasData = CloudHasData(files, ownDevice: null);
            if (choice == FirstJoinChoice.StartHere && cloudHasData)
            {
                choice = FirstJoinChoice.Merge;
            }

            var cloudGeneration = SyncFormat.MaxGeneration(files);
            if (choice == FirstJoinChoice.ReplaceCloud)
            {
                cloudGeneration++;
            }

            // A fresh id per join: sequences of an earlier join in the same cloud can never collide.
            var device = Guid.NewGuid().ToString("N");
            await _state.SetAsync(SyncStateStore.EnabledKey, "0", ct);
            await _state.SetAsync(SyncStateStore.DeviceIdKey, device, ct);
            await _state.SetLongAsync(SyncStateStore.LocalSequenceKey, 0, ct);
            await _state.SetLongAsync(SyncStateStore.LastSnapshotSequenceKey, 0, ct);
            await _state.SetAsync(SyncStateStore.OwnSnapshotsKey, string.Empty, ct);
            await _state.SetLongAsync(SyncStateStore.BootstrappedKey, 0, ct);
            await _state.ClearAppliedAsync(ct);
            await _state.SetLongAsync(SyncStateStore.CloudGenerationKey, cloudGeneration, ct);
            await _state.SetAsync(SyncStateStore.JoinedAccountKey, CurrentAccount(), ct);
            await _state.SetAsync(SyncStateStore.NeedsRejoinKey, "0", ct);
            _deviceId = device;
            _generation = cloudGeneration;
            NeedsRejoin = false;

            switch (choice)
            {
                case FirstJoinChoice.StartHere:
                    await _state.ClearOutboxAsync(ct);
                    await WriteSnapshotAsync(ct);
                    await _state.SetLongAsync(SyncStateStore.BootstrappedKey, 1, ct);
                    AddDetail("This device started the sync with a snapshot.");
                    break;

                case FirstJoinChoice.ReplaceCloud:
                    await _state.ClearOutboxAsync(ct);
                    await WriteSnapshotAsync(ct);
                    await _state.SetLongAsync(SyncStateStore.BootstrappedKey, 1, ct);
                    // The new snapshot is safe in the cloud: now the old generation can go (other devices are asked on their next sync).
                    foreach (var file in files.Where(f => SyncFormat.ParseSnapshot(f.Path) is not null || SyncFormat.ParseChange(f.Path) is not null))
                    {
                        await _storage.DeleteAsync(file.Path, ct);
                    }

                    AddDetail($"This device replaced the data in the cloud (generation {cloudGeneration}).");
                    break;

                case FirstJoinChoice.Replace:
                    await _state.ClearOutboxAsync(ct);
                    await ApplyRemoteAsync(files, replace: true, ct);
                    break;

                default:
                    await _state.EnqueueAllAsync(ct);
                    await ApplyRemoteAsync(files, replace: false, ct);
                    break;
            }

            await _state.SetAsync(SyncStateStore.EnabledKey, "1", ct);
            IsEnabled = true;
            if (choice is not (FirstJoinChoice.StartHere or FirstJoinChoice.ReplaceCloud))
            {
                _notifier?.Notify();
            }
        }
        finally
        {
            _gate.Release();
        }

        await SyncAsync(SyncTrigger.Manual, ct);
    }

    private async Task ResumeCoreAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_deviceId.Length == 0 || NeedsRejoin)
            {
                throw new InvalidOperationException("This device never joined the cloud; there is nothing to resume.");
            }

            await _state.SetAsync(SyncStateStore.EnabledKey, "1", ct);
            IsEnabled = true;
            SetStatus(new SyncStatus(SyncStateKind.Synced, await LastSyncAsync(ct)));
            AddDetail("Sync resumed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisableAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // A pause (C14): the device id, the applied sequences and the outbox stay.
            await _state.SetAsync(SyncStateStore.EnabledKey, "0", ct);
            IsEnabled = false;
            SetStatus(SyncStatus.Off);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ForgetJoinAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await _state.SetAsync(SyncStateStore.EnabledKey, "0", ct);
            await _state.SetAsync(SyncStateStore.DeviceIdKey, string.Empty, ct);
            await _state.SetAsync(SyncStateStore.JoinedAccountKey, string.Empty, ct);
            await _state.SetAsync(SyncStateStore.NeedsRejoinKey, "0", ct);
            _deviceId = string.Empty;
            IsEnabled = false;
            NeedsRejoin = false;
            SetStatus(SyncStatus.Off);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DateTimeOffset?> LastSyncAsync(CancellationToken ct) =>
        DateTimeOffset.TryParse(await _state.GetAsync(SyncStateStore.LastSyncKey, ct), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : null;

    /// <inheritdoc />
    public async Task<bool> SyncAsync(SyncTrigger trigger, CancellationToken ct = default)
    {
        if (!IsEnabled || !await _gate.WaitAsync(0, ct))
        {
            return false;
        }

        var attempt = _clock.UtcNow;
        var previous = _status;
        try
        {
            SetStatus(previous with { Kind = SyncStateKind.Syncing, LastAttemptAt = attempt, Trigger = trigger });
            var files = await _storage.ListAsync(string.Empty, ct);
            if (SyncFormat.MaxGeneration(files) > _generation)
            {
                // Another device replaced the data in the cloud: uploading or applying anything would mix two generations.
                AddDetail("The data in the cloud was replaced on another device.");
                SetStatus(previous with { Kind = SyncStateKind.CloudReplaced, LastAttemptAt = attempt, Trigger = trigger });
                return true;
            }

            var conflicts = new List<string>();
            var applied = await ApplyRemoteAsync(files, replace: false, ct);
            conflicts.AddRange(applied.Conflicts);
            var (uploaded, rowsSent) = await UploadAsync(files, ct);
            await SyncAttachmentFilesAsync(files, ct);
            await WriteSnapshotIfDueAsync(ct);
            await CleanUpAsync(files, ct);

            await _state.SetAsync(SyncStateStore.LastSyncKey, attempt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), ct);
            var skew = ClockSkewed(attempt);
            if (skew)
            {
                AddDetail("The clock of this device differs from the server's by more than five minutes.");
            }

            SetStatus(new SyncStatus(SyncStateKind.Synced, attempt, attempt, null, conflicts.Count, skew, trigger, applied.RowsApplied, rowsSent));
            if (applied.RowsApplied > 0)
            {
                _notifier?.Notify();
            }

            return true;
        }
        catch (UpdateRequiredException)
        {
            SetStatus(previous with { Kind = SyncStateKind.UpdateRequired, LastAttemptAt = attempt, Trigger = trigger });
            return true;
        }
        catch (SyncException ex) when (ex.Kind is SyncErrorKind.NoNetwork or SyncErrorKind.Timeout)
        {
            SetStatus(previous with { Kind = SyncStateKind.WaitingForNetwork, LastAttemptAt = attempt, Error = ex.Kind, Trigger = trigger });
            return true;
        }
        catch (SyncException ex)
        {
            _log?.Invoke("Sync", ex);
            SetStatus(previous with { Kind = SyncStateKind.Failed, LastAttemptAt = attempt, Error = ex.Kind, Trigger = trigger });
            return true;
        }
        catch (OperationCanceledException)
        {
            SetStatus(previous);
            throw;
        }
        catch (Exception ex)
        {
            _log?.Invoke("Sync", ex);
            SetStatus(previous with { Kind = SyncStateKind.Failed, LastAttemptAt = attempt, Error = SyncErrorKind.Other, Trigger = trigger });
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- Download and apply --------------------------------------------------------------------------------------

    private sealed class UpdateRequiredException : Exception;

    private sealed record Applied(int Files, int RowsApplied, IReadOnlyList<string> Conflicts);

    private bool CloudHasData(IReadOnlyList<SyncFileInfo> files, string? ownDevice) =>
        files.Any(f => SyncFormat.ParseSnapshot(f.Path) is not null
            || (SyncFormat.ParseChange(f.Path) is { } change && change.Device != ownDevice));

    private async Task<Applied> ApplyRemoteAsync(IReadOnlyList<SyncFileInfo> files, bool replace, CancellationToken ct)
    {
        var conflicts = new List<string>();
        var rowsApplied = 0;
        var filesApplied = 0;
        var applied = await _state.GetAppliedAsync(ct);
        var bootstrapped = await _state.GetLongAsync(SyncStateStore.BootstrappedKey, ct) == 1;

        // 1. Download everything that is needed before anything is changed (a failure here leaves the data untouched).
        RemoteDocument? snapshot = null;
        var snapshots = files.Select(f => (File: f, Info: SyncFormat.ParseSnapshot(f.Path))).Where(x => x.Info is not null)
            .Where(x => x.Info!.Value.Generation == _generation)
            .OrderByDescending(x => x.Info!.Value.At).ToList();
        var pending = PendingChanges(files, applied);
        var gap = pending.Any(g => g.Value[0].Sequence > applied.GetValueOrDefault(g.Key) + 1);
        if ((!bootstrapped || gap) && snapshots.Count > 0)
        {
            var (file, info) = snapshots[0];
            snapshot = await LoadAsync(file.Path, info!.Value.Device, 0, isSnapshot: true, info.Value.At, ct);
            if (snapshot.IsBroken)
            {
                conflicts.Add($"{file.Path}: unreadable snapshot, ignored");
                snapshot = null;
            }
        }

        if (snapshot is { NeedsNewerApp: true })
        {
            throw new UpdateRequiredException();
        }

        var coveredByAnySnapshot = snapshot?.Sequences ?? new Dictionary<string, long>();
        var queues = new Dictionary<string, List<RemoteDocument>>(StringComparer.Ordinal);
        foreach (var (device, list) in pending)
        {
            var from = Math.Max(applied.GetValueOrDefault(device), coveredByAnySnapshot.GetValueOrDefault(device));
            var queue = new List<RemoteDocument>();
            foreach (var file in list.Where(f => f.Sequence > from))
            {
                var doc = await LoadAsync(file.Path, device, file.Sequence, isSnapshot: false, file.Modified, ct);
                if (doc.NeedsNewerApp)
                {
                    throw new UpdateRequiredException();
                }

                queue.Add(doc);
            }

            if (queue.Count > 0)
            {
                queues[device] = queue;
            }
        }

        // 2. The snapshot (a new device, or a gap because files were deleted) in its own transaction.
        if (snapshot is not null)
        {
            var result = await ApplyDocumentAsync(snapshot, replace, ct);
            rowsApplied += result.Rows;
            conflicts.AddRange(result.Conflicts);
            foreach (var (device, sequence) in snapshot.Sequences.Where(s => s.Key != _deviceId))
            {
                await _state.SetLongAsync(SyncStateStore.AppliedPrefix + device, Math.Max(sequence, applied.GetValueOrDefault(device)), ct);
                applied[device] = Math.Max(sequence, applied.GetValueOrDefault(device));
            }

            filesApplied++;
            AddDetail($"Applied snapshot {snapshot.Path} ({snapshot.RowCount} rows).");
        }

        else if (replace)
        {
            // The cloud holds change files only: the data is still replaced, by those files alone.
            await ApplyDocumentAsync(
                new RemoteDocument { Path = string.Empty, Device = string.Empty, Sequence = 0, IsSnapshot = true, CreatedAt = _clock.UtcNow, NeedsNewerApp = false },
                replace: true,
                ct);
        }

        await _state.SetLongAsync(SyncStateStore.BootstrappedKey, 1, ct);

        // 3. Change files in rounds: a file whose rows point to rows of another device's file not yet applied waits for it.
        var progress = true;
        while (progress && queues.Count > 0)
        {
            progress = false;
            foreach (var device in queues.Keys.ToList())
            {
                var queue = queues[device];
                while (queue.Count > 0)
                {
                    var doc = queue[0];
                    if (doc.Sequence <= applied.GetValueOrDefault(device))
                    {
                        queue.RemoveAt(0);
                        continue;
                    }

                    if (doc.IsBroken)
                    {
                        conflicts.Add($"{doc.Path}: unreadable file");
                        break;
                    }

                    var result = await ApplyDocumentAsync(doc, replace: false, ct);
                    if (!result.Done)
                    {
                        break;
                    }

                    queue.RemoveAt(0);
                    applied[device] = doc.Sequence;
                    await _state.SetLongAsync(SyncStateStore.AppliedPrefix + device, doc.Sequence, ct);
                    rowsApplied += result.Rows;
                    filesApplied++;
                    conflicts.AddRange(result.Conflicts);
                    progress = true;
                    AddDetail($"Applied {doc.Path} ({doc.RowCount} rows).");
                }

                if (queue.Count == 0)
                {
                    queues.Remove(device);
                }
            }
        }

        foreach (var (device, queue) in queues.Where(q => q.Value.Count > 0))
        {
            if (!queue[0].IsBroken)
            {
                conflicts.Add($"{device}: waiting for records of another device");
            }
        }

        foreach (var text in conflicts)
        {
            AddDetail("Conflict: " + text);
        }

        return new Applied(filesApplied, rowsApplied, conflicts);
    }

    private Dictionary<string, List<(string Path, long Sequence, DateTimeOffset Modified)>> PendingChanges(IReadOnlyList<SyncFileInfo> files, Dictionary<string, long> applied) =>
        files.Select(f => (File: f, Info: SyncFormat.ParseChange(f.Path)))
            .Where(x => x.Info is not null)
            .GroupBy(x => x.Info!.Value.Device, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (Path: x.File.Path, Sequence: x.Info!.Value.Sequence, Modified: x.File.ModifiedAt ?? DateTimeOffset.MinValue))
                    .Where(x => x.Sequence > applied.GetValueOrDefault(g.Key))
                    .OrderBy(x => x.Sequence)
                    .ToList(),
                StringComparer.Ordinal)
            .Where(g => g.Value.Count > 0 && g.Key != _deviceId)
            .ToDictionary(g => g.Key, g => g.Value, StringComparer.Ordinal);

    private async Task<RemoteDocument> LoadAsync(string path, string device, long sequence, bool isSnapshot, DateTimeOffset at, CancellationToken ct)
    {
        await using var stream = await _storage.DownloadAsync(path, ct);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        if (isSnapshot)
        {
            try
            {
                bytes = SyncFormat.Gunzip(bytes);
            }
            catch (InvalidDataException)
            {
                return RemoteDocument.Parse(path, [], device, sequence, isSnapshot, at);
            }
        }

        return RemoteDocument.Parse(path, bytes, device, sequence, isSnapshot, at);
    }

    private sealed record DocumentResult(bool Done, int Rows, IReadOnlyList<string> Conflicts);

    // One transaction per file. A foreign-key failure at commit (the parent row comes with a file not applied yet) is "not done".
    private async Task<DocumentResult> ApplyDocumentAsync(RemoteDocument doc, bool replace, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
            using (var pragma = connection.CreateCommand())
            {
                pragma.Transaction = transaction;
                pragma.CommandText = "PRAGMA defer_foreign_keys=ON";
                pragma.ExecuteNonQuery();
            }

            var applier = new RowApplier(connection, transaction, _deviceId);
            if (replace)
            {
                foreach (var table in SyncFormat.Tables)
                {
                    applier.Purge(table);
                }
            }

            foreach (var (table, rows) in doc.Tables)
            {
                applier.Apply(table, rows, doc.Device, force: replace);
            }

            try
            {
                await transaction.CommitAsync(ct);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return new DocumentResult(false, 0, []);
            }

            return new DocumentResult(true, applier.Written, applier.Conflicts);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    // ---- Upload, snapshot, clean-up ------------------------------------------------------------------------------

    private async Task<(int Files, int Rows)> UploadAsync(IReadOnlyList<SyncFileInfo> files, CancellationToken ct)
    {
        var entries = (await _state.ReadOutboxAsync(ct)).ToList();
        if (entries.Count == 0)
        {
            return (0, 0);
        }

        var known = SyncFormat.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var unknown = entries.Where(e => !known.ContainsKey(e.Table)).ToList();
        if (unknown.Count > 0)
        {
            await _state.RemoveFromOutboxAsync(unknown, ct);
            entries = [.. entries.Except(unknown)];
        }

        // A crash between an upload and the stored sequence must not reuse a number other devices may have applied already.
        var remoteMax = files.Select(f => SyncFormat.ParseChange(f.Path)).Where(c => c is not null && c.Value.Device == _deviceId)
            .Select(c => c!.Value.Sequence).DefaultIfEmpty(0).Max();
        var sequence = Math.Max(await _state.GetLongAsync(SyncStateStore.LocalSequenceKey, ct), remoteMax);
        var uploaded = 0;
        var rows = 0;
        foreach (var chunk in entries.Chunk(_options.MaxRowsPerFile))
        {
            sequence++;
            var content = await BuildChangeFileAsync(chunk, sequence, known, ct);
            await _storage.UploadAsync(SyncFormat.ChangePathFor(_deviceId, sequence), new MemoryStream(content), ct);
            await _state.SetLongAsync(SyncStateStore.LocalSequenceKey, sequence, ct);
            await _state.RemoveFromOutboxAsync(chunk, ct);
            await RecordDeletedAttachmentsAsync(chunk, ct);
            uploaded++;
            rows += chunk.Length;
            AddDetail($"Uploaded {SyncFormat.ChangePathFor(_deviceId, sequence)} ({chunk.Length} rows).");
        }

        return (uploaded, rows);
    }

    private async Task<byte[]> BuildChangeFileAsync(
        (string Table, string Id, long Stamp)[] chunk, long sequence, Dictionary<string, DataExchangeService.TableInfo> known, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            using var stream = new MemoryStream();
            using (var writer = SyncFormat.NewWriter(stream))
            {
                writer.WriteStartObject();
                SyncFormat.WriteHeader(writer, _options.AppVersion, _deviceId, _clock.UtcNow);
                writer.WriteNumber("sequence", sequence);
                writer.WriteStartObject("rows");
                foreach (var group in chunk.GroupBy(c => c.Table, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartArray(group.Key);
                    SyncFormat.WriteRows(connection, null, known[group.Key], writer, [.. group.Select(g => g.Id)]);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return stream.ToArray();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Writes a full snapshot now (also used by the first device and on request).</summary>
    public async Task WriteSnapshotAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var applied = await _state.GetAppliedAsync(ct);
        var own = await _state.GetLongAsync(SyncStateStore.LocalSequenceKey, ct);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        byte[] bytes;
        try
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            using var stream = new MemoryStream();
            using (var writer = SyncFormat.NewWriter(stream))
            {
                writer.WriteStartObject();
                SyncFormat.WriteHeader(writer, _options.AppVersion, _deviceId, now);
                writer.WriteStartObject("sequences");
                foreach (var (device, sequence) in applied.Append(new KeyValuePair<string, long>(_deviceId, own)).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WriteNumber(device, sequence);
                }

                writer.WriteEndObject();
                writer.WriteStartObject("rows");
                foreach (var table in SyncFormat.Tables)
                {
                    writer.WriteStartArray(table.Name);
                    SyncFormat.WriteRows(connection, null, table, writer, null);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            bytes = SyncFormat.Gzip(stream.ToArray());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        var path = SyncFormat.SnapshotPathFor(_deviceId, now, _generation);
        await _storage.UploadAsync(path, new MemoryStream(bytes), ct);
        await _state.SetLongAsync(SyncStateStore.LastSnapshotSequenceKey, own, ct);
        var history = await _state.GetAsync(SyncStateStore.OwnSnapshotsKey, ct) ?? string.Empty;
        await _state.SetAsync(SyncStateStore.OwnSnapshotsKey, history + $"{now.UtcTicks}:{own};", ct);
        AddDetail($"Wrote snapshot {path}.");
    }

    private async Task WriteSnapshotIfDueAsync(CancellationToken ct)
    {
        var own = await _state.GetLongAsync(SyncStateStore.LocalSequenceKey, ct);
        if (own - await _state.GetLongAsync(SyncStateStore.LastSnapshotSequenceKey, ct) >= _options.SnapshotEvery)
        {
            await WriteSnapshotAsync(ct);
        }
    }

    // Own change files covered by an own snapshot older than the retention period are deleted; of the own snapshots only the
    // newest two stay. Files of other devices are never touched. Foreign files in the folder are ignored.
    private async Task CleanUpAsync(IReadOnlyList<SyncFileInfo> files, CancellationToken ct)
    {
        await CleanUpDeletedAttachmentsAsync(ct);
        var history = (await _state.GetAsync(SyncStateStore.OwnSnapshotsKey, ct) ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Split(':'))
            .Select(p => (At: new DateTimeOffset(long.Parse(p[0], CultureInfo.InvariantCulture), TimeSpan.Zero), Sequence: long.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToList();
        var old = history.Where(h => _clock.UtcNow - h.At > _options.Retention).OrderByDescending(h => h.Sequence).Cast<(DateTimeOffset At, long Sequence)?>().FirstOrDefault();
        if (old is { } covered)
        {
            foreach (var file in files)
            {
                if (SyncFormat.ParseChange(file.Path) is { } change && change.Device == _deviceId && change.Sequence <= covered.Sequence)
                {
                    await _storage.DeleteAsync(file.Path, ct);
                    AddDetail($"Deleted {file.Path} (covered by a snapshot).");
                }
            }
        }

        var keep = history.OrderByDescending(h => h.At).Take(2).Select(h => SyncFormat.SnapshotPathFor(_deviceId, h.At, _generation)).ToHashSet(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (SyncFormat.ParseSnapshot(file.Path) is { } snapshot && snapshot.Device == _deviceId && !keep.Contains(file.Path) && history.Count > 2)
            {
                await _storage.DeleteAsync(file.Path, ct);
            }
        }

        if (history.Count > 2)
        {
            await _state.SetAsync(
                SyncStateStore.OwnSnapshotsKey,
                string.Concat(history.OrderByDescending(h => h.At).Take(2).Reverse().Select(h => $"{h.At.UtcTicks}:{h.Sequence};")),
                ct);
        }
    }

    // ---- Helpers -------------------------------------------------------------------------------------------------

    private bool ClockSkewed(DateTimeOffset now) =>
        _storage is IServerClock { ServerTime: { } server, ObservedAt: { } observed } && (observed - server).Duration() > MaxSkew;

    private void AddDetail(string text)
    {
        lock (_details)
        {
            _details.Insert(0, new SyncDetail(_clock.UtcNow, text));
            if (_details.Count > MaxDetails)
            {
                _details.RemoveRange(MaxDetails, _details.Count - MaxDetails);
            }
        }
    }

    private void SetStatus(SyncStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
