using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;
using HomeUtilities.ViewModels;

namespace HomeUtilities.ViewModels.Tests;

internal sealed class FakeDatabaseCleaner : IDatabaseCleaner
{
    /// <summary>Everything the cleaner and the other fakes did, in order.</summary>
    public List<string> Journal { get; set; } = [];

    public CleanUpPreview Preview { get; set; } = new(0, 0, 0);

    public CleanUpResult Result { get; set; } = new(0, 0, 0, 0, 0, []);

    public int CleanCalls { get; private set; }

    public Task<CleanUpPreview> PreviewAsync(CancellationToken ct = default) => Task.FromResult(Preview);

    public Task<CleanUpResult> CleanAsync(CancellationToken ct = default)
    {
        CleanCalls++;
        Journal.Add("clean");
        return Task.FromResult(Result);
    }
}

public class DatabaseCleanUpViewModelTests
{
    private readonly FakeDatabaseCleaner _cleaner = new();
    private readonly FakeAutoBackupService _backups = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSnackbarService _snackbar = new();
    private readonly FakeErrorLog _log = new();
    private readonly FakeSyncEngine _engine = new();

    private DatabaseCleanUpViewModel Create(bool withEngine = true) =>
        new(_cleaner, _backups, _dialogs, new FakeLocalizer(new FakeLanguageService()), _snackbar, _log, withEngine ? _engine : null);

    [Fact]
    public async Task NothingToClean_ShowsAMessage_NoBackupNoChange()
    {
        var vm = Create();

        await vm.CleanUpCommand.ExecuteAsync(null);

        Assert.Equal(0, _cleaner.CleanCalls);
        Assert.Empty(_backups.Backups);
        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal("CleanUpNothing [en]", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Declined_ChangesNothingAndMakesNoBackup()
    {
        _cleaner.Preview = new CleanUpPreview(37, 5, 2_097_152);
        _dialogs.ConfirmResult = false;

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Equal(0, _cleaner.CleanCalls);
        Assert.Empty(_backups.Backups);
        // 37 records, 5 files, 2 MB (formatted in the ViewModel with the app culture).
        Assert.Contains("CleanUpConfirm 37|5|BackupSizeMb 2 [en]", Assert.Single(_dialogs.Confirmations).Message);
    }

    [Fact]
    public async Task Confirmed_BackupIsMadeBeforeAnyChange()
    {
        _cleaner.Preview = new CleanUpPreview(2, 1, 1000);
        _cleaner.Result = new CleanUpResult(2, 1, 1000, 8_388_608, 2_097_152, []);
        var journal = new List<string>();
        _cleaner.Journal = journal;
        _backups.OnBackup = reason => journal.Add("backup " + reason);

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Equal(["backup BeforeCleanUp", "clean"], journal);
        var report = Assert.Single(_dialogs.Alerts).Message;
        Assert.Contains("CleanUpDone 2|1|BackupSizeKb 1 [en]|BackupSizeMb 8 [en]|BackupSizeMb 2 [en]", report);
        // The running message replaces a pending Undo snackbar.
        Assert.Equal("CleanUpRunning [en]", Assert.Single(_snackbar.Messages));
    }

    [Fact]
    public async Task FailedBackup_UserDeclinesToContinue_NothingIsCleaned()
    {
        _cleaner.Preview = new CleanUpPreview(2, 0, 0);
        _backups.BackupSucceeds = false;
        _dialogs.ConfirmAnswers.Enqueue(true); // the clean-up question
        _dialogs.ConfirmAnswers.Enqueue(false); // "Continue without a copy?"

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Equal(0, _cleaner.CleanCalls);
        Assert.Empty(_dialogs.Alerts);
    }

    [Fact]
    public async Task KeptRecords_AreWrittenToTheErrorLog()
    {
        _cleaner.Preview = new CleanUpPreview(3, 0, 0);
        _cleaner.Result = new CleanUpResult(2, 0, 0, 10_000, 8_000, ["Bills: 1 deleted record(s) kept because live records still refer to them"]);

        await Create().CleanUpCommand.ExecuteAsync(null);

        var entry = Assert.Single(_log.Entries);
        Assert.Contains("Bills: 1 deleted record(s) kept", entry.Exception.Message);
        Assert.Contains("CleanUpKept 1", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task SyncOff_NoCloudQuestion_NoEngineCall()
    {
        _cleaner.Preview = new CleanUpPreview(2, 0, 0);
        _engine.IsEnabled = false;

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Empty(_engine.Enabled);
        Assert.Equal(0, _engine.MarkNeedsRejoinCalls);
        Assert.Single(_dialogs.Confirmations);
    }

    [Fact]
    public async Task SyncOn_ConfirmQuestionMentionsTheCloud_AndTheCloudIsReplaced()
    {
        _cleaner.Preview = new CleanUpPreview(2, 0, 0);
        _engine.IsEnabled = true;

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Contains("CleanUpConfirmSync", _dialogs.Confirmations[0].Message);
        Assert.Equal([FirstJoinChoice.ReplaceCloud], _engine.Enabled);
        Assert.Equal(0, _engine.MarkNeedsRejoinCalls);
        Assert.Contains("CleanUpCloudReplaced", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task SyncOn_ReplaceCancelled_SyncIsSwitchedOffAndNeedsRejoin()
    {
        _cleaner.Preview = new CleanUpPreview(2, 0, 0);
        _engine.IsEnabled = true;
        _dialogs.ConfirmAnswers.Enqueue(true); // clean up
        _dialogs.ConfirmAnswers.Enqueue(false); // replace the cloud: not now

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Equal(1, _cleaner.CleanCalls);
        Assert.Empty(_engine.Enabled);
        Assert.Equal(1, _engine.MarkNeedsRejoinCalls);
        Assert.False(_engine.IsEnabled);
        Assert.True(_engine.NeedsRejoin);
        Assert.Contains("CleanUpSyncOff", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task SyncOn_CloudUnreachable_SyncIsSwitchedOffAndNeedsRejoin()
    {
        _cleaner.Preview = new CleanUpPreview(2, 0, 0);
        _engine.IsEnabled = true;
        _engine.EnableFailure = new SyncException(SyncErrorKind.NoNetwork, "offline");

        await Create().CleanUpCommand.ExecuteAsync(null);

        Assert.Equal(1, _engine.MarkNeedsRejoinCalls);
        Assert.Contains("CleanUpCloudFailed offline", Assert.Single(_dialogs.Alerts).Message);
        Assert.Contains(_log.Entries, e => e.Source.Contains("clean-up", StringComparison.Ordinal));
    }
}

internal sealed class FakeSyncEngine : ISyncEngine
{
    public SyncStatus Status { get; private set; } = SyncStatus.Off;

    public bool IsEnabled { get; set; }

    public bool NeedsRejoin { get; private set; }

    public IReadOnlyList<SyncDetail> Details => [];

    public List<FirstJoinChoice> Enabled { get; } = [];

    public int MarkNeedsRejoinCalls { get; private set; }

    public Exception? EnableFailure { get; set; }

    public event EventHandler? StatusChanged
    {
        add { }
        remove { }
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<FirstJoinInfo> InspectAsync(CancellationToken ct = default) => Task.FromResult(new FirstJoinInfo(false, false));

    public Task EnableAsync(FirstJoinChoice choice, CancellationToken ct = default)
    {
        if (EnableFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        Enabled.Add(choice);
        IsEnabled = true;
        return Task.CompletedTask;
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        IsEnabled = false;
        return Task.CompletedTask;
    }

    public Task ForgetJoinAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task MarkNeedsRejoinAsync(CancellationToken ct = default)
    {
        MarkNeedsRejoinCalls++;
        IsEnabled = false;
        NeedsRejoin = true;
        return Task.CompletedTask;
    }

    public Task<CloudFilesReport> ListCloudFilesAsync(CancellationToken ct = default) => Task.FromResult(new CloudFilesReport([], [], 0, 0, 0, 0));

    public Task<bool> SyncAsync(SyncTrigger trigger, CancellationToken ct = default) => Task.FromResult(IsEnabled);
}
