using System.Security.Cryptography;
using System.Text;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.App.Services;

/// <summary>
/// Windows secret store: one file per key under <c>AppDataDirectory/secrets</c>, encrypted with DPAPI for the current user.
/// MAUI <c>SecureStorage</c> relies on package storage that an unpackaged app does not have, so DPAPI is used directly.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HomeUtilities.Sync.v1");

    private static string FileFor(string key)
    {
        var folder = Path.Combine(FileSystem.AppDataDirectory, "secrets");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, key + ".bin");
    }

    public async Task<string?> GetAsync(string key)
    {
        var file = FileFor(key);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(file);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            // Another Windows user or a damaged file: treat as signed out.
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(FileFor(key), bytes);
    }

    public void Remove(string key)
    {
        var file = FileFor(key);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }
}
