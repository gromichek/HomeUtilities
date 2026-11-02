using HomeUtilities.Core.Sync;

namespace HomeUtilities.App.Services;

/// <summary>Android secret store: MAUI <c>SecureStorage</c> (Android Keystore). Excluded from the Android backup rules.</summary>
public sealed class SecureStorageSecretStore : ISecretStore
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception ex) when (ex is Java.Security.GeneralSecurityException or InvalidOperationException)
        {
            // The key store can lose its key (restore on another device): signed out.
            SecureStorage.Default.Remove(key);
            return null;
        }
    }

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public void Remove(string key) => SecureStorage.Default.Remove(key);
}
