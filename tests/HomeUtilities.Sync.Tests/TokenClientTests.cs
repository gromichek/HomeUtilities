using System.Net;
using HomeUtilities.Core.Sync;
using HomeUtilities.Sync.Google;

namespace HomeUtilities.Sync.Tests;

public class TokenClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly GoogleOAuthOptions Options = new("client-id", "client-secret", string.Empty);

    private static GoogleTokenClient Client(FakeHandler handler) => new(new HttpClient(handler), new FixedTime(Now));

    [Fact]
    public async Task Exchange_posts_the_code_with_the_verifier_and_reads_the_tokens()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer());
        var grant = await Client(handler).ExchangeCodeAsync(Options, "the-code", "the-verifier", "http://127.0.0.1:5000/", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(GoogleTokenClient.TokenEndpoint, request.Uri.ToString());
        Assert.Contains("grant_type=authorization_code", request.Body);
        Assert.Contains("code=the-code", request.Body);
        Assert.Contains("code_verifier=the-verifier", request.Body);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A5000%2F", request.Body);
        Assert.Contains("client_id=client-id", request.Body);
        Assert.Equal("access-1", grant.Access.Value);
        Assert.Equal(Now.AddSeconds(3600), grant.Access.ExpiresAt);
        Assert.Equal("refresh-1", grant.RefreshToken);
        Assert.Equal("anna@example.test", GoogleTokenClient.EmailFromIdToken(grant.IdToken));
    }

    [Fact]
    public async Task Exchange_without_a_refresh_token_fails()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.OK, Tokens.TokenAnswer(refresh: null));
        var ex = await Assert.ThrowsAsync<SyncException>(() =>
            Client(handler).ExchangeCodeAsync(Options, "c", "v", "http://127.0.0.1:5000/", CancellationToken.None));
        Assert.Equal(SyncErrorKind.Other, ex.Kind);
    }

    [Fact]
    public async Task Refresh_posts_the_refresh_token_and_returns_a_new_access_token()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.OK, """{"access_token":"access-2","expires_in":1800}""");
        var token = await Client(handler).RefreshAsync(Options, "refresh-1", CancellationToken.None);

        Assert.Contains("grant_type=refresh_token", handler.Requests[0].Body);
        Assert.Contains("refresh_token=refresh-1", handler.Requests[0].Body);
        Assert.Equal("access-2", token.Value);
        Assert.Equal(Now.AddSeconds(1800), token.ExpiresAt);
    }

    [Fact]
    public async Task Refresh_with_invalid_grant_means_the_access_was_revoked()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Token has been expired or revoked."}""", Now);
        var ex = await Assert.ThrowsAsync<SyncException>(() => Client(handler).RefreshAsync(Options, "r", CancellationToken.None));
        Assert.Equal(SyncErrorKind.TokenRevoked, ex.Kind);
    }

    [Fact]
    public async Task Invalid_grant_with_a_clock_far_off_is_reported_as_clock_skew()
    {
        // Google's Date header says 12:00, this device thinks it is 12:20.
        var handler = new FakeHandler().Enqueue(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""", Now.AddMinutes(-20));
        var ex = await Assert.ThrowsAsync<SyncException>(() => Client(handler).RefreshAsync(Options, "r", CancellationToken.None));
        Assert.Equal(SyncErrorKind.ClockSkew, ex.Kind);
    }

    [Fact]
    public async Task No_network_is_reported_as_NoNetwork()
    {
        var handler = new FakeHandler().EnqueueFailure(new HttpRequestException("offline"));
        var ex = await Assert.ThrowsAsync<SyncException>(() => Client(handler).RefreshAsync(Options, "r", CancellationToken.None));
        Assert.Equal(SyncErrorKind.NoNetwork, ex.Kind);
    }

    [Fact]
    public async Task Revoke_succeeds_for_ok_and_for_an_already_invalid_token_and_fails_offline()
    {
        var ok = new FakeHandler().Enqueue(HttpStatusCode.OK, "{}");
        Assert.True(await Client(ok).RevokeAsync("refresh-1", CancellationToken.None));
        Assert.Contains("token=refresh-1", ok.Requests[0].Body);
        Assert.Equal(GoogleTokenClient.RevokeEndpoint, ok.Requests[0].Uri.ToString());

        var invalid = new FakeHandler().Enqueue(HttpStatusCode.BadRequest, """{"error":"invalid_token"}""");
        Assert.True(await Client(invalid).RevokeAsync("x", CancellationToken.None));

        var offline = new FakeHandler().EnqueueFailure(new HttpRequestException());
        Assert.False(await Client(offline).RevokeAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task UserInfo_returns_the_email_with_a_bearer_token()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.OK, """{"sub":"1","email":"anna@example.test"}""");
        Assert.Equal("anna@example.test", await Client(handler).GetEmailAsync("tok", CancellationToken.None));
        Assert.Equal("Bearer tok", handler.Requests[0].Authorization);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a.!!!.c")]
    public void EmailFromIdToken_is_null_for_unusable_tokens(string? token)
    {
        Assert.Null(GoogleTokenClient.EmailFromIdToken(token));
    }

    [Fact]
    public void Drive_api_disabled_in_the_project_is_recognised()
    {
        const string body = """{"error":{"code":403,"message":"Google Drive API has not been used","errors":[{"reason":"accessNotConfigured"}],"status":"PERMISSION_DENIED"}}""";
        Assert.Equal(SyncErrorKind.ApiDisabled, GoogleHttp.Map(HttpStatusCode.Forbidden, Now, body, Now).Kind);

        const string details = """{"error":{"code":403,"message":"disabled","status":"PERMISSION_DENIED","details":[{"reason":"SERVICE_DISABLED"}]}}""";
        Assert.Equal(SyncErrorKind.ApiDisabled, GoogleHttp.Map(HttpStatusCode.Forbidden, Now, details, Now).Kind);
    }

    [Fact]
    public void Other_http_errors_are_mapped_without_tokens_in_the_message()
    {
        Assert.Equal(SyncErrorKind.TokenRevoked, GoogleHttp.Map(HttpStatusCode.Unauthorized, Now, "{}", Now).Kind);
        Assert.Equal(SyncErrorKind.NotFound, GoogleHttp.Map(HttpStatusCode.NotFound, Now, "{}", Now).Kind);
        var other = GoogleHttp.Map(HttpStatusCode.InternalServerError, Now, "not json", Now);
        Assert.Equal(SyncErrorKind.Other, other.Kind);
        Assert.Contains("500", other.Message);
    }
}
