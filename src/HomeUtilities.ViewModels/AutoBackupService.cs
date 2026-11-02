using System.Globalization;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Automatic local backups: the settings, the regular copy at app start and the copies before risky operations.
/// Copies are made in the background and never throw; a failure goes to the error log and is kept for the Backup section.
/// </summary>
public interface IAutoBackupService
{
    /// <summary>Raised (on any thread) when a copy was made or failed, or the settings changed.</summary>
    event EventHandler? Changed;

    /// <summary>The current settings (on by default, daily, 7 copies).</summary>
    AutoBackupSettings Settings { get; }

    /// <summary>Stores new settings; values outside the offered choices are replaced by the defaults.</summary>
    void Save(AutoBackupSettings settings);

    /// <summary>Message of the last failed copy, or null if the last attempt worked (or none was made).</summary>
    string? LastError { get; }

    /// <summary>The copies on this device, newest first.</summary>
    IReadOnlyList<AutoBackupInfo> Copies();

    /// <summary>Opens a copy for reading (seekable); null if it no longer exists.</summary>
    Stream? Open(string name);

    /// <summary>
    /// Start-up step, run after the first screen is shown: waits for the database, then makes a copy if one is due (interval
    /// elapsed and data changed since the last copy). Does nothing when switched off. Never throws.
    /// </summary>
    Task RunIfDueAsync(CancellationToken ct = default);

    /// <summary>
    /// Makes a copy before a risky operation (import, restore, delete all data). Always made, whatever the on/off setting.
    /// Returns false if it failed; the failure is already logged and shown in the Backup section, and the caller carries on.
    /// </summary>
    Task<bool> BackupBeforeAsync(AutoBackupReason reason, CancellationToken ct = default);
}

/// <summary>Default <see cref="IAutoBackupService"/>.</summary>
public sealed class AutoBackupService : IAutoBackupService, IDisposable
{
    /// <summary>Settings key: <c>1</c> or <c>0</c>.</summary>
    public const string EnabledKey = "AutoBackupEnabled";

    /// <summary>Settings key: interval in days.</summary>
    public const string IntervalKey = "AutoBackupIntervalDays";

    /// <summary>Settings key: number of kept copies.</summary>
    public const string KeepKey = "AutoBackupKeepCopies";

    /// <summary>Settings key: ISO 8601 time of the last data change on this device.</summary>
    public const string DataChangedKey = "AutoBackupDataChangedAt";

    /// <summary>Settings key: message of the last failed copy.</summary>
    public const string LastErrorKey = "AutoBackupLastError";

    private readonly IAutoBackupStorage _storage;
    private readonly ISettingsStore _settings;
    private readonly IClock _clock;
    private readonly IErrorLog _errorLog;
    private readonly IDataChangeNotifier _notifier;
    private readonly IDatabaseInitializer _initializer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the service and starts listening to data changes and to the copies made by others.</summary>
    public AutoBackupService(
        IAutoBackupStorage storage,
        ISettingsStore settings,
        IClock clock,
        IErrorLog errorLog,
        IDataChangeNotifier notifier,
        IDatabaseInitializer initializer)
    {
        _storage = storage;
        _settings = settings;
        _clock = clock;
        _errorLog = errorLog;
        _notifier = notifier;
        _initializer = initializer;
        _storage.KeepCopies = Settings.KeepCopies;
        _notifier.Changed += OnDataChanged;
        _storage.Completed += OnCompleted;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public AutoBackupSettings Settings
    {
        get
        {
            var enabled = _settings.GetString(EnabledKey) != "0";
            _ = int.TryParse(_settings.GetString(IntervalKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval);
            _ = int.TryParse(_settings.GetString(KeepKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var keep);
            return new AutoBackupSettings(enabled, interval, keep).Normalized();
        }
    }

    /// <inheritdoc />
    public string? LastError => _settings.GetString(LastErrorKey) is { Length: > 0 } error ? error : null;

    /// <inheritdoc />
    public void Save(AutoBackupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalized();
        _settings.SetString(EnabledKey, normalized.Enabled ? "1" : "0");
        _settings.SetString(IntervalKey, normalized.IntervalDays.ToString(CultureInfo.InvariantCulture));
        _settings.SetString(KeepKey, normalized.KeepCopies.ToString(CultureInfo.InvariantCulture));
        _storage.KeepCopies = normalized.KeepCopies;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public IReadOnlyList<AutoBackupInfo> Copies() => _storage.List();

    /// <inheritdoc />
    public Stream? Open(string name) => _storage.Open(name);

    /// <inheritdoc />
    public async Task RunIfDueAsync(CancellationToken ct = default)
    {
        try
        {
            if (!Settings.Enabled)
            {
                return;
            }

            // The first screen starts the migration; wait for it (the call is serialized) instead of copying a half-migrated file.
            await _initializer.InitializeAsync(ct);
            if (!await _gate.WaitAsync(0, ct))
            {
                return;
            }

            try
            {
                var last = _storage.List().Select(c => (DateTimeOffset?)c.CreatedAt).FirstOrDefault();
                var due = AutoBackupPolicy.IsDue(Settings, _clock.UtcNow, last, LastDataChange(), _storage.DatabaseExists);
                if (due)
                {
                    await _storage.CreateAsync(AutoBackupReason.Scheduled, ct);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
        catch (Exception ex)
        {
            _errorLog.Append("Automatic backup", ex);
            SetError(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<bool> BackupBeforeAsync(AutoBackupReason reason, CancellationToken ct = default)
    {
        if (!_storage.DatabaseExists)
        {
            return true;
        }

        await _gate.WaitAsync(ct);
        try
        {
            return (await _storage.CreateAsync(reason, ct)).Succeeded;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stops listening.</summary>
    public void Dispose()
    {
        _notifier.Changed -= OnDataChanged;
        _storage.Completed -= OnCompleted;
        _gate.Dispose();
    }

    private DateTimeOffset? LastDataChange() =>
        DateTimeOffset.TryParse(_settings.GetString(DataChangedKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    // Written on every save (a small preference); compared with the start time of the newest copy, so a change made while a
    // copy is being written still counts as "changed since".
    private void OnDataChanged(object? sender, EventArgs e) =>
        _settings.SetString(DataChangedKey, _clock.UtcNow.ToString("O", CultureInfo.InvariantCulture));

    private void OnCompleted(object? sender, AutoBackupResult result)
    {
        if (result.Error is { } error)
        {
            _errorLog.Append($"Automatic backup ({result.Reason})", error);
            SetError(error.Message);
        }
        else
        {
            SetError(null);
        }
    }

    private void SetError(string? message)
    {
        _settings.SetString(LastErrorKey, message);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
