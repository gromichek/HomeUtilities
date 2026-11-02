using HomeUtilities.Core.Sync;
using HomeUtilities.ViewModels;

namespace HomeUtilities.ViewModels.Tests;

public class SyncSettingsViewModelTests
{
    internal sealed class FakeAccount : ISyncAccountService
    {
        public string ProviderName => "Google Drive";

        public SyncAccountState State { get; set; } = SyncAccountState.SignedOut;

        public event EventHandler? StateChanged;

        public Exception? SignInFailure { get; set; }

        public SignOutResult SignOutResult { get; set; } = SignOutResult.Done;

        public TaskCompletionSource? SignInGate { get; set; }

        public int InitializeCalls { get; private set; }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public async Task SignInAsync(CancellationToken ct = default)
        {
            if (SignInGate is { } gate)
            {
                try
                {
                    await gate.Task.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    throw new SyncException(SyncErrorKind.Cancelled, "cancelled");
                }
            }

            if (SignInFailure is not null)
            {
                throw SignInFailure;
            }

            Set(SyncAccountState.SignedIn("anna@example.test"));
        }

        public Task<SignOutResult> SignOutAsync(CancellationToken ct = default)
        {
            Set(SyncAccountState.SignedOut);
            return Task.FromResult(SignOutResult);
        }

        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("tok");

        public void Set(SyncAccountState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeStorage : ISyncStorage
    {
        public Exception? Failure { get; set; }

        private byte[] _content = [];

        public Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SyncFileInfo>>([new SyncFileInfo(SyncConnectionCheck.TestFilePath, _content.Length, null)]);

        public async Task UploadAsync(string path, Stream content, CancellationToken ct = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            _content = copy.ToArray();
        }

        public Task<Stream> DownloadAsync(string path, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream(_content));

        public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly FakeAccount _account = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeErrorLog _log = new();

    internal static SyncSettingsViewModel Create(FakeLanguageService language) =>
        new(new FakeAccount(), new FakeStorage(), new FakeLocalizer(language), new FakeDialogService(), new FakeErrorLog());

    private SyncSettingsViewModel Vm() =>
        new(_account, _storage, new FakeLocalizer(new FakeLanguageService()), _dialogs, _log);

    [Fact]
    public void A_build_without_client_ids_says_so_and_cannot_connect()
    {
        _account.State = SyncAccountState.NotConfigured;
        var vm = Vm();

        Assert.True(vm.IsNotConfigured);
        Assert.Equal("SyncStatusNotConfigured [en]", vm.StatusText);
        Assert.False(vm.CanConnect);
        Assert.False(vm.CanCheck);
        Assert.False(vm.CanDisconnect);
        Assert.Equal("SettingsValueSyncNotConfigured [en]", vm.SummaryText);
    }

    [Fact]
    public void Signed_out_offers_only_connect()
    {
        var vm = Vm();

        Assert.True(vm.CanConnect);
        Assert.False(vm.CanCheck);
        Assert.False(vm.CanDisconnect);
        Assert.Equal("SyncStatusSignedOut [en]", vm.StatusText);
        Assert.Equal(1, _account.InitializeCalls);
    }

    [Fact]
    public async Task Connect_signs_in_and_shows_the_email_and_the_other_buttons()
    {
        var vm = Vm();

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.True(vm.IsSignedIn);
        Assert.Equal("SyncStatusSignedIn [en]", vm.StatusText);
        Assert.Equal("SyncAccount anna@example.test [en]", vm.AccountText);
        Assert.Equal("anna@example.test", vm.SummaryText);
        Assert.False(vm.CanConnect);
        Assert.True(vm.CanCheck);
        Assert.True(vm.CanDisconnect);
        Assert.False(vm.HasError);
    }

    [Theory]
    [InlineData(SyncErrorKind.ConsentDenied)]
    [InlineData(SyncErrorKind.NoNetwork)]
    [InlineData(SyncErrorKind.Timeout)]
    [InlineData(SyncErrorKind.ClockSkew)]
    public async Task A_failed_sign_in_shows_the_matching_message_and_logs_the_details(SyncErrorKind kind)
    {
        _account.SignInFailure = new SyncException(kind, "details");
        var vm = Vm();

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Equal($"SyncError_{kind} [en]", vm.MessageText);
        Assert.True(vm.HasError);
        Assert.False(vm.IsSignedIn);
        Assert.True(vm.CanConnect);
        Assert.Equal("Sync.SignIn", Assert.Single(_log.Entries).Source);
    }

    [Fact]
    public async Task Cancelling_the_waiting_sign_in_is_no_error()
    {
        _account.SignInGate = new TaskCompletionSource();
        var vm = Vm();

        var connecting = vm.ConnectCommand.ExecuteAsync(null);
        Assert.True(vm.IsConnecting);
        Assert.Equal("SyncSigningIn [en]", vm.StatusText);
        Assert.False(vm.CanConnect);

        vm.CancelSignInCommand.Execute(null);
        await connecting;

        Assert.False(vm.IsConnecting);
        Assert.Equal("SyncError_Cancelled [en]", vm.MessageText);
        Assert.False(vm.HasError);
        Assert.Empty(_log.Entries);
        Assert.True(vm.CanConnect);
    }

    [Fact]
    public async Task Check_connection_reports_success()
    {
        _account.State = SyncAccountState.SignedIn("anna@example.test");
        var vm = Vm();

        await vm.CheckConnectionCommand.ExecuteAsync(null);

        Assert.Equal("SyncCheckOk [en]", vm.MessageText);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Check_connection_names_the_failed_step_and_the_reason()
    {
        _account.State = SyncAccountState.SignedIn("anna@example.test");
        _storage.Failure = new SyncException(SyncErrorKind.ApiDisabled, "disabled");
        var vm = Vm();

        await vm.CheckConnectionCommand.ExecuteAsync(null);

        Assert.Equal("SyncCheckFailed SyncStep_Write [en]|SyncError_ApiDisabled [en] [en]", vm.MessageText);
        Assert.True(vm.HasError);
        Assert.Equal("Sync.Check", Assert.Single(_log.Entries).Source);
    }

    [Fact]
    public async Task Check_connection_without_network_does_not_crash()
    {
        _account.State = SyncAccountState.SignedIn("anna@example.test");
        _storage.Failure = new SyncException(SyncErrorKind.NoNetwork, "offline", new HttpRequestException());
        var vm = Vm();

        await vm.CheckConnectionCommand.ExecuteAsync(null);

        Assert.Contains("SyncError_NoNetwork", vm.MessageText);
    }

    [Fact]
    public async Task Disconnect_asks_first_and_does_nothing_when_declined()
    {
        _account.State = SyncAccountState.SignedIn("anna@example.test");
        _dialogs.ConfirmResult = false;
        var vm = Vm();

        await vm.DisconnectCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirmations);
        Assert.True(vm.IsSignedIn);
    }

    [Fact]
    public async Task Disconnect_signs_out_and_tells_when_the_revoke_could_not_be_sent()
    {
        _account.State = SyncAccountState.SignedIn("anna@example.test");
        var vm = Vm();

        await vm.DisconnectCommand.ExecuteAsync(null);
        Assert.False(vm.IsSignedIn);
        Assert.Equal("SyncDisconnected [en]", vm.MessageText);
        Assert.False(vm.HasError);

        _account.SignOutResult = SignOutResult.RevokeFailed;
        _account.Set(SyncAccountState.SignedIn("anna@example.test"));
        await vm.DisconnectCommand.ExecuteAsync(null);
        Assert.Equal("SyncDisconnectedRevokeFailed [en]", vm.MessageText);
        Assert.True(vm.HasError);
    }

    [Fact]
    public void A_lost_access_shows_the_reason_and_still_allows_connect_and_disconnect()
    {
        _account.State = SyncAccountState.Failed(SyncErrorKind.TokenRevoked, "anna@example.test");
        var vm = Vm();

        Assert.Equal("SyncStatusError [en]", vm.StatusText);
        Assert.Equal("SyncError_TokenRevoked [en]", vm.MessageText);
        Assert.True(vm.HasError);
        Assert.True(vm.CanConnect);
        Assert.True(vm.CanDisconnect);
        Assert.False(vm.CanCheck);
    }

    [Fact]
    public void An_external_state_change_updates_the_texts()
    {
        var vm = Vm();
        _account.Set(SyncAccountState.SignedIn("bob@example.test"));
        Assert.Equal("bob@example.test", vm.SummaryText);
    }
}
