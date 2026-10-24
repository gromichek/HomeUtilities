using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace HomeUtilities.Data.Tests.Backup;

/// <summary>A device: a temp folder with a database file, attachments and a backup service over them.</summary>
internal sealed class BackupFixture : IDisposable
{
    private readonly ServiceProvider _services;

    public BackupFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "hu-backup-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Clock = new FakeClock(new DateTimeOffset(2026, 10, 24, 9, 30, 0, TimeSpan.Zero));
        Options = new BackupOptions(DatabasePath, AttachmentsFolder, Path.Combine(Root, "restore"), Path.Combine(Root, "backups"), "1.0.0", "Test");
        Backup = new BackupService(Options, Clock);
        _services = new ServiceCollection()
            .AddSingleton<IClock>(Clock)
            .AddHomeUtilitiesData(DatabasePath, AttachmentsFolder)
            .BuildServiceProvider();
    }

    public string Root { get; }

    public string DatabasePath => Path.Combine(Root, "homeutilities.db");

    public string AttachmentsFolder => Path.Combine(Root, "attachments");

    public FakeClock Clock { get; }

    public BackupOptions Options { get; }

    public BackupService Backup { get; }

    public IPropertyStore Properties => _services.GetRequiredService<IPropertyStore>();

    public async Task InitializeAsync() => await _services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

    public async Task AddPropertiesAsync(params string[] names)
    {
        foreach (var name in names)
        {
            await Properties.SaveAsync(new Property { Name = name, Kind = PropertyKind.House });
        }
    }

    public void WriteAttachment(string relativePath, string content)
    {
        var path = Path.Combine(AttachmentsFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public async Task<MemoryStream> BackupToMemoryAsync()
    {
        var stream = new MemoryStream();
        await Backup.CreateAsync(stream);
        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
