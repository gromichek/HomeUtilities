using System.Text.Json;

namespace HomeUtilities.Sync.Google;

/// <summary>
/// OAuth client values of this build, read from the git-ignored <c>secrets.json</c> that is embedded at build time
/// (ARCHITECTURE §9). A build without them is "not configured": the Sync page says so and Connect is disabled.
/// </summary>
public sealed record GoogleOAuthOptions(string DesktopClientId, string DesktopClientSecret, string AndroidClientId)
{
    public const string DriveAppDataScope = "https://www.googleapis.com/auth/drive.appdata";

    public static GoogleOAuthOptions Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    /// <summary>The Windows desktop flow needs both the client id and the (non-confidential) secret.</summary>
    public bool HasDesktopClient => DesktopClientId.Length > 0 && DesktopClientSecret.Length > 0;

    /// <summary>The Android client is identified by package name + signing certificate, so its id is only a "configured" switch.</summary>
    public bool HasAndroidClient => AndroidClientId.Length > 0;

    /// <summary>Parses <c>secrets.json</c>; null, empty or invalid text gives <see cref="Empty"/> (never throws, never logs values).</summary>
    public static GoogleOAuthOptions Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                ? new GoogleOAuthOptions(Read(root, "googleDesktopClientId"), Read(root, "googleDesktopClientSecret"), Read(root, "googleAndroidClientId"))
                : Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    private static string Read(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;
}
