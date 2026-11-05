using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Sync;
using HomeUtilities.ViewModels;

namespace HomeUtilities.ViewModels.Tests;

public class SyncEngineViewModelTests
{
    private sealed class FakeEngine : ISyncEngine
    {
        public SyncStatus Status { get; set; } = SyncStatus.Off;

        public bool IsEnabled { get; set; }

        public bool NeedsRejoin { get; set; }

        public int ForgetCalls { get; private set; }

        public CloudFilesReport Report { get; set; } = new([], [], 0, 0, 0, 0);

        public Task ForgetJoinAsync(CancellationToken ct = default)
        {
            ForgetCalls++;
            IsEnabled = false;
            Status = SyncStatus.Off;
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public int MarkNeedsRejoinCalls { get; private set; }

        public Task MarkNeedsRejoinAsync(CancellationToken ct = default)
        {
            MarkNeedsRejoinCalls++;
            IsEnabled = false;
            NeedsRejoin = true;
            return Task.CompletedTask;
        }

        public Task<CloudFilesReport> ListCloudFilesAsync(CancellationToken ct = default) => Task.FromResult(Report);

        public void Raise() => StatusChanged?.Invoke(this, EventArgs.Empty);

        public FirstJoinInfo Info { get; set; } = new(false, false);

        public Exception? InspectFailure { get; set; }

        public List<FirstJoinChoice> Enabled { get; } = [];

        public List<SyncTrigger> Runs { get; } = [];

        public int DisableCalls { get; private set; }

        public IReadOnlyList<SyncDetail> Details { get; set; } = [];

        public event EventHandler? StatusChanged;

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<FirstJoinInfo> InspectAsync(CancellationToken ct = default) =>
            InspectFailure is null ? Task.FromResult(Info) : Task.FromException<FirstJoinInfo>(InspectFailure);

        public Task EnableAsync(FirstJoinChoice choice, CancellationToken ct = default)
        {
            Enabled.Add(choice);
            IsEnabled = true;
            Status = new SyncStatus(SyncStateKind.Synced, new DateTimeOffset(2026, 11, 2, 16, 42, 0, TimeSpan.Zero));
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task DisableAsync(CancellationToken ct = default)
        {
            DisableCalls++;
            IsEnabled = false;
            Status = SyncStatus.Off;
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task<bool> SyncAsync(SyncTrigger trigger, CancellationToken ct = default)
        {
            Runs.Add(trigger);
            return Task.FromResult(IsEnabled);
        }
    }

    private readonly FakeEngine _engine = new();
    private readonly SyncSettingsViewModelTests.FakeAccount _account = new() { State = SyncAccountState.SignedIn("anna@example.test") };
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAutoBackupService _backups = new();

    private SyncSettingsViewModel Vm() => new(
        _account,
        new NoStorage(),
        new FakeLocalizer(new FakeLanguageService()),
        _dialogs,
        new FakeErrorLog(),
        _engine,
        _backups);

    private sealed class NoStorage : ISyncStorage
    {
        public Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SyncFileInfo>>([]);

        public Task UploadAsync(string path, Stream content, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Stream> DownloadAsync(string path, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task SettleAsync(SyncSettingsViewModel vm)
    {
        for (var i = 0; i < 100 && vm.IsBusy; i++)
        {
            await Task.Delay(10);
        }

        await Task.Delay(30);
    }

    [Fact]
    public async Task Switching_on_with_an_empty_cloud_starts_here_without_a_question()
    {
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.StartHere], _engine.Enabled);
        Assert.True(vm.SyncOn);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task Switching_on_an_empty_device_downloads_without_asking_or_backing_up()
    {
        _engine.Info = new FirstJoinInfo(true, false);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task A_first_join_with_data_on_both_sides_offers_three_choices_with_Replace_preselected_and_a_backup_first()
    {
        _engine.Info = new FirstJoinInfo(true, true);
        Pick(_ => 0);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        var dialog = _shownDialog!;
        Assert.Equal(
            ["SyncJoinReplace [en]", "SyncJoinReplaceCloud [en]", "SyncJoinMerge [en]"],
            dialog.Items.Select(i => i.Text));
        Assert.Equal([FirstJoinChoice.Replace, FirstJoinChoice.ReplaceCloud, FirstJoinChoice.Merge], dialog.Items.Select(i => (FirstJoinChoice)i.Value!));
        Assert.All(dialog.Items, i => Assert.True(i.HasDescription));
        Assert.Equal("SyncJoinTitle [en]", dialog.Title);
        Assert.Equal("SyncJoinQuestion [en]", dialog.Message);
        Assert.Equal([AutoBackupReason.BeforeRestore], _backups.Backups);
        Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
    }

    [Fact]
    public async Task The_replace_cloud_choice_works_from_a_first_join_and_needs_no_backup()
    {
        _engine.Info = new FirstJoinInfo(true, true);
        Pick(_ => 1);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.ReplaceCloud], _engine.Enabled);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task Merge_needs_no_backup_and_cancel_leaves_sync_off()
    {
        _engine.Info = new FirstJoinInfo(true, true);
        Pick(_ => 2);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);
        Assert.Equal([FirstJoinChoice.Merge], _engine.Enabled);
        Assert.Empty(_backups.Backups);

        var second = new FakeEngine { Info = new FirstJoinInfo(true, true) };
        Pick(_ => null);
        var vm2 = new SyncSettingsViewModel(_account, new NoStorage(), new FakeLocalizer(new FakeLanguageService()), _dialogs, new FakeErrorLog(), second, _backups);
        vm2.SyncOn = true;
        await SettleAsync(vm2);
        Assert.Empty(second.Enabled);
        Assert.False(vm2.SyncOn);
    }

    [Fact]
    public void The_dialog_view_model_carries_the_full_long_text_and_sizes_the_popup_for_it()
    {
        var localizer = new FakeLocalizer(new FakeLanguageService());
        var longText = string.Join(' ', Enumerable.Repeat("The cloud already holds data and so does this device.", 6));
        var withText = new SingleChoiceDialogViewModel(localizer, "T", [new SingleChoiceOption(1, "A"), new SingleChoiceOption(2, "B")], 1, longText);
        var without = new SingleChoiceDialogViewModel(localizer, "T", [new SingleChoiceOption(1, "A"), new SingleChoiceOption(2, "B")], 1);

        Assert.Equal(longText, withText.Message);
        Assert.True(withText.HasMessage);
        Assert.False(without.HasMessage);
        Assert.True(withText.RowCountHint > without.RowCountHint);
    }
    [Fact]
    public async Task A_failed_check_of_the_cloud_shows_the_reason_and_leaves_the_switch_off()
    {
        _engine.InspectFailure = new SyncException(SyncErrorKind.NoNetwork, "offline");
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.False(vm.SyncOn);
        Assert.Equal("SyncError_NoNetwork [en]", vm.MessageText);
        Assert.Empty(_engine.Enabled);
    }

    [Fact]
    public async Task Sync_now_runs_a_manual_sync_and_disconnect_switches_sync_off_first()
    {
        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.Synced, new DateTimeOffset(2026, 11, 2, 16, 42, 0, TimeSpan.Zero));
        var vm = Vm();
        Assert.True(vm.SyncOn);
        Assert.True(vm.SyncNowCommand.CanExecute(null));

        await vm.SyncNowCommand.ExecuteAsync(null);
        Assert.Equal([SyncTrigger.Manual], _engine.Runs);

        await vm.DisconnectCommand.ExecuteAsync(null);
        Assert.Equal(1, _engine.ForgetCalls);
        Assert.False(vm.SyncOn);
    }

    [Theory]
    [InlineData(SyncStateKind.WaitingForNetwork, "SyncRunWaiting [en]")]
    [InlineData(SyncStateKind.UpdateRequired, "SyncRunUpdate [en]")]
    [InlineData(SyncStateKind.Syncing, "SyncRunSyncing [en]")]
    public async Task The_status_line_follows_the_engine(SyncStateKind kind, string expected)
    {
        _engine.IsEnabled = true;
        var vm = Vm();
        _engine.Status = new SyncStatus(kind);
        await vm.OnAppearingAsync();

        Assert.Equal(expected, vm.SyncRunText);
    }

    // ---- Scheduler ----

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

        public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    [Fact]
    public async Task A_saved_change_starts_one_debounced_run_and_leaving_the_app_runs_it_at_once()
    {
        _engine.IsEnabled = true;
        var notifier = new DataChangeNotifier();
        using var scheduler = new SyncScheduler(_engine, notifier, new Clock(), debounce: TimeSpan.FromMilliseconds(100));

        notifier.Notify();
        notifier.Notify();
        await Task.Delay(400);
        Assert.Equal([SyncTrigger.Debounced], _engine.Runs);

        _engine.Runs.Clear();
        notifier.Notify();
        await scheduler.FlushAsync();
        Assert.Equal([SyncTrigger.Leaving], _engine.Runs);
        await Task.Delay(300);
        Assert.Equal([SyncTrigger.Leaving], _engine.Runs);

        await scheduler.FlushAsync();
        Assert.Equal([SyncTrigger.Leaving], _engine.Runs);
    }

    [Fact]
    public async Task Changes_while_sync_is_off_or_running_start_nothing_and_resume_is_throttled()
    {
        var notifier = new DataChangeNotifier();
        var clock = new Clock();
        using var scheduler = new SyncScheduler(_engine, notifier, clock, debounce: TimeSpan.FromMilliseconds(50));

        notifier.Notify();
        await Task.Delay(200);
        Assert.Empty(_engine.Runs);

        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.Syncing);
        notifier.Notify();
        await Task.Delay(200);
        Assert.Empty(_engine.Runs);

        _engine.Status = new SyncStatus(SyncStateKind.Synced);
        await scheduler.StartAsync();
        await scheduler.ResumeAsync();
        Assert.Equal([SyncTrigger.Start], _engine.Runs);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await scheduler.ResumeAsync();
        Assert.Equal([SyncTrigger.Start, SyncTrigger.Resume], _engine.Runs);
    }

    // ---- T-032 ----

    /// <summary>The last choice dialog shown (title, full message, rows).</summary>
    private SingleChoiceDialogViewModel? _shownDialog;

    /// <summary>Scripts the user in the app's own choice dialog: the index of the row to pick, or null to cancel.</summary>
    private void Pick(Func<SingleChoiceDialogViewModel, int?> pick) =>
        _dialogs.EditorScript = editor =>
        {
            var dialog = (SingleChoiceDialogViewModel)editor;
            _shownDialog = dialog;
            if (pick(dialog) is { } index)
            {
                dialog.Items[index].ChooseCommand.Execute(null);
                dialog.ConfirmCommand.Execute(null);
            }
            else
            {
                dialog.CancelCommand.Execute(null);
            }
        };

    private sealed class FakeNetwork : ISyncConnectivity
    {
        public bool IsConnected { get; set; } = true;

        public bool IsUnmetered { get; set; } = true;

        public event EventHandler? Changed;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public List<int> Threads { get; } = [];

        public void Post(Action action)
        {
            // Hands the action to one fixed "UI thread" and records which thread ran it.
            Task.Run(() =>
            {
                Threads.Add(Environment.CurrentManagedThreadId);
                action();
            }).GetAwaiter().GetResult();
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task The_network_coming_back_syncs_in_the_foreground_only()
    {
        var network = new FakeNetwork();
        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.WaitingForNetwork);
        using var scheduler = new SyncScheduler(_engine, new DataChangeNotifier(), new Clock(), connectivity: network);

        network.Raise();
        await Task.Delay(100);
        Assert.Empty(_engine.Runs);

        await scheduler.StartAsync();
        _engine.Runs.Clear();
        network.Raise();
        await WaitForAsync(() => _engine.Runs.Count > 0);
        Assert.Equal([SyncTrigger.Network], _engine.Runs);

        await scheduler.FlushAsync();
        _engine.Runs.Clear();
        network.Raise();
        await Task.Delay(100);
        Assert.Empty(_engine.Runs);
    }

    [Fact]
    public async Task The_retry_timer_runs_only_in_the_foreground_and_only_while_waiting_or_failed()
    {
        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.Synced);
        using var scheduler = new SyncScheduler(_engine, new DataChangeNotifier(), new Clock(), retryInterval: TimeSpan.FromMilliseconds(40));
        Assert.False(scheduler.IsForeground);

        // Not started: no timer, no run.
        await Task.Delay(150);
        Assert.Empty(_engine.Runs);

        await scheduler.StartAsync();
        Assert.True(scheduler.IsForeground);
        _engine.Runs.Clear();

        // Synced: the ticks do nothing.
        await Task.Delay(150);
        Assert.Empty(_engine.Runs);

        _engine.Status = new SyncStatus(SyncStateKind.WaitingForNetwork);
        await WaitForAsync(() => _engine.Runs.Count > 0);
        Assert.Contains(SyncTrigger.Retry, _engine.Runs);

        _engine.Status = new SyncStatus(SyncStateKind.Failed, Error: SyncErrorKind.Other);
        _engine.Runs.Clear();
        await WaitForAsync(() => _engine.Runs.Count > 0);
        Assert.Contains(SyncTrigger.Retry, _engine.Runs);

        // Leaving the foreground stops the timer.
        await scheduler.FlushAsync();
        Assert.False(scheduler.IsForeground);
        await Task.Delay(100);
        _engine.Runs.Clear();
        await Task.Delay(200);
        Assert.Empty(_engine.Runs);

        // The default interval is a minute.
        Assert.Equal(TimeSpan.FromSeconds(60), SyncScheduler.RetryInterval);
    }

    [Fact]
    public async Task The_retry_waits_while_there_is_no_connection()
    {
        var network = new FakeNetwork { IsConnected = false };
        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.WaitingForNetwork);
        using var scheduler = new SyncScheduler(_engine, new DataChangeNotifier(), new Clock(), connectivity: network);
        await scheduler.StartAsync();
        _engine.Runs.Clear();

        await scheduler.RetryTickAsync();
        Assert.Empty(_engine.Runs);

        network.IsConnected = true;
        await scheduler.RetryTickAsync();
        Assert.Equal([SyncTrigger.Retry], _engine.Runs);
    }

    // ---- C11: events from another thread ----

    [Fact]
    public async Task Engine_events_raised_on_another_thread_reach_the_view_model_through_the_dispatcher()
    {
        var dispatcher = new InlineDispatcher();
        _engine.IsEnabled = true;
        var vm = new SyncSettingsViewModel(
            _account, new NoStorage(), new FakeLocalizer(new FakeLanguageService()), _dialogs, new FakeErrorLog(), _engine, _backups, null, dispatcher);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _engine.Status = new SyncStatus(SyncStateKind.WaitingForNetwork);
        var raiser = Environment.CurrentManagedThreadId;
        await Task.Run(() => _engine.Raise());

        Assert.NotEmpty(dispatcher.Threads);
        Assert.Contains(nameof(SyncSettingsViewModel.SyncRunText), changed);
        Assert.Equal("SyncRunWaiting [en]", vm.SyncRunText);
        Assert.NotEqual(raiser, dispatcher.Threads[0]);
    }

    [Fact]
    public async Task A_left_page_no_longer_listens_to_the_singleton_engine()
    {
        var dispatcher = new InlineDispatcher();
        _engine.IsEnabled = true;
        var vm = new SyncSettingsViewModel(
            _account, new NoStorage(), new FakeLocalizer(new FakeLanguageService()), _dialogs, new FakeErrorLog(), _engine, _backups, null, dispatcher);

        await vm.OnDisappearingAsync();
        _engine.Status = new SyncStatus(SyncStateKind.WaitingForNetwork);
        _engine.Raise();
        Assert.Empty(dispatcher.Threads);

        await vm.OnAppearingAsync();
        dispatcher.Threads.Clear();
        _engine.Raise();
        Assert.NotEmpty(dispatcher.Threads);

        vm.Dispose();
        dispatcher.Threads.Clear();
        _engine.Raise();
        Assert.Empty(dispatcher.Threads);
    }

    // ---- C14 / C9 questions ----

    [Fact]
    public async Task Switching_on_again_on_the_same_account_resumes_without_a_question_or_a_backup()
    {
        _engine.Info = new FirstJoinInfo(true, true, CanResume: true);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.Resume], _engine.Enabled);
        Assert.Empty(_dialogs.Confirmations);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task After_a_restore_switching_on_offers_both_directions()
    {
        _engine.Info = new FirstJoinInfo(true, true, NeedsRejoin: true);
        Pick(_ => 1);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal(["SyncJoinReplace [en]", "SyncJoinReplaceCloud [en]"], _shownDialog!.Items.Select(i => i.Text));
        Assert.Equal("SyncRejoinTitle [en]", _shownDialog.Title);
        Assert.Equal("SyncRejoinQuestion [en]", _shownDialog.Message);
        Assert.Equal([FirstJoinChoice.ReplaceCloud], _engine.Enabled);
        Assert.Empty(_backups.Backups);
    }

    [Fact]
    public async Task After_a_restore_replacing_this_device_makes_a_backup_first()
    {
        _engine.Info = new FirstJoinInfo(true, true, NeedsRejoin: true);
        Pick(_ => 0);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
        Assert.Single(_backups.Backups);
    }

    [Fact]
    public async Task A_replaced_cloud_asks_to_replace_this_device_with_a_backup_first_and_declining_changes_nothing()
    {
        _engine.Info = new FirstJoinInfo(true, true, CloudReplaced: true);
        Pick(_ => 1);
        var vm = Vm();
        vm.SyncOn = true;
        await SettleAsync(vm);

        Assert.Equal(1, _dialogs.EditorsShown);
        Assert.Equal("SyncCloudReplacedQuestion [en]", _shownDialog!.Message);
        Assert.Empty(_engine.Enabled);
        Assert.False(vm.SyncOn);

        Pick(_ => 0);
        vm.SyncOn = true;
        await SettleAsync(vm);
        Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
        Assert.Single(_backups.Backups);
    }

    // ---- C13 messages ----

    private (SyncNotifier Notifier, FakeSnackbarService Snackbar, SyncSettings Settings) Messages(IDialogService? dialogs = null)
    {
        var snackbar = new FakeSnackbarService();
        var settings = new SyncSettings(new FakeSettingsStore());
        var notifier = new SyncNotifier(_engine, snackbar, new FakeLocalizer(new FakeLanguageService()), settings, null, dialogs, _backups, new FakeErrorLog());
        return (notifier, snackbar, settings);
    }

    private void Raise(SyncStateKind kind, SyncTrigger trigger, int received = 0, int sent = 0, SyncErrorKind? error = null)
    {
        _engine.Status = new SyncStatus(kind, Error: error, Trigger: trigger, Received: received, Sent: sent);
        _engine.Raise();
    }

    [Fact]
    public void Syncing_is_announced_only_for_sync_now_and_the_first_sync_after_the_start()
    {
        var (notifier, snackbar, _) = Messages();
        using (notifier)
        {
            Raise(SyncStateKind.Syncing, SyncTrigger.Start);
            Raise(SyncStateKind.Synced, SyncTrigger.Start);
            Raise(SyncStateKind.Syncing, SyncTrigger.Debounced);
            Raise(SyncStateKind.Synced, SyncTrigger.Debounced);
            Raise(SyncStateKind.Syncing, SyncTrigger.Resume);
            Raise(SyncStateKind.Synced, SyncTrigger.Resume);
            Raise(SyncStateKind.Syncing, SyncTrigger.Manual);
            Raise(SyncStateKind.Synced, SyncTrigger.Manual);

            Assert.Equal(["SyncMsgSyncing [en]", "SyncMsgSyncing [en]"], snackbar.Messages);
            Assert.All(snackbar.Calls, c => Assert.Equal(SyncNotifier.MessageDuration, c.Duration));
        }
    }

    [Fact]
    public void Synced_is_shown_only_when_something_was_received_or_sent()
    {
        var (notifier, snackbar, _) = Messages();
        using (notifier)
        {
            Raise(SyncStateKind.Synced, SyncTrigger.Debounced);
            Assert.Empty(snackbar.Messages);

            Raise(SyncStateKind.Synced, SyncTrigger.Retry, received: 0, sent: 1);
            Raise(SyncStateKind.Synced, SyncTrigger.Debounced, received: 3, sent: 0);

            Assert.Equal(["SyncMsgSynced 0|1 [en]", "SyncMsgSynced 3|0 [en]"], snackbar.Messages);
            Assert.InRange(SyncNotifier.MessageDuration.TotalSeconds, 1, 2);
        }
    }

    [Fact]
    public void An_error_is_shown_once_per_kind_until_the_next_success()
    {
        var (notifier, snackbar, _) = Messages();
        using (notifier)
        {
            Raise(SyncStateKind.WaitingForNetwork, SyncTrigger.Retry, error: SyncErrorKind.NoNetwork);
            Raise(SyncStateKind.WaitingForNetwork, SyncTrigger.Retry, error: SyncErrorKind.NoNetwork);
            Raise(SyncStateKind.WaitingForNetwork, SyncTrigger.Retry, error: SyncErrorKind.NoNetwork);
            Assert.Equal(["SyncMsgWaiting [en]"], snackbar.Messages);

            Raise(SyncStateKind.Failed, SyncTrigger.Retry, error: SyncErrorKind.TokenRevoked);
            Raise(SyncStateKind.Failed, SyncTrigger.Retry, error: SyncErrorKind.TokenRevoked);
            Assert.Equal(2, snackbar.Messages.Count);
            Assert.Equal("SyncMsgFailed SyncError_TokenRevoked [en] [en]", snackbar.Messages[1]);

            Raise(SyncStateKind.Synced, SyncTrigger.Retry);
            Raise(SyncStateKind.WaitingForNetwork, SyncTrigger.Retry, error: SyncErrorKind.NoNetwork);
            Assert.Equal(3, snackbar.Messages.Count);
        }
    }

    [Fact]
    public void The_switch_turns_all_messages_off_and_the_setting_is_kept()
    {
        var (notifier, snackbar, settings) = Messages();
        using (notifier)
        {
            Assert.True(settings.ShowMessages);
            settings.ShowMessages = false;
            Raise(SyncStateKind.Syncing, SyncTrigger.Manual);
            Raise(SyncStateKind.Failed, SyncTrigger.Manual, error: SyncErrorKind.Other);
            Assert.Empty(snackbar.Messages);
            Assert.False(settings.DownloadOnMobileData);
        }
    }

    [Fact]
    public async Task A_replaced_cloud_is_asked_once_per_episode_after_a_sync()
    {
        var (notifier, _, _) = Messages(_dialogs);
        using (notifier)
        {
            Pick(_ => 1);
            Raise(SyncStateKind.CloudReplaced, SyncTrigger.Debounced);
            Raise(SyncStateKind.CloudReplaced, SyncTrigger.Retry);
            await Task.Delay(50);
            Assert.Equal(1, _dialogs.EditorsShown);

            Pick(_ => 0);
            Raise(SyncStateKind.Synced, SyncTrigger.Manual);
            Raise(SyncStateKind.CloudReplaced, SyncTrigger.Debounced);
            await WaitForAsync(() => _engine.Enabled.Count > 0);
            Assert.Equal(2, _dialogs.EditorsShown);
            Assert.Equal([FirstJoinChoice.Replace], _engine.Enabled);
            Assert.Single(_backups.Backups);
        }
    }

    // ---- C12 ----

    [Fact]
    public async Task The_cloud_file_list_is_read_only_text_with_a_total()
    {
        _engine.IsEnabled = true;
        _engine.Status = new SyncStatus(SyncStateKind.Synced);
        _engine.Report = new CloudFilesReport(
            [new CloudDeviceFiles("aaaaaaaabbbbbbbbccccccccdddddddd", true, 3, 1500, null), new CloudDeviceFiles("11112222333344445555666677778888", false, 2, 3 * 1024 * 1024, null)],
            [new CloudSnapshotFile("aaaaaaaabbbbbbbbccccccccdddddddd", new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero), 2048, 0)],
            4,
            4096,
            1,
            10);
        var vm = Vm();
        Assert.False(vm.HasCloudFiles);

        await vm.LoadCloudFilesCommand.ExecuteAsync(null);

        var lines = vm.CloudFilesText.Split(Environment.NewLine);
        Assert.Equal(6, lines.Length);
        Assert.StartsWith("SyncCloudDeviceThis aaaaaaaa|3|1.5 KB|", lines[0]);
        Assert.StartsWith("SyncCloudDevice 11112222|2|3 MB|", lines[1]);
        Assert.StartsWith("SyncCloudSnapshot ", lines[2]);
        Assert.StartsWith("SyncCloudAttachments 4|4 KB", lines[3]);
        Assert.StartsWith("SyncCloudForeign 1|10 B", lines[4]);
        Assert.StartsWith("SyncCloudTotal 11|", lines[5]);
        Assert.True(vm.HasCloudFiles);
        Assert.Equal(1500 + (3 * 1024 * 1024) + 2048 + 4096 + 10, _engine.Report.TotalBytes);
    }

    [Fact]
    public void The_options_are_written_to_the_settings()
    {
        var store = new FakeSettingsStore();
        var settings = new SyncSettings(store);
        var vm = new SyncSettingsViewModel(
            _account, new NoStorage(), new FakeLocalizer(new FakeLanguageService()), _dialogs, new FakeErrorLog(), _engine, _backups, settings);

        vm.ShowMessages = false;
        vm.DownloadOnMobile = true;

        Assert.False(settings.ShowMessages);
        Assert.True(settings.DownloadOnMobileData);
    }
}
