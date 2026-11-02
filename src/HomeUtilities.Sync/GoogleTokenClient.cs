using System.Text;
using System.Text.Json;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Google;

/// <summary>An access token with the time it expires; held in memory only.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt)
{
    /// <summary>Refresh a minute early so a call does not start with a token that expires on the way.</summary>
    public bool IsValidAt(DateTimeOffset now) => now < ExpiresAt - TimeSpan.FromMinutes(1);
}

/// <summary>The answer of the token endpoint to an authorization code.</summary>
public sealed record TokenGrant(AccessToken Access, string RefreshToken, string? IdToken);

/// <summary>Calls of the Google OAuth endpoints (token, revoke, userinfo) over <see cref="HttpClient"/>.</summary>
public sealed class GoogleTokenClient(HttpClient http, TimeProvider time)
{
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    public const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";

    /// <summary>Exchanges the authorization code (with the PKCE verifier) for tokens.</summary>
    public async Task<TokenGrant> ExchangeCodeAsync(
        GoogleOAuthOptions options, string code, string verifier, string redirectUri, CancellationToken ct)
    {
        var response = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = options.DesktopClientId,
            ["client_secret"] = options.DesktopClientSecret,
        }, ct);

        var refresh = response.RefreshToken
            ?? throw new SyncException(SyncErrorKind.Other, "Google returned no refresh token.");
        return new TokenGrant(response.ToAccessToken(time.GetUtcNow()), refresh, response.IdToken);
    }

    /// <summary>Gets a new access token; <c>invalid_grant</c> (revoked or expired) throws <see cref="SyncErrorKind.TokenRevoked"/>.</summary>
    public async Task<AccessToken> RefreshAsync(GoogleOAuthOptions options, string refreshToken, CancellationToken ct)
    {
        var response = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.DesktopClientId,
            ["client_secret"] = options.DesktopClientSecret,
        }, ct);
        return response.ToAccessToken(time.GetUtcNow());
    }

    /// <summary>Revokes a refresh or access token; true when Google accepted it (an already invalid token counts as done).</summary>
    public async Task<bool> RevokeAsync(string token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, RevokeEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }),
            };
            using var response = await GoogleHttp.SendAsync(http, request, ct);
            // 400 invalid_token: the token was already revoked or expired — nothing left to revoke.
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest;
        }
        catch (SyncException)
        {
            return false;
        }
    }

    /// <summary>The e-mail of the account the access token belongs to (scope <c>email</c>).</summary>
    public async Task<string> GetEmailAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await GoogleHttp.SendAsync(http, request, ct);
        await GoogleHttp.EnsureSuccessAsync(response, time.GetUtcNow(), ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.TryGetProperty("email", out var email) && email.GetString() is { Length: > 0 } text
            ? text
            : throw new SyncException(SyncErrorKind.Other, "Google returned no e-mail address.");
    }

    /// <summary>
    /// Reads the <c>email</c> claim of an ID token received directly from the token endpoint over TLS (so its signature need
    /// not be verified); null when the token has no e-mail.
    /// </summary>
    public static string? EmailFromIdToken(string? idToken)
    {
        var parts = idToken?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("email", out var email) ? email.GetString() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private async Task<TokenResponse> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        using var response = await GoogleHttp.SendAsync(http, request, ct);
        await GoogleHttp.EnsureSuccessAsync(response, time.GetUtcNow(), ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        try
        {
            return JsonSerializer.Deserialize<TokenResponse>(json) ?? throw new SyncException(SyncErrorKind.Other, "Empty token answer.");
        }
        catch (JsonException ex)
        {
            throw new SyncException(SyncErrorKind.Other, "The token answer cannot be read.", ex);
        }
    }

    private sealed class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        public AccessToken ToAccessToken(DateTimeOffset now) => new(
            AccessToken ?? throw new SyncException(SyncErrorKind.Other, "The token answer has no access token."),
            now.AddSeconds(ExpiresIn > 0 ? ExpiresIn : 3600));
    }
}
