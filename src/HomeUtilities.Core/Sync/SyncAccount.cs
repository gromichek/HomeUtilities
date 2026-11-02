namespace HomeUtilities.Core.Sync;

/// <summary>Whether a sync account is usable.</summary>
public enum SyncAccountStatus
{
    /// <summary>This build has no OAuth client ids (no <c>secrets.json</c>) or the platform has no sign-in.</summary>
    NotConfigured,

    SignedOut,

    SignedIn,

    /// <summary>The stored sign-in no longer works (for example the access was revoked); see <see cref="SyncAccountState.Error"/>.</summary>
    Error,
}

/// <summary>Why a sign-in or a storage call failed; the UI maps each kind to one localized message.</summary>
public enum SyncErrorKind
{
    Other,
    NotConfigured,
    NoNetwork,
    Cancelled,
    ConsentDenied,
    TokenRevoked,
    ApiDisabled,
    ClockSkew,
    Timeout,
    NotFound,
}

/// <summary>The current state of the account: status, the e-mail when signed in, the error kind when in error.</summary>
public sealed record SyncAccountState(SyncAccountStatus Status, string? Email = null, SyncErrorKind? Error = null)
{
    public static SyncAccountState NotConfigured { get; } = new(SyncAccountStatus.NotConfigured);

    public static SyncAccountState SignedOut { get; } = new(SyncAccountStatus.SignedOut);

    public static SyncAccountState SignedIn(string email) => new(SyncAccountStatus.SignedIn, email);

    public static SyncAccountState Failed(SyncErrorKind error, string? email = null) => new(SyncAccountStatus.Error, email, error);
}

/// <summary>Outcome of signing out: the local sign-in is always removed; the revoke call at the provider may fail.</summary>
public enum SignOutResult
{
    Done,

    /// <summary>The sign-in was removed here, but the provider could not be told (no network); the user can remove the access in the account settings.</summary>
    RevokeFailed,
}

/// <summary>A failure of sign-in or of a storage call, carrying the <see cref="SyncErrorKind"/> the UI shows.</summary>
public sealed class SyncException(SyncErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public SyncErrorKind Kind { get; } = kind;
}

/// <summary>
/// The account the sync talks to (ARCHITECTURE §8). One implementation per platform; tokens never leave it.
/// </summary>
public interface ISyncAccountService
{
    /// <summary>Name of the storage provider shown to the user (e.g. "Google Drive").</summary>
    string ProviderName { get; }

    SyncAccountState State { get; }

    /// <summary>Raised after <see cref="State"/> changed.</summary>
    event EventHandler? StateChanged;

    /// <summary>Reads the stored sign-in (no network). Call once at start and when the Sync page opens.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Opens the system sign-in and stores the result; throws <see cref="SyncException"/> (Cancelled, ConsentDenied, …).</summary>
    Task SignInAsync(CancellationToken ct = default);

    /// <summary>Deletes everything stored and revokes the access at the provider (best effort).</summary>
    Task<SignOutResult> SignOutAsync(CancellationToken ct = default);

    /// <summary>A valid access token, refreshed when needed. Throws <see cref="SyncException"/> when signed out or revoked.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>A file in the app's own remote folder.</summary>
public sealed record SyncFileInfo(string Path, long Size, DateTimeOffset? ModifiedAt);

/// <summary>
/// The app's own folder at the storage provider (ARCHITECTURE §8): flat files addressed by a path such as
/// <c>changes/&lt;deviceId&gt;/1.json</c>. Google Drive <c>appDataFolder</c> is the first implementation.
/// </summary>
public interface ISyncStorage
{
    /// <summary>All files whose path starts with <paramref name="prefix"/> (empty = all).</summary>
    Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default);

    /// <summary>Creates the file or replaces its content.</summary>
    Task UploadAsync(string path, Stream content, CancellationToken ct = default);

    /// <summary>The content of the file; throws <see cref="SyncException"/> with <see cref="SyncErrorKind.NotFound"/> when it does not exist.</summary>
    Task<Stream> DownloadAsync(string path, CancellationToken ct = default);

    /// <summary>Deletes the file; a missing file is not an error.</summary>
    Task DeleteAsync(string path, CancellationToken ct = default);
}

/// <summary>Small secrets (refresh token, e-mail) kept per user on this device; never synced, never logged.</summary>
public interface ISecretStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);
}

/// <summary>Opens a URL in the system browser (for the sign-in page).</summary>
public interface IUrlOpener
{
    Task<bool> OpenAsync(Uri url);
}

/// <summary>
/// The "Check connection" test of the Sync page: writes, lists, reads and deletes a small file in the app folder.
/// Returns the step that failed (null = all four worked) and the exception.
/// </summary>
public static class SyncConnectionCheck
{
    public const string TestFilePath = "connection-check.txt";

    public static async Task<SyncCheckResult> RunAsync(ISyncStorage storage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        var content = "HomeUtilities connection check " + Guid.NewGuid().ToString("N");
        var step = SyncCheckStep.Write;
        try
        {
            await storage.UploadAsync(TestFilePath, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), ct);

            step = SyncCheckStep.List;
            var files = await storage.ListAsync(TestFilePath, ct);
            if (!files.Any(f => f.Path == TestFilePath))
            {
                throw new SyncException(SyncErrorKind.NotFound, "The test file is not in the list.");
            }

            step = SyncCheckStep.Read;
            await using (var stream = await storage.DownloadAsync(TestFilePath, ct))
            using (var reader = new StreamReader(stream))
            {
                if (await reader.ReadToEndAsync(ct) != content)
                {
                    throw new SyncException(SyncErrorKind.Other, "The test file has other content than written.");
                }
            }

            step = SyncCheckStep.Delete;
            await storage.DeleteAsync(TestFilePath, ct);
            return new SyncCheckResult(null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SyncCheckResult(step, ex);
        }
    }
}

public enum SyncCheckStep
{
    Write,
    List,
    Read,
    Delete,
}

/// <summary>Result of <see cref="SyncConnectionCheck"/>: <see cref="FailedStep"/> is null when everything worked.</summary>
public sealed record SyncCheckResult(SyncCheckStep? FailedStep, Exception? Error)
{
    public bool Succeeded => FailedStep is null;
}
