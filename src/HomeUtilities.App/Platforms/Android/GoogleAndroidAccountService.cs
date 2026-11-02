using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Identity;
using Android.Gms.Common.Apis;
using HomeUtilities.Core.Sync;
using HomeUtilities.Sync.Google;

namespace HomeUtilities.App.Services;

/// <summary>
/// Google sign-in for Android with the Google Identity Services <b>Authorization API</b> (Google Play services): the system
/// shows the account picker and the consent page, no browser and no redirect URI. The OAuth client of the Cloud project is
/// found by package name + signing certificate SHA-1, so no client id is needed in the code. The access token comes from Play
/// services on each call (it caches and refreshes it); the app stores only the e-mail.
/// </summary>
public sealed class GoogleAndroidAccountService : ISyncAccountService
{
    public const string SecretKey = "google.android.email";

    private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(30);

    private readonly GoogleOAuthOptions _options;
    private readonly GoogleTokenClient _tokens;
    private readonly ISecretStore _secrets;
    private readonly TimeProvider _time;

    private AccessToken? _access;

    public GoogleAndroidAccountService(GoogleOAuthOptions options, GoogleTokenClient tokens, ISecretStore secrets, TimeProvider time)
    {
        _options = options;
        _tokens = tokens;
        _secrets = secrets;
        _time = time;
        State = options.HasAndroidClient ? SyncAccountState.SignedOut : SyncAccountState.NotConfigured;
    }

    public string ProviderName => "Google Drive";

    public SyncAccountState State { get; private set; }

    public event EventHandler? StateChanged;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (!_options.HasAndroidClient)
        {
            SetState(SyncAccountState.NotConfigured);
            return;
        }

        if (State.Status == SyncAccountStatus.Error)
        {
            return;
        }

        var email = await _secrets.GetAsync(SecretKey);
        SetState(string.IsNullOrEmpty(email) ? SyncAccountState.SignedOut : SyncAccountState.SignedIn(email));
    }

    public async Task SignInAsync(CancellationToken ct = default)
    {
        if (!_options.HasAndroidClient)
        {
            throw new SyncException(SyncErrorKind.NotConfigured, "Sync is not configured in this build.");
        }

        var token = await AuthorizeAsync(interactive: true, ct);
        string email;
        try
        {
            email = await _tokens.GetEmailAsync(token.Value, ct);
        }
        catch (SyncException ex) when (ex.Kind == SyncErrorKind.TokenRevoked)
        {
            // Play services still hands out the token revoked by an earlier Disconnect (owner, 2026-11-02: reconnecting
            // said "access revoked"). Drop it from the Play services cache and ask again — Google shows the consent anew.
            await ClearCachedTokenAsync(token.Value);
            token = await AuthorizeAsync(interactive: true, ct);
            email = await _tokens.GetEmailAsync(token.Value, ct);
        }

        await _secrets.SetAsync(SecretKey, email);
        _access = token;
        SetState(SyncAccountState.SignedIn(email));
    }

    public async Task<SignOutResult> SignOutAsync(CancellationToken ct = default)
    {
        var wasSignedIn = State.Status is SyncAccountStatus.SignedIn or SyncAccountStatus.Error;
        var cached = _access;
        _access = null;
        _secrets.Remove(SecretKey);
        SetState(_options.HasAndroidClient ? SyncAccountState.SignedOut : SyncAccountState.NotConfigured);
        if (!wasSignedIn)
        {
            return SignOutResult.Done;
        }

        // Revoking the access token removes the whole grant of this app from the Google account.
        try
        {
            var token = cached ?? await AuthorizeAsync(interactive: false, ct);
            var revoked = await _tokens.RevokeAsync(token.Value, ct);

            // Also forget the token in Play services, otherwise the next Connect receives the revoked token again.
            await ClearCachedTokenAsync(token.Value);
            return revoked ? SignOutResult.Done : SignOutResult.RevokeFailed;
        }
        catch (SyncException)
        {
            return SignOutResult.RevokeFailed;
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_access is { } cached && cached.IsValidAt(_time.GetUtcNow()))
        {
            return cached.Value;
        }

        if (State.Status != SyncAccountStatus.SignedIn)
        {
            throw new SyncException(SyncErrorKind.TokenRevoked, "Not signed in.");
        }

        try
        {
            _access = await AuthorizeAsync(interactive: false, ct);
            return _access.Value;
        }
        catch (SyncException ex) when (ex.Kind == SyncErrorKind.TokenRevoked)
        {
            _access = null;
            SetState(SyncAccountState.Failed(SyncErrorKind.TokenRevoked, State.Email));
            throw;
        }
    }

    // AuthorizationClient.clearToken (Play services auth 21.4+) removes the token from the Play services cache. A failure
    // only means the cache keeps the token; the sign-in then reports the revoked access as before, so it is logged.
    private static async System.Threading.Tasks.Task ClearCachedTokenAsync(string token)
    {
        try
        {
            global::Android.Content.Context context = Platform.AppContext;
            var client = Identity.GetAuthorizationClient(context);
            var request = ClearTokenRequest.InvokeBuilder().SetToken(token).Build();
            await AwaitAsync(client.ClearToken(request), CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clearing the cached Google token failed: {ex.Message}");
        }
    }

    private async Task<AccessToken> AuthorizeAsync(bool interactive, CancellationToken ct)
    {
        try
        {
            return await AuthorizeCoreAsync(interactive, ct);
        }
        catch (OperationCanceledException)
        {
            throw new SyncException(SyncErrorKind.Cancelled, "The sign-in was cancelled.");
        }
    }

    private async Task<AccessToken> AuthorizeCoreAsync(bool interactive, CancellationToken ct)
    {
        var activity = Platform.CurrentActivity ?? throw new SyncException(SyncErrorKind.Other, "No foreground activity for the sign-in.");
        var client = Identity.GetAuthorizationClient(activity);
        var request = new AuthorizationRequest.Builder()
            .SetRequestedScopes(
            [
                new Scope(GoogleOAuthOptions.DriveAppDataScope),
                new Scope("openid"),
                new Scope("email"),
            ])
            .Build();

        var result = (AuthorizationResult?)await AwaitAsync(client.Authorize(request), ct)
            ?? throw new SyncException(SyncErrorKind.Other, "Authorization returned nothing.");

        if (result.HasResolution)
        {
            if (!interactive)
            {
                // The consent was withdrawn (Google account settings) or never given on this device.
                throw new SyncException(SyncErrorKind.TokenRevoked, "The access needs a new consent.");
            }

            result = await AndroidAuthBridge.ResolveAsync(activity, result, ct);
        }

        var accessToken = result.AccessToken ?? throw new SyncException(SyncErrorKind.Other, "Authorization returned no access token.");
        return new AccessToken(accessToken, _time.GetUtcNow().AddMinutes(CacheTime.TotalMinutes));
    }

    /// <summary>Awaits a Play services <see cref="global::Android.Gms.Tasks.Task"/> and maps its failure to a <see cref="SyncException"/>.</summary>
    internal static async System.Threading.Tasks.Task<Java.Lang.Object?> AwaitAsync(global::Android.Gms.Tasks.Task task, CancellationToken ct)
    {
        var source = new TaskCompletionSource<Java.Lang.Object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        task.AddOnSuccessListener(new Listener(source));
        task.AddOnFailureListener(new Listener(source));
        using var registration = ct.Register(() => source.TrySetCanceled(ct));
        return await source.Task;
    }

    internal static SyncException Map(Java.Lang.Exception exception)
    {
        if (exception is ApiException api)
        {
            // CommonStatusCodes: 7 NETWORK_ERROR, 8 INTERNAL_ERROR, 10 DEVELOPER_ERROR (package / SHA-1 / consent screen not
            // matching the Cloud project), 16 CANCELED, 12501 SIGN_IN_CANCELLED, 12502 SIGN_IN_CURRENTLY_IN_PROGRESS.
            return api.StatusCode switch
            {
                7 => new SyncException(SyncErrorKind.NoNetwork, "No connection to Google Play services.", exception),
                16 or 12501 => new SyncException(SyncErrorKind.Cancelled, "The sign-in was cancelled.", exception),
                _ => new SyncException(SyncErrorKind.Other, $"Google Play services status {api.StatusCode}: {api.Message}", exception),
            };
        }

        return new SyncException(SyncErrorKind.Other, exception.Message ?? "Authorization failed.", exception);
    }

    private void SetState(SyncAccountState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Listener(TaskCompletionSource<Java.Lang.Object?> source) : Java.Lang.Object, global::Android.Gms.Tasks.IOnSuccessListener, global::Android.Gms.Tasks.IOnFailureListener
    {
        public void OnSuccess(Java.Lang.Object? result) => source.TrySetResult(result);

        public void OnFailure(Java.Lang.Exception exception) => source.TrySetException(Map(exception));
    }
}

/// <summary>
/// Hands the consent page of Play services (a <see cref="PendingIntent"/>) to the activity and receives its answer from
/// <c>MainActivity.OnActivityResult</c>.
/// </summary>
public static class AndroidAuthBridge
{
    public const int RequestCode = 4730;

    private static TaskCompletionSource<Intent?>? _pending;

    public static async System.Threading.Tasks.Task<AuthorizationResult> ResolveAsync(Activity activity, AuthorizationResult result, CancellationToken ct)
    {
        var source = new TaskCompletionSource<Intent?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = source;
        using var registration = ct.Register(() => source.TrySetCanceled(ct));
        try
        {
            activity.StartIntentSenderForResult(result.PendingIntent!.IntentSender, RequestCode, null, 0, 0, 0);
            var data = await source.Task;
            return Identity.GetAuthorizationClient(activity).GetAuthorizationResultFromIntent(data)
                ?? throw new SyncException(SyncErrorKind.Other, "Authorization returned nothing.");
        }
        catch (Java.Lang.Exception ex)
        {
            throw GoogleAndroidAccountService.Map(ex);
        }
        catch (OperationCanceledException)
        {
            throw new SyncException(SyncErrorKind.Cancelled, "The sign-in was cancelled.");
        }
        finally
        {
            _pending = null;
        }
    }

    /// <summary>Called by <c>MainActivity.OnActivityResult</c>; true when the result belongs to the sign-in.</summary>
    public static bool OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode || _pending is not { } pending)
        {
            return false;
        }

        if (resultCode == Result.Ok)
        {
            pending.TrySetResult(data);
        }
        else
        {
            pending.TrySetException(new SyncException(SyncErrorKind.Cancelled, "The sign-in was cancelled."));
        }

        return true;
    }
}
