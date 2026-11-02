using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Google;

/// <summary>
/// <see cref="ISyncStorage"/> on the hidden Google Drive <c>appDataFolder</c> through the Drive v3 REST API. The path of a
/// file is its Drive file name (a name may contain "/"), so the folder stays flat; scope <c>drive.appdata</c> only.
/// </summary>
public sealed class GoogleDriveStorage(HttpClient http, ISyncAccountService account, TimeProvider time) : ISyncStorage, IServerClock
{
    /// <inheritdoc />
    public DateTimeOffset? ServerTime { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? ObservedAt { get; private set; }

    public const string FilesUrl = "https://www.googleapis.com/drive/v3/files";
    public const string UploadUrl = "https://www.googleapis.com/upload/drive/v3/files";
    private const string Space = "appDataFolder";

    public async Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var result = new List<SyncFileInfo>();
        string? pageToken = null;
        do
        {
            var url = $"{FilesUrl}?spaces={Space}&pageSize=1000&fields={Uri.EscapeDataString("nextPageToken,files(id,name,size,modifiedTime)")}"
                + (pageToken is null ? string.Empty : "&pageToken=" + Uri.EscapeDataString(pageToken));
            using var document = JsonDocument.Parse(await SendForStringAsync(HttpMethod.Get, url, null, ct));
            var root = document.RootElement;
            if (root.TryGetProperty("files", out var files))
            {
                foreach (var file in files.EnumerateArray())
                {
                    var name = file.GetProperty("name").GetString() ?? string.Empty;
                    if (name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        result.Add(new SyncFileInfo(
                            name,
                            file.TryGetProperty("size", out var size) && long.TryParse(size.GetString(), out var bytes) ? bytes : 0,
                            file.TryGetProperty("modifiedTime", out var modified) && DateTimeOffset.TryParse(modified.GetString(), out var at) ? at : null));
                    }
                }
            }

            pageToken = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        }
        while (pageToken is not null);

        return result;
    }

    public async Task UploadAsync(string path, Stream content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);
        var existing = await FindIdAsync(path, ct);

        // Create: POST with the parent; update: PATCH of the existing id (the parent cannot be sent again).
        var metadata = existing is null
            ? JsonSerializer.Serialize(new { name = path, parents = new[] { Space } })
            : JsonSerializer.Serialize(new { name = path });
        var multipart = new MultipartContent("related", "hu_" + Guid.NewGuid().ToString("N"));
        multipart.Add(new StringContent(metadata, Encoding.UTF8, "application/json"));
        var media = new StreamContent(content);
        media.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(media);

        var url = existing is null
            ? $"{UploadUrl}?uploadType=multipart&fields=id"
            : $"{UploadUrl}/{Uri.EscapeDataString(existing)}?uploadType=multipart&fields=id";
        await SendForStringAsync(existing is null ? HttpMethod.Post : HttpMethod.Patch, url, multipart, ct);
    }

    public async Task<Stream> DownloadAsync(string path, CancellationToken ct = default)
    {
        var id = await FindIdAsync(path, ct)
            ?? throw new SyncException(SyncErrorKind.NotFound, "The file does not exist.");
        var bytes = await SendForBytesAsync(HttpMethod.Get, $"{FilesUrl}/{Uri.EscapeDataString(id)}?alt=media", ct);
        return new MemoryStream(bytes);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        // Several files may share a name in Drive; delete all of them so the path is really gone.
        foreach (var id in await FindIdsAsync(path, ct))
        {
            try
            {
                await SendForStringAsync(HttpMethod.Delete, $"{FilesUrl}/{Uri.EscapeDataString(id)}", null, ct);
            }
            catch (SyncException ex) when (ex.Kind == SyncErrorKind.NotFound)
            {
                // Already gone.
            }
        }
    }

    private async Task<string?> FindIdAsync(string path, CancellationToken ct) => (await FindIdsAsync(path, ct)).FirstOrDefault();

    private async Task<List<string>> FindIdsAsync(string path, CancellationToken ct)
    {
        var query = $"name = '{path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'";
        var url = $"{FilesUrl}?spaces={Space}&pageSize=100&fields={Uri.EscapeDataString("files(id)")}&q={Uri.EscapeDataString(query)}";
        using var document = JsonDocument.Parse(await SendForStringAsync(HttpMethod.Get, url, null, ct));
        return document.RootElement.TryGetProperty("files", out var files)
            ? [.. files.EnumerateArray().Select(f => f.GetProperty("id").GetString()!)]
            : [];
    }

    private async Task<string> SendForStringAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var response = await SendAsync(method, url, content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<byte[]> SendForBytesAsync(HttpMethod method, string url, CancellationToken ct)
    {
        using var response = await SendAsync(method, url, null, ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await account.GetAccessTokenAsync(ct));
        var response = await GoogleHttp.SendAsync(http, request, ct);
        if (response.Headers.Date is { } serverDate)
        {
            ServerTime = serverDate;
            ObservedAt = time.GetUtcNow();
        }

        try
        {
            await GoogleHttp.EnsureSuccessAsync(response, time.GetUtcNow(), ct);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
