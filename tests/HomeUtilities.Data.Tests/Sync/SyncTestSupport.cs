using System.Text;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Sync;
using HomeUtilities.Data.Backup;
using HomeUtilities.Data.Sync;
using HomeUtilities.Data.Tests.Backup;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sync;

/// <summary>An in-memory cloud folder; can be made to fail after a number of uploads.</summary>
internal sealed class FakeSyncStorage : ISyncStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public int? FailAfterUploads { get; set; }

    public bool Offline { get; set; }

    public int Uploads { get; private set; }

    /// <summary>Modification date reported for every file (the provider's date is not used by the sync itself).</summary>
    public DateTimeOffset? Modified { get; set; }

    public Task<IReadOnlyList<SyncFileInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        Check();
        return Task.FromResult<IReadOnlyList<SyncFileInfo>>([.. Files.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(f => new SyncFileInfo(f.Key, f.Value.Length, Modified))]);
    }

    public Task UploadAsync(string path, Stream content, CancellationToken ct = default)
    {
        Check();
        if (FailAfterUploads is { } limit && Uploads >= limit)
        {
            throw new SyncException(SyncErrorKind.NoNetwork, "Upload failed (test).");
        }

        using var memory = new MemoryStream();
        content.CopyTo(memory);
        Files[path] = memory.ToArray();
        Uploads++;
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string path, CancellationToken ct = default)
    {
        Check();
        return Files.TryGetValue(path, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new SyncException(SyncErrorKind.NotFound, "No such file.");
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        Check();
        Files.Remove(path);
        return Task.CompletedTask;
    }

    private void Check()
    {
        if (Offline)
        {
            throw new SyncException(SyncErrorKind.NoNetwork, "Offline (test).");
        }
    }
}

/// <summary>A network that can be switched between Wi-Fi, mobile data and nothing.</summary>
internal sealed class FakeConnectivity : ISyncConnectivity
{
    public bool IsConnected { get; set; } = true;

    public bool IsUnmetered { get; set; } = true;

    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>One device with its own database, clock and sync engine over a shared cloud.</summary>
internal sealed class SyncDevice : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    public SyncDevice(FakeSyncStorage cloud, SyncOptions? options = null)
    {
        Connectivity = new FakeConnectivity();
        Fixture = new BackupFixture();
        Fixture.Clock.UtcNow = Start;
        Cloud = cloud;
        Notifier = new DataChangeNotifier();
        Engine = new SyncEngine(
            Fixture.Get<IDbContextFactory<HomeUtilitiesDbContext>>(),
            cloud,
            Fixture.Clock,
            Notifier,
            (options ?? new SyncOptions("test")) with { AttachmentsFolder = Fixture.AttachmentsFolder },
            (source, ex) =>
            {
                if (ex is InvalidDataException)
                {
                    Logged.Add(ex.Message);
                    return;
                }

                throw new InvalidOperationException($"{source}: {ex}", ex);
            },
            () => Email,
            Connectivity,
            () => MobileDataAllowed);
    }

    /// <summary>The e-mail of the signed-in account.</summary>
    public string Email { get; set; } = "anna@example.test";

    public bool MobileDataAllowed { get; set; }

    public FakeConnectivity Connectivity { get; }

    /// <summary>Errors the engine logged and survived (for example a wrong checksum).</summary>
    public List<string> Logged { get; } = [];

    public BackupFixture Fixture { get; }

    public FakeSyncStorage Cloud { get; }

    public DataChangeNotifier Notifier { get; }

    public SyncEngine Engine { get; }

    public DateTimeOffset Now
    {
        get => Fixture.Clock.UtcNow;
        set => Fixture.Clock.UtcNow = value;
    }

    public IDbContextFactory<HomeUtilitiesDbContext> Factory => Fixture.Get<IDbContextFactory<HomeUtilitiesDbContext>>();

    public static async Task<SyncDevice> CreateAsync(FakeSyncStorage cloud, SyncOptions? options = null, bool seeded = true)
    {
        var device = new SyncDevice(cloud, options);
        if (seeded)
        {
            await device.Fixture.InitializeAsync();
        }
        else
        {
            await using var db = await device.Factory.CreateDbContextAsync();
            await DatabaseMigrator.MigrateAsync(db, CancellationToken.None);
        }

        await device.Engine.InitializeAsync();
        return device;
    }

    public async Task<Guid> AddPropertyAsync(string name)
    {
        var property = new Property { Name = name, Kind = PropertyKind.House };
        await Fixture.Properties.SaveAsync(property);
        return property.Id;
    }

    public async Task RenameAsync(Guid id, string name)
    {
        await using var db = await Factory.CreateDbContextAsync();
        var property = await db.Properties.SingleAsync(p => p.Id == id);
        property.Name = name;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await Factory.CreateDbContextAsync();
        db.Properties.Remove(await db.Properties.SingleAsync(p => p.Id == id));
        await db.SaveChangesAsync();
    }

    /// <summary>Name and deleted flag of a property, or null if the row does not exist at all.</summary>
    public async Task<(string Name, bool IsDeleted, DateTimeOffset UpdatedAt)?> PropertyAsync(Guid id)
    {
        await using var db = await Factory.CreateDbContextAsync();
        var row = await db.Properties.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == id);
        return row is null ? null : (row.Name, row.IsDeleted, row.UpdatedAt);
    }

    public async Task<List<string>> LivePropertyNamesAsync()
    {
        await using var db = await Factory.CreateDbContextAsync();
        return await db.Properties.Select(p => p.Name).OrderBy(n => n).ToListAsync();
    }

    public async Task<int> OutboxCountAsync()
    {
        await using var db = await Factory.CreateDbContextAsync();
        return (await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"SyncOutbox\"").ToListAsync()).Single() is var n ? (int)n : 0;
    }

    public async Task<bool> SyncAsync() => await Engine.SyncAsync(SyncTrigger.Manual);

    public async Task<string> ExportAsync()
    {
        var stream = new MemoryStream();
        await Fixture.DataExchange.ExportAsync(stream, withAttachments: false);
        return Regex.Replace(Encoding.UTF8.GetString(stream.ToArray()), "\"exportedAt\": \"[^\"]*\",?", string.Empty);
    }

    public void Dispose() => Fixture.Dispose();
}
