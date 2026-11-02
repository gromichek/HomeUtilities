using System.Reflection;
using HomeUtilities.Sync.Google;

namespace HomeUtilities.App.Services;

/// <summary>
/// Reads the OAuth client values embedded from the git-ignored <c>secrets.json</c> (see <c>secrets.template.json</c>).
/// A build without the file (CI) is "not configured". Values are never logged.
/// </summary>
public static class SyncSecrets
{
    public const string ResourceName = "secrets.json";

    public static GoogleOAuthOptions Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return GoogleOAuthOptions.Empty;
            }

            using var reader = new StreamReader(stream);
            return GoogleOAuthOptions.Parse(reader.ReadToEnd());
        }
        catch (IOException)
        {
            return GoogleOAuthOptions.Empty;
        }
    }
}
