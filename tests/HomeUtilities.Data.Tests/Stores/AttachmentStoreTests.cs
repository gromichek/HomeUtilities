using System.Security.Cryptography;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Common;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public sealed class AttachmentStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "homeutilities-attachments-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static Attachment Meta(Guid? ownerId = null, string name = "dial.jpg", string type = "image/jpeg") => new()
    {
        FileName = name,
        ContentType = type,
        OwnerKind = AttachmentOwnerKind.MeterReading,
        OwnerId = ownerId ?? Guid.NewGuid(),
    };

    private static MemoryStream Bytes(params byte[] bytes) => new(bytes);

    [Fact]
    public async Task Add_copies_the_file_as_id_plus_extension_and_saves_the_row()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        var owner = Guid.NewGuid();
        byte[] content = [1, 2, 3, 4, 5];

        var saved = await store.AddAsync(Meta(owner, "IMG 0042.JPG"), Bytes(content));

        var path = Path.Combine(_folder, saved.Id + ".jpg");
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
        var row = (await store.GetAsync(saved.Id))!;
        Assert.Equal(("IMG 0042.JPG", "image/jpeg", 5L, owner), (row.FileName, row.ContentType, row.SizeBytes, row.OwnerId));
        Assert.Equal(AttachmentOwnerKind.MeterReading, row.OwnerKind);
    }

    [Fact]
    public async Task Add_computes_size_and_sha256()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        byte[] content = [10, 20, 30];
        var expected = Convert.ToHexStringLower(SHA256.HashData(content));

        var saved = await store.AddAsync(Meta(), Bytes(content));

        Assert.Equal(3, saved.SizeBytes);
        Assert.Equal(expected, saved.Sha256);
        Assert.Equal(64, saved.Sha256.Length);
        Assert.Equal(expected, (await store.GetAsync(saved.Id))!.Sha256);
    }

    [Fact]
    public async Task Extension_follows_the_content_type_not_the_file_name()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);

        var saved = await store.AddAsync(Meta(name: "bill.exe", type: "application/pdf"), Bytes(1));

        Assert.True(File.Exists(Path.Combine(_folder, saved.Id + ".pdf")));
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task Add_stamps_the_timestamps()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);

        var saved = await store.AddAsync(Meta(), Bytes(1));

        var row = (await store.GetAsync(saved.Id))!;
        Assert.Equal(test.Clock.UtcNow, row.CreatedAt);
        Assert.Equal(test.Clock.UtcNow, row.UpdatedAt);
    }

    [Fact]
    public async Task Add_rejects_an_empty_file_without_writing_anything()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);

        var ex = await Assert.ThrowsAsync<AttachmentRejectedException>(() => store.AddAsync(Meta(), Bytes()));

        Assert.Equal(ErrorCodes.AttachmentEmpty, Assert.Single(ex.Result.Errors).Code);
        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
        await using var db = test.NewContext();
        Assert.Empty(await db.Attachments.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Add_rejects_a_file_over_20_MB()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        using var huge = new MemoryStream(new byte[20 * 1024 * 1024 + 1]);

        var ex = await Assert.ThrowsAsync<AttachmentRejectedException>(() => store.AddAsync(Meta(), huge));

        Assert.Equal(ErrorCodes.AttachmentTooLarge, Assert.Single(ex.Result.Errors).Code);
        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
    }

    [Fact]
    public async Task Add_accepts_a_file_of_exactly_20_MB()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        using var content = new MemoryStream(new byte[20 * 1024 * 1024]);
        content.Position = 0;

        var saved = await store.AddAsync(Meta(), content);

        Assert.Equal(20L * 1024 * 1024, saved.SizeBytes);
    }

    [Fact]
    public async Task Add_rejects_a_content_type_that_is_not_allowed()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);

        var ex = await Assert.ThrowsAsync<AttachmentRejectedException>(() => store.AddAsync(Meta(type: "application/zip", name: "a.zip"), Bytes(1)));

        Assert.Equal(ErrorCodes.AttachmentContentTypeNotAllowed, Assert.Single(ex.Result.Errors).Code);
    }

    [Fact]
    public async Task Database_failure_removes_the_copied_file()
    {
        var store = new AttachmentStore(new FailingFactory(), _folder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync(Meta(), Bytes(1, 2, 3)));

        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task Soft_delete_hides_the_row_and_keeps_the_file()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        var owner = Guid.NewGuid();
        var saved = await store.AddAsync(Meta(owner), Bytes(1, 2));
        var path = Path.Combine(_folder, saved.Id + ".jpg");

        await store.DeleteAsync(saved.Id);

        Assert.Null(await store.GetAsync(saved.Id));
        Assert.Null(await store.OpenReadAsync(saved.Id));
        Assert.Empty(await store.ListByOwnerAsync(AttachmentOwnerKind.MeterReading, owner));
        Assert.True(File.Exists(path));
        await using var db = test.NewContext();
        Assert.True((await db.Attachments.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Delete_of_an_unknown_id_does_nothing()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);

        await store.DeleteAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task List_by_owner_returns_only_that_owner_and_kind()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        var reading = Guid.NewGuid();
        var other = Guid.NewGuid();
        var first = await store.AddAsync(Meta(reading, "a.jpg"), Bytes(1));
        test.Clock.UtcNow = test.Clock.UtcNow.AddMinutes(1);
        var second = await store.AddAsync(Meta(reading, "b.jpg"), Bytes(2));
        await store.AddAsync(Meta(other), Bytes(3));
        var sameIdOtherKind = Meta(reading);
        sameIdOtherKind.OwnerKind = AttachmentOwnerKind.Bill;
        await store.AddAsync(sameIdOtherKind, Bytes(4));

        var list = await store.ListByOwnerAsync(AttachmentOwnerKind.MeterReading, reading);

        Assert.Equal([first.Id, second.Id], list.Select(a => a.Id));
    }

    [Fact]
    public async Task Open_read_returns_the_stored_bytes()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        var saved = await store.AddAsync(Meta(), Bytes(9, 8, 7));

        await using var stream = (await store.OpenReadAsync(saved.Id))!;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal([9, 8, 7], copy.ToArray());
        Assert.Equal(Path.Combine(_folder, saved.Id + ".jpg"), await store.GetFilePathAsync(saved.Id));
    }

    [Fact]
    public async Task Open_read_returns_null_when_the_file_is_missing()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder);
        var saved = await store.AddAsync(Meta(), Bytes(1));
        File.Delete(Path.Combine(_folder, saved.Id + ".jpg"));

        Assert.Null(await store.OpenReadAsync(saved.Id));
        Assert.Null(await store.GetFilePathAsync(saved.Id));
    }

    [Fact]
    public async Task Image_processor_result_is_stored_and_changes_the_extension()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder, new ShrinkingProcessor());

        var saved = await store.AddAsync(Meta(name: "big.png", type: "image/png"), Bytes(1, 2, 3, 4, 5, 6));

        Assert.Equal(("big.jpg", "image/jpeg", 2L), (saved.FileName, saved.ContentType, saved.SizeBytes));
        Assert.Equal([1, 2], await File.ReadAllBytesAsync(Path.Combine(_folder, saved.Id + ".jpg")));
    }

    [Fact]
    public async Task Image_processor_is_not_used_for_pdf()
    {
        using var test = new TestDb();
        var store = new AttachmentStore(test.Factory, _folder, new ShrinkingProcessor());

        var saved = await store.AddAsync(Meta(name: "bill.pdf", type: "application/pdf"), Bytes(1, 2, 3, 4));

        Assert.Equal(4, saved.SizeBytes);
        Assert.Equal("application/pdf", saved.ContentType);
    }

    private sealed class FailingFactory : IDbContextFactory<HomeUtilitiesDbContext>
    {
        public HomeUtilitiesDbContext CreateDbContext() => throw new InvalidOperationException("database is down");
    }

    /// <summary>Keeps the first two bytes and reports JPEG, like a re-encoding that made the file smaller.</summary>
    private sealed class ShrinkingProcessor : IImageProcessor
    {
        public async Task<ProcessedImage> ProcessAsync(Stream content, string contentType, CancellationToken ct = default)
        {
            var head = new byte[2];
            await content.ReadExactlyAsync(head, ct);
            return new ProcessedImage(new MemoryStream(head), "image/jpeg");
        }
    }
}
