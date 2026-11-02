using System.Net;
using System.Text.Json;
using System.Web;
using HomeUtilities.Core.Sync;
using HomeUtilities.Sync.Google;

namespace HomeUtilities.Sync.Tests;

public class DesktopAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly GoogleOAuthOptions Options = new("client-id", "client-secret", string.Empty);

    /// <summary>Plays the browser: opens the authorization URL by calling the loopback redirect with the given query.</summary>
    private sealed class FakeBrowser : IUrlOpener
    {
        public Uri? Opened { get; private set; }

        public Func<Uri, string> Answer { get; set; } = url => "code=the-code&state=" + HttpUtility.ParseQueryString(url.Query)["state"];

        public bool Result { get; set; } = true;

        public Task<bool> OpenAsync(Uri url)
        {
            Opened = url;
            if (Result)
            {
                var redirect = HttpUtility.ParseQueryString(url.Query)["redirect_uri"]!;
                _ = Task.Run(async () =>
                {
                    using var http = new HttpClient();
                    using var response = await http.GetAsync(redirect + "?" + Answer(url));
                });
            }

            return Task.FromResult(Result);
        }
    }

    private static (GoogleDesktopAccountService Service, FakeHandler Handler, MemorySecretStore Secrets, FakeBrowser Browser, FixedTime Time) Create(GoogleOAuthOptions? options = null)
    {
        var handler = new FakeHandler();
        var time = new FixedTime(Now);
        var secrets = new MemorySecretStore();
        var browser = new FakeBrowser();
        var service = new GoogleDesktopAccountService(
            options ?? Options, new GoogleTokenClient(new HttpClient(handler), time), secrets, browser, time, () => new LoopbackPage("Done", "Back to the app"));
        return (service, handler, secrets, browser, time);
    }

    [Fact]
    public async Task Without_client_ids_the_account_is_not_configured_and_sign_in_is_refused()
    {
        var (service, _, _, _, _) = Create(GoogleOAuthOptions.Empty);
        await service.InitializeAsync();
        Assert.Equal(SyncAccountStatus.NotConfigured, service.State.Status);
        var ex = await Assert.ThrowsAsync<SyncException>(() => service.SignInAsync());
        Assert.Equal(SyncErrorKind.NotConfigured, ex.Kind);
    }

    [Fact]
    public async Task Sign_in_opens_the_browser_with_pkce_and_minimal_scopes_then_stores_the_refresh_token()
    {
        var (service, handler, secrets, browser, _) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());

        await service.SignInAsync();

        var query = HttpUtility.ParseQueryString(browser.Opened!.Query);
        Assert.StartsWith(GoogleDesktopAccountService.AuthorizationEndpoint, browser.Opened.ToString());
        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("https://www.googleapis.com/auth/drive.appdata openid email", query["scope"]);
        Assert.StartsWith("http://127.0.0.1:", query["redirect_uri"]);

        // The verifier sent to the token endpoint belongs to the challenge in the URL.
        var form = HttpUtility.ParseQueryString(handler.Requests[0].Body);
        Assert.Equal("the-code", form["code"]);
        Assert.Equal(query["code_challenge"], Pkce.CreateChallenge(form["code_verifier"]!));
        Assert.Equal(query["redirect_uri"], form["redirect_uri"]);

        Assert.Equal(SyncAccountState.SignedIn("anna@example.test"), service.State);
        using var stored = JsonDocument.Parse(secrets.Values[GoogleDesktopAccountService.SecretKey]);
        Assert.Equal("refresh-1", stored.RootElement.GetProperty("RefreshToken").GetString());
    }

    [Fact]
    public async Task Sign_in_falls_back_to_userinfo_when_the_token_answer_has_no_id_token()
    {
        var (service, handler, _, _, _) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer(email: null)).Enqueue(HttpStatusCode.OK, """{"email":"bob@example.test"}""");
        await service.SignInAsync();
        Assert.Equal("bob@example.test", service.State.Email);
    }

    [Fact]
    public async Task Denied_consent_is_reported_and_nothing_is_stored()
    {
        var (service, _, secrets, browser, _) = Create();
        browser.Answer = url => "error=access_denied&state=" + HttpUtility.ParseQueryString(url.Query)["state"];

        var ex = await Assert.ThrowsAsync<SyncException>(() => service.SignInAsync());

        Assert.Equal(SyncErrorKind.ConsentDenied, ex.Kind);
        Assert.Empty(secrets.Values);
        Assert.Equal(SyncAccountStatus.SignedOut, service.State.Status);
    }

    [Fact]
    public async Task Cancelling_the_sign_in_is_reported_as_Cancelled()
    {
        var (service, _, _, browser, _) = Create();
        browser.Result = true;
        browser.Answer = _ => "state=unused"; // never reached: cancelled first
        using var cts = new CancellationTokenSource();
        // The fake browser answers at once, so cancel before it can.
        await cts.CancelAsync();
        var ex = await Assert.ThrowsAsync<SyncException>(() => service.SignInAsync(cts.Token));
        Assert.Equal(SyncErrorKind.Cancelled, ex.Kind);
    }

    [Fact]
    public async Task A_browser_that_cannot_be_opened_is_an_error()
    {
        var (service, _, _, browser, _) = Create();
        browser.Result = false;
        var ex = await Assert.ThrowsAsync<SyncException>(() => service.SignInAsync());
        Assert.Equal(SyncErrorKind.Other, ex.Kind);
    }

    [Fact]
    public async Task The_stored_sign_in_survives_a_restart_without_network()
    {
        var (first, handler, secrets, _, _) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());
        await first.SignInAsync();

        var second = new GoogleDesktopAccountService(
            Options, new GoogleTokenClient(new HttpClient(new FakeHandler()), new FixedTime(Now)), secrets, new FakeBrowser(), new FixedTime(Now),
            () => new LoopbackPage("t", "m"));
        await second.InitializeAsync();

        Assert.Equal(SyncAccountState.SignedIn("anna@example.test"), second.State);
    }

    [Fact]
    public async Task Access_token_is_cached_until_shortly_before_it_expires_then_refreshed()
    {
        var (service, handler, _, _, time) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer(expiresIn: 3600));
        await service.SignInAsync();

        Assert.Equal("access-1", await service.GetAccessTokenAsync());
        Assert.Single(handler.Requests); // no refresh call

        time.Now = Now.AddMinutes(30);
        Assert.Equal("access-1", await service.GetAccessTokenAsync());

        time.Now = Now.AddMinutes(59.5); // inside the one-minute safety margin
        handler.Enqueue(HttpStatusCode.OK, """{"access_token":"access-2","expires_in":3600}""");
        Assert.Equal("access-2", await service.GetAccessTokenAsync());
        Assert.Contains("grant_type=refresh_token", handler.Requests[1].Body);
    }

    [Fact]
    public async Task A_revoked_token_clears_the_stored_sign_in_and_puts_the_account_in_error()
    {
        var (service, handler, secrets, _, time) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());
        await service.SignInAsync();
        time.Now = Now.AddHours(2);
        handler.Enqueue(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""", time.Now);

        var ex = await Assert.ThrowsAsync<SyncException>(() => service.GetAccessTokenAsync());

        Assert.Equal(SyncErrorKind.TokenRevoked, ex.Kind);
        Assert.Equal(SyncAccountStatus.Error, service.State.Status);
        Assert.Equal(SyncErrorKind.TokenRevoked, service.State.Error);
        Assert.Empty(secrets.Values);
    }

    [Fact]
    public async Task Getting_a_token_when_signed_out_fails()
    {
        var (service, _, _, _, _) = Create();
        var ex = await Assert.ThrowsAsync<SyncException>(() => service.GetAccessTokenAsync());
        Assert.Equal(SyncErrorKind.TokenRevoked, ex.Kind);
    }

    [Fact]
    public async Task Sign_out_deletes_the_secrets_and_revokes_the_refresh_token()
    {
        var (service, handler, secrets, _, _) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());
        await service.SignInAsync();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        Assert.Equal(SignOutResult.Done, await service.SignOutAsync());

        Assert.Empty(secrets.Values);
        Assert.Equal(SyncAccountStatus.SignedOut, service.State.Status);
        Assert.Equal(GoogleTokenClient.RevokeEndpoint, handler.Requests[^1].Uri.ToString());
        Assert.Contains("token=refresh-1", handler.Requests[^1].Body);
    }

    [Fact]
    public async Task Sign_out_offline_still_signs_out_locally_and_says_the_revoke_failed()
    {
        var (service, handler, secrets, _, _) = Create();
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());
        await service.SignInAsync();
        handler.EnqueueFailure(new HttpRequestException("offline"));

        Assert.Equal(SignOutResult.RevokeFailed, await service.SignOutAsync());

        Assert.Empty(secrets.Values);
        Assert.Equal(SyncAccountStatus.SignedOut, service.State.Status);
    }

    [Fact]
    public async Task State_changes_raise_the_event()
    {
        var (service, handler, _, _, _) = Create();
        var raised = 0;
        service.StateChanged += (_, _) => raised++;
        handler.Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());
        await service.SignInAsync();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        await service.SignOutAsync();
        Assert.Equal(2, raised);
    }
}
