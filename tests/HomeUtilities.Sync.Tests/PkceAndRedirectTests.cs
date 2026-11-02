using HomeUtilities.Core.Sync;
using HomeUtilities.Sync.Google;

namespace HomeUtilities.Sync.Tests;

public class PkceAndRedirectTests
{
    [Fact]
    public void Challenge_matches_the_RFC7636_test_vector()
    {
        // RFC 7636 appendix B.
        Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.CreateChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void Verifier_is_43_url_safe_characters_and_different_each_time()
    {
        var a = Pkce.CreateVerifier();
        var b = Pkce.CreateVerifier();
        Assert.Equal(43, a.Length);
        Assert.All(a, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ParseCode_returns_the_code_when_the_state_matches()
    {
        Assert.Equal("4/0Abc", LoopbackRedirect.ParseCode(new Uri("http://127.0.0.1:5000/?state=s1&code=4%2F0Abc&scope=x"), "s1"));
    }

    [Fact]
    public void ParseCode_rejects_a_wrong_state()
    {
        var ex = Assert.Throws<SyncException>(() => LoopbackRedirect.ParseCode(new Uri("http://127.0.0.1:5000/?state=evil&code=c"), "s1"));
        Assert.Equal(SyncErrorKind.Other, ex.Kind);
    }

    [Fact]
    public void ParseCode_rejects_a_missing_state()
    {
        Assert.Throws<SyncException>(() => LoopbackRedirect.ParseCode(new Uri("http://127.0.0.1:5000/?code=c"), "s1"));
    }

    [Fact]
    public void ParseCode_maps_access_denied_to_ConsentDenied()
    {
        var ex = Assert.Throws<SyncException>(() => LoopbackRedirect.ParseCode(new Uri("http://127.0.0.1:5000/?state=s1&error=access_denied"), "s1"));
        Assert.Equal(SyncErrorKind.ConsentDenied, ex.Kind);
    }

    [Fact]
    public void ParseCode_maps_other_errors_and_a_missing_code_to_Other()
    {
        Assert.Equal(SyncErrorKind.Other,
            Assert.Throws<SyncException>(() => LoopbackRedirect.ParseCode(new Uri("http://127.0.0.1:5000/?state=s1&error=server_error"), "s1")).Kind);
        Assert.Equal(SyncErrorKind.Other,
            Assert.Throws<SyncException>(() => LoopbackRedirect.ParseCode(new Uri("http://127.0.0.1:5000/?state=s1"), "s1")).Kind);
    }

    [Fact]
    public void Options_are_read_from_secrets_json_and_empty_when_missing_or_broken()
    {
        var options = GoogleOAuthOptions.Parse("""{ "googleDesktopClientId": " id ", "googleDesktopClientSecret": "secret", "googleAndroidClientId": "" }""");
        Assert.Equal("id", options.DesktopClientId);
        Assert.True(options.HasDesktopClient);
        Assert.False(options.HasAndroidClient);

        Assert.False(GoogleOAuthOptions.Parse(null).HasDesktopClient);
        Assert.False(GoogleOAuthOptions.Parse("not json").HasDesktopClient);
        Assert.False(GoogleOAuthOptions.Parse("""{ "googleDesktopClientId": "id" }""").HasDesktopClient);
        Assert.False(GoogleOAuthOptions.Parse("[]").HasDesktopClient);
    }

    [Fact]
    public async Task Loopback_server_returns_the_answer_and_ignores_the_favicon_request()
    {
        using var server = LoopbackServer.Start();
        using var http = new HttpClient();
        var wait = server.WaitForRedirectAsync(new LoopbackPage("Done", "Close <this> window"), CancellationToken.None);

        using var favicon = await http.GetAsync(server.RedirectUri + "favicon.ico");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, favicon.StatusCode);
        using var answer = await http.GetAsync(server.RedirectUri + "?state=s&code=c");
        var html = await answer.Content.ReadAsStringAsync();

        var uri = await wait.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("c", LoopbackRedirect.ParseCode(uri, "s"));
        Assert.Contains("Close &lt;this&gt; window", html);
    }

    [Fact]
    public async Task Cancelling_the_wait_stops_the_listener()
    {
        string redirectUri;
        using (var server = LoopbackServer.Start())
        {
            redirectUri = server.RedirectUri;
            using var cts = new CancellationTokenSource();
            var wait = server.WaitForRedirectAsync(new LoopbackPage("t", "m"), cts.Token);
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        using var http = new HttpClient();
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(redirectUri));
    }
}
