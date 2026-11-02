using System.Text.Json;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Google;

/// <summary>
/// Google sign-in for Windows (unpackaged desktop app): authorization code flow with PKCE and a loopback redirect, the
/// sign-in page opens in the system browser. The refresh token is kept in the <see cref="ISecretStore"/>, access tokens
/// only in memory. Scopes: <c>drive.appdata openid email</c>, nothing else.
/// </summary>
public sealed class GoogleDesktopAccountService : ISyncAccountService
{
    public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string Scopes = GoogleOAuthOptions.DriveAppDataScope + " openid email";
    public const string SecretKey = "google.desktop";

    /// <summary>The user has this long to finish the sign-in in the browser; then the listener is stopped.</summary>
    public static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    private readonly GoogleOAuthOptions _options;
    private readonly GoogleTokenClient _tokens;
    private readonly ISecretStore _secrets;
    private readonly IUrlOpener _browser;
    private readonly TimeProvider _time;
    private readonly Func<LoopbackPage> _page;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private AccessToken? _access;

    public GoogleDesktopAccountService(
        GoogleOAuthOptions options,
        GoogleTokenClient tokens,
        ISecretStore secrets,
        IUrlOpener browser,
        TimeProvider time,
        Func<LoopbackPage> page)
    {
        _options = options;
        _tokens = tokens;
        _secrets = secrets;
        _browser = browser;
        _time = time;
        _page = page;
        State = options.HasDesktopClient ? SyncAccountState.SignedOut : SyncAccountState.NotConfigured;
    }

    public string ProviderName => "Google Drive";

    public SyncAccountState State { get; private set; }

    public event EventHandler? StateChanged;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (!_options.HasDesktopClient)
        {
            SetState(SyncAccountState.NotConfigured);
            return;
        }

        // A failed state (revoked) stays until the user signs in again or out.
        if (State.Status == SyncAccountStatus.Error)
        {
            return;
        }

        var stored = await ReadStoredAsync();
        SetState(stored is null ? SyncAccountState.SignedOut : SyncAccountState.SignedIn(stored.Email));
    }

    public async Task SignInAsync(CancellationToken ct = default)
    {
        if (!_options.HasDesktopClient)
        {
            throw new SyncException(SyncErrorKind.NotConfigured, "Sync is not configured in this build.");
        }

        using var server = LoopbackServer.Start();
        var verifier = Pkce.CreateVerifier();
        var state = Pkce.CreateState();
        var url = BuildAuthorizationUrl(server.RedirectUri, state, Pkce.CreateChallenge(verifier));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SignInTimeout);
        TokenGrant grant;
        try
        {
            if (!await _browser.OpenAsync(url))
            {
                throw new SyncException(SyncErrorKind.Other, "The system browser could not be opened.");
            }

            var redirect = await server.WaitForRedirectAsync(_page(), timeout.Token);
            var code = LoopbackRedirect.ParseCode(redirect, state);
            grant = await _tokens.ExchangeCodeAsync(_options, code, verifier, server.RedirectUri, timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new SyncException(SyncErrorKind.Cancelled, "The sign-in was cancelled.");
        }
        catch (OperationCanceledException)
        {
            throw new SyncException(SyncErrorKind.Timeout, "The sign-in was not finished in time.");
        }

        var email = GoogleTokenClient.EmailFromIdToken(grant.IdToken)
            ?? await _tokens.GetEmailAsync(grant.Access.Value, ct);
        await _secrets.SetAsync(SecretKey, JsonSerializer.Serialize(new Stored(grant.RefreshToken, email)));
        _access = grant.Access;
        SetState(SyncAccountState.SignedIn(email));
    }

    public async Task<SignOutResult> SignOutAsync(CancellationToken ct = default)
    {
        var stored = await ReadStoredAsync();
        _access = null;
        _secrets.Remove(SecretKey);
        SetState(_options.HasDesktopClient ? SyncAccountState.SignedOut : SyncAccountState.NotConfigured);

        if (stored is null)
        {
            return SignOutResult.Done;
        }

        return await _tokens.RevokeAsync(stored.RefreshToken, ct) ? SignOutResult.Done : SignOutResult.RevokeFailed;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_access is { } cached && cached.IsValidAt(_time.GetUtcNow()))
            {
                return cached.Value;
            }

            var stored = await ReadStoredAsync()
                ?? throw new SyncException(SyncErrorKind.TokenRevoked, "Not signed in.");
            try
            {
                _access = await _tokens.RefreshAsync(_options, stored.RefreshToken, ct);
            }
            catch (SyncException ex) when (ex.Kind is SyncErrorKind.TokenRevoked)
            {
                // The access was removed in the Google account (or expired): the stored token is useless.
                _access = null;
                _secrets.Remove(SecretKey);
                SetState(SyncAccountState.Failed(SyncErrorKind.TokenRevoked, stored.Email));
                throw;
            }

            return _access.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal Uri BuildAuthorizationUrl(string redirectUri, string state, string challenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.DesktopClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            // offline + consent: Google returns a refresh token on every sign-in, not only the first.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
        };
        return new Uri(AuthorizationEndpoint + "?" + string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}")));
    }

    private async Task<Stored?> ReadStoredAsync()
    {
        var json = await _secrets.GetAsync(SecretKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(json);
            return stored is { RefreshToken.Length: > 0 } ? stored : null;
        }
        catch (JsonException)
        {
            return null;
        }
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

    private sealed record Stored(string RefreshToken, string Email);
}
