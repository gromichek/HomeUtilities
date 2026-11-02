using System.Net;
using System.Text;
using HomeUtilities.Core.Sync;
using HomeUtilities.Sync.Google;

namespace HomeUtilities.Sync.Tests;

public class DriveStorageTests
{
    private sealed class FakeAccount : ISyncAccountService
    {
        public string ProviderName => "Fake";
        public SyncAccountState State => SyncAccountState.SignedIn("anna@example.test");
        public event EventHandler? StateChanged { add { } remove { } }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<SignOutResult> SignOutAsync(CancellationToken ct = default) => Task.FromResult(SignOutResult.Done);
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("tok");
    }

    private static GoogleDriveStorage Storage(FakeHandler handler) =>
        new(new HttpClient(handler), new FakeAccount(), new FixedTime(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task List_asks_the_appDataFolder_pages_through_and_filters_by_prefix()
    {
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.OK, """{"nextPageToken":"p2","files":[{"id":"1","name":"changes/a/1.json","size":"10","modifiedTime":"2026-11-02T10:00:00.000Z"},{"id":"2","name":"snapshot.json.gz"}]}""")
            .Enqueue(HttpStatusCode.OK, """{"files":[{"id":"3","name":"changes/b/1.json","size":"5"}]}""");

        var files = await Storage(handler).ListAsync("changes/", CancellationToken.None);

        Assert.Equal(["changes/a/1.json", "changes/b/1.json"], files.Select(f => f.Path));
        Assert.Equal(10, files[0].Size);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero), files[0].ModifiedAt);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Get, r.Method);
            Assert.Equal("Bearer tok", r.Authorization);
            Assert.Contains("spaces=appDataFolder", r.Uri.Query);
        });
        Assert.Contains("pageToken=p2", handler.Requests[1].Uri.Query);
    }

    [Fact]
    public async Task Upload_of_a_new_file_posts_multipart_with_the_appDataFolder_parent()
    {
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.OK, """{"files":[]}""")
            .Enqueue(HttpStatusCode.OK, """{"id":"new"}""");

        await Storage(handler).UploadAsync("changes/a/1.json", new MemoryStream(Encoding.UTF8.GetBytes("{\"x\":1}")), CancellationToken.None);

        var lookup = handler.Requests[0];
        Assert.Contains("spaces=appDataFolder", lookup.Uri.Query);
        Assert.Contains(Uri.EscapeDataString("name = 'changes/a/1.json'"), lookup.Uri.Query);

        var upload = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, upload.Method);
        Assert.Equal("https://www.googleapis.com/upload/drive/v3/files", upload.Uri.GetLeftPart(UriPartial.Path));
        Assert.Contains("uploadType=multipart", upload.Uri.Query);
        Assert.StartsWith("multipart/related", upload.ContentType);
        Assert.Contains("\"name\":\"changes/a/1.json\"", upload.Body);
        Assert.Contains("\"parents\":[\"appDataFolder\"]", upload.Body);
        Assert.Contains("{\"x\":1}", upload.Body);
    }

    [Fact]
    public async Task Upload_of_an_existing_file_patches_it_without_the_parent()
    {
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.OK, """{"files":[{"id":"f1"}]}""")
            .Enqueue(HttpStatusCode.OK, """{"id":"f1"}""");

        await Storage(handler).UploadAsync("a.json", new MemoryStream([1, 2, 3]), CancellationToken.None);

        var upload = handler.Requests[1];
        Assert.Equal(HttpMethod.Patch, upload.Method);
        Assert.Equal("https://www.googleapis.com/upload/drive/v3/files/f1", upload.Uri.GetLeftPart(UriPartial.Path));
        Assert.DoesNotContain("parents", upload.Body);
    }

    [Fact]
    public async Task Names_with_quotes_are_escaped_in_the_query()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.OK, """{"files":[]}""");
        await Storage(handler).DeleteAsync("it's", CancellationToken.None);
        Assert.Contains(Uri.EscapeDataString("name = 'it\\'s'"), handler.Requests[0].Uri.Query);
    }

    [Fact]
    public async Task Download_finds_the_id_and_reads_the_media()
    {
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.OK, """{"files":[{"id":"f1"}]}""")
            .EnqueueBytes([9, 8, 7]);

        await using var stream = await Storage(handler).DownloadAsync("a.json", CancellationToken.None);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal([9, 8, 7], copy.ToArray());
        Assert.Equal("https://www.googleapis.com/drive/v3/files/f1?alt=media", handler.Requests[1].Uri.ToString());
    }

    [Fact]
    public async Task Download_of_a_missing_file_is_NotFound()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.OK, """{"files":[]}""");
        var ex = await Assert.ThrowsAsync<SyncException>(() => Storage(handler).DownloadAsync("a.json", CancellationToken.None));
        Assert.Equal(SyncErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public async Task Delete_removes_every_file_with_the_name_and_ignores_a_missing_one()
    {
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.OK, """{"files":[{"id":"f1"},{"id":"f2"}]}""")
            .Enqueue(HttpStatusCode.NoContent, string.Empty)
            .Enqueue(HttpStatusCode.NotFound, """{"error":{"code":404}}""");

        await Storage(handler).DeleteAsync("a.json", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.EndsWith("/files/f1", handler.Requests[1].Uri.ToString());
        Assert.EndsWith("/files/f2", handler.Requests[2].Uri.ToString());
    }

    [Fact]
    public async Task A_disabled_drive_api_surfaces_as_ApiDisabled()
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.Forbidden, """{"error":{"code":403,"errors":[{"reason":"accessNotConfigured"}]}}""");
        var ex = await Assert.ThrowsAsync<SyncException>(() => Storage(handler).ListAsync("", CancellationToken.None));
        Assert.Equal(SyncErrorKind.ApiDisabled, ex.Kind);
    }

    [Fact]
    public async Task Connection_check_runs_write_list_read_delete_in_order()
    {
        // The content must equal what was written: serve back the uploaded text.
        var storage = new EchoStorage();
        var result = await SyncConnectionCheck.RunAsync(storage);
        Assert.True(result.Succeeded);
        Assert.Equal(["upload", "list", "download", "delete"], storage.Calls);
    }

    [Fact]
    public async Task Connection_check_reports_the_failed_step()
    {
        var storage = new EchoStorage { FailOn = "download" };
        var result = await SyncConnectionCheck.RunAsync(storage);
        Assert.False(result.Succeeded);
        Assert.Equal(SyncCheckStep.Read, result.FailedStep);
        Assert.IsType<SyncException>(result.Error);
    }

    private sealed class EchoStorage : ISyncStorage
    {
        private byte[] _content = [];

        public List<string> Calls { get; } = [];

        public string? FailOn { get; init; }

        private void Step(string name)
        {
            Calls.Add(name);
            if (name == FailOn)
            {
                throw new SyncException(SyncErrorKind.NoNetwork, "offline");
            }
        }

        public Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default)
        {
            Step("list");
            return Task.FromResult<IReadOnlyList<SyncFileInfo>>([new SyncFileInfo(SyncConnectionCheck.TestFilePath, _content.Length, null)]);
        }

        public async Task UploadAsync(string path, Stream content, CancellationToken ct = default)
        {
            Step("upload");
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            _content = copy.ToArray();
        }

        public Task<Stream> DownloadAsync(string path, CancellationToken ct = default)
        {
            Step("download");
            return Task.FromResult<Stream>(new MemoryStream(_content));
        }

        public Task DeleteAsync(string path, CancellationToken ct = default)
        {
            Step("delete");
            return Task.CompletedTask;
        }
    }
}
