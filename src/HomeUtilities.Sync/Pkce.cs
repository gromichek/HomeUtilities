using System.Security.Cryptography;
using System.Text;

namespace HomeUtilities.Sync.Google;

/// <summary>PKCE (RFC 7636) for the authorization code flow of the installed-app (desktop) client.</summary>
public static class Pkce
{
    /// <summary>A random code verifier: 32 random bytes as base64url, 43 characters.</summary>
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge: <c>BASE64URL(SHA256(ASCII(verifier)))</c>.</summary>
    public static string CreateChallenge(string verifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(verifier);
        return Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    }

    /// <summary>A random value for the OAuth <c>state</c> parameter.</summary>
    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
