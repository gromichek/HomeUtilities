using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;

namespace HomeUtilities.ViewModels.Tests;

public class AutoBackupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAutoBackupStorage _storage = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeClock _clock = new(Now);
    private readonly FakeErrorLog _log = new();
    private readonly DataChangeNotifier _notifier = new();
    private readonly FakeDatabaseInitializer _initializer = new();

    private AutoBackupService Create() => new(_storage, _settings, _clock, _log, _notifier, _initializer);

    [Fact]
    public void Defaults_AreOnDailySevenCopies()
    {
        Assert.Equal(new AutoBackupSettings(true, 1, 7), Create().Settings);
    }

    [Fact]
    public void Saved_settings_are_kept_normalized_and_given_to_the_storage()
    {
        var service = Create();

        service.Save(new AutoBackupSettings(false, 3, 14));
        service.Save(service.Settings with { KeepCopies = 99 });

        Assert.Equal(new AutoBackupSettings(false, 3, 7), service.Settings);
        Assert.Equal(7, _storage.KeepCopies);
    }

    [Fact]
    public async Task First_start_with_a_database_makes_a_regular_copy_after_the_database_is_ready()
    {
        var service = Create();

        await service.RunIfDueAsync();

        Assert.Equal(1, _initializer.Calls);
        Assert.Equal([AutoBackupReason.Scheduled], _storage.Created);
    }

    [Fact]
    public async Task Nothing_is_copied_without_a_database_or_when_switched_off()
    {
        var service = Create();
        _storage.DatabaseExists = false;
        await service.RunIfDueAsync();

        _storage.DatabaseExists = true;
        service.Save(service.Settings with { Enabled = false });
        await service.RunIfDueAsync();

        Assert.Empty(_storage.Created);
    }

    [Fact]
    public async Task A_copy_younger_than_the_interval_is_not_repeated_even_after_a_change()
    {
        _storage.Existing.Add(Copy(Now.AddHours(-5)));
        var service = Create();
        _notifier.Notify();

        await service.RunIfDueAsync();

        Assert.Empty(_storage.Created);
    }

    [Fact]
    public async Task An_old_copy_without_a_change_since_is_not_repeated()
    {
        _storage.Existing.Add(Copy(Now.AddDays(-2)));
        var service = Create();

        await service.RunIfDueAsync();

        Assert.Empty(_storage.Created);
    }

    [Fact]
    public async Task An_old_copy_and_a_change_since_makes_a_new_copy()
    {
        _storage.Existing.Add(Copy(Now.AddDays(-2)));
        var service = Create();
        _clock.UtcNow = Now.AddDays(-1);
        _notifier.Notify();
        _clock.UtcNow = Now;

        await service.RunIfDueAsync();

        Assert.Equal([AutoBackupReason.Scheduled], _storage.Created);
        Assert.NotNull(_settings.Values[AutoBackupService.DataChangedKey]);
    }

    [Fact]
    public async Task A_change_before_the_last_copy_does_not_count()
    {
        _storage.Existing.Add(Copy(Now.AddDays(-2)));
        var service = Create();
        _clock.UtcNow = Now.AddDays(-3);
        _notifier.Notify();
        _clock.UtcNow = Now;

        await service.RunIfDueAsync();

        Assert.Empty(_storage.Created);
    }

    [Fact]
    public async Task Risky_operations_always_get_a_copy_even_when_regular_copies_are_off()
    {
        var service = Create();
        service.Save(service.Settings with { Enabled = false });

        var ok = await service.BackupBeforeAsync(AutoBackupReason.BeforeImport);

        Assert.True(ok);
        Assert.Equal([AutoBackupReason.BeforeImport], _storage.Created);
    }

    [Fact]
    public async Task Without_a_database_there_is_nothing_to_copy_before_an_operation()
    {
        _storage.DatabaseExists = false;

        Assert.True(await Create().BackupBeforeAsync(AutoBackupReason.BeforeWipe));
        Assert.Empty(_storage.Created);
    }

    [Fact]
    public async Task A_failed_copy_is_logged_remembered_and_cleared_by_the_next_success()
    {
        var service = Create();
        _storage.Failure = new IOException("disk full");

        var ok = await service.BackupBeforeAsync(AutoBackupReason.BeforeImport);

        Assert.False(ok);
        Assert.Equal("disk full", service.LastError);
        Assert.Equal("disk full", Assert.Single(_log.Entries).Exception.Message);

        _storage.Failure = null;
        Assert.True(await service.BackupBeforeAsync(AutoBackupReason.BeforeImport));
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task A_failing_database_start_is_logged_and_never_thrown()
    {
        var service = Create();
        _initializer.Failure = new InvalidOperationException("migration failed");

        await service.RunIfDueAsync();

        Assert.Equal("migration failed", service.LastError);
        Assert.Empty(_storage.Created);
    }

    [Fact]
    public async Task Two_copies_never_run_at_once_and_a_regular_copy_gives_way_to_a_running_one()
    {
        var service = Create();
        _storage.Gate = new TaskCompletionSource();
        var first = service.BackupBeforeAsync(AutoBackupReason.BeforeImport);
        await _storage.Started.Task;

        await service.RunIfDueAsync();
        var second = service.BackupBeforeAsync(AutoBackupReason.BeforeWipe);
        await Task.Delay(30);
        Assert.Equal(1, _storage.Running);
        Assert.Empty(_storage.Created);

        _storage.Gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal([AutoBackupReason.BeforeImport, AutoBackupReason.BeforeWipe], _storage.Created);
        Assert.Equal(1, _storage.MaxRunning);
    }

    private static AutoBackupInfo Copy(DateTimeOffset at) => new(AutoBackupNames.Format(at, AutoBackupReason.Scheduled), at, 1000, AutoBackupReason.Scheduled);
}

internal sealed class FakeAutoBackupStorage : IAutoBackupStorage
{
    private int _running;

    public event EventHandler<AutoBackupResult>? Completed;

    public int KeepCopies { get; set; } = 7;

    public bool DatabaseExists { get; set; } = true;

    public List<AutoBackupInfo> Existing { get; } = [];

    public List<AutoBackupReason> Created { get; } = [];

    public Exception? Failure { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public TaskCompletionSource Started { get; } = new();

    public int Running => _running;

    public int MaxRunning { get; private set; }

    public async Task<AutoBackupResult> CreateAsync(AutoBackupReason reason, CancellationToken ct = default)
    {
        MaxRunning = Math.Max(MaxRunning, Interlocked.Increment(ref _running));
        try
        {
            Started.TrySetResult();
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            AutoBackupResult result;
            if (Failure is { } failure)
            {
                result = new AutoBackupResult(reason, null, failure);
            }
            else
            {
                Created.Add(reason);
                var copy = new AutoBackupInfo(AutoBackupNames.Format(DateTimeOffset.UtcNow, reason), DateTimeOffset.UtcNow, 1000, reason);
                Existing.Add(copy);
                result = new AutoBackupResult(reason, copy, null);
            }

            Completed?.Invoke(this, result);
            return result;
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }

    public IReadOnlyList<AutoBackupInfo> List() => [.. Existing.OrderByDescending(c => c.CreatedAt)];

    public Stream? Open(string name) => null;
}

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

internal sealed class FakeDatabaseInitializer : IDatabaseInitializer
{
    public int Calls { get; private set; }

    public Exception? Failure { get; set; }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        Calls++;
        return Failure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }
}
