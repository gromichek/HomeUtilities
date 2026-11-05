using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Backup;
using Microsoft.Data.Sqlite;

namespace HomeUtilities.Data.Tests.Backup;

public sealed class DatabaseCleanerTests : IDisposable
{
    private readonly BackupFixture _device = new();

    public void Dispose() => _device.Dispose();

    private DatabaseCleaner Cleaner => new(_device.Options);

    private async Task SampleAsync()
    {
        await _device.InitializeAsync();
        await _device.FillSampleAsync();
    }

    private SqliteConnection Open()
    {
        SqliteConnection.ClearAllPools();
        var connection = new SqliteConnection($"Data Source={_device.DatabasePath};Pooling=False");
        connection.Open();
        return connection;
    }

    private long Scalar(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private void Run(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Row count of every table that has an IsDeleted column: (all rows, soft-deleted rows).</summary>
    private Dictionary<string, (long All, long Deleted)> Counts()
    {
        using var connection = Open();
        var tables = new List<string>();
        using (var list = connection.CreateCommand())
        {
            list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var reader = list.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var counts = new Dictionary<string, (long, long)>();
        foreach (var table in tables)
        {
            using var columns = connection.CreateCommand();
            columns.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = 'IsDeleted'";
            if (Convert.ToInt64(columns.ExecuteScalar()) == 0)
            {
                continue;
            }

            using var count = connection.CreateCommand();
            count.CommandText = $"SELECT COUNT(*), COALESCE(SUM(IsDeleted), 0) FROM \"{table}\"";
            using var reader = count.ExecuteReader();
            reader.Read();
            counts[table] = (reader.GetInt64(0), reader.GetInt64(1));
        }

        return counts;
    }

    [Fact]
    public async Task Removes_exactly_the_soft_deleted_rows_of_every_table_and_keeps_the_live_ones()
    {
        await SampleAsync();
        // Sample data: 85 payments with 85 allocations, 15 tariff rates, 106 readings. Delete all allocations and payments,
        // all tariff rates and the first 10 readings (children only, so every live row keeps its parents).
        Run("UPDATE PaymentAllocations SET IsDeleted = 1");
        Run("UPDATE Payments SET IsDeleted = 1");
        Run("UPDATE TariffRates SET IsDeleted = 1");
        Run("UPDATE MeterReadings SET IsDeleted = 1 WHERE Id IN (SELECT Id FROM MeterReadings ORDER BY Id LIMIT 10)");
        var before = Counts();
        var expectedRemoved = before.Values.Sum(c => c.Deleted);
        Assert.Equal(85 + 85 + 15 + 10, expectedRemoved);

        var preview = await Cleaner.PreviewAsync();
        var result = await Cleaner.CleanAsync();

        Assert.Equal(expectedRemoved, preview.DeletedRecords);
        Assert.Equal(expectedRemoved, result.RecordsRemoved);
        Assert.Empty(result.KeptRecords);
        var after = Counts();
        foreach (var (table, (all, deleted)) in before)
        {
            Assert.Equal((all - deleted, 0L), after[table]);
        }

        Assert.Equal(0, after["PaymentAllocations"].All);
        Assert.Equal(0, after["Payments"].All);
        Assert.Equal(0, after["TariffRates"].All);
        Assert.Equal(96, after["MeterReadings"].All);
        Assert.Equal(94, after["Bills"].All);
    }

    [Fact]
    public async Task Rows_of_the_sync_outbox_that_point_at_removed_rows_go_with_them()
    {
        await SampleAsync();
        var outboxBefore = Scalar("SELECT COUNT(*) FROM SyncOutbox");
        var paymentEntries = Scalar("SELECT COUNT(*) FROM SyncOutbox WHERE TableName = 'Payments'");
        Assert.True(paymentEntries > 0);
        Run("UPDATE PaymentAllocations SET IsDeleted = 1");
        Run("UPDATE Payments SET IsDeleted = 1");

        await Cleaner.CleanAsync();

        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM SyncOutbox WHERE TableName = 'Payments'"));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM SyncOutbox WHERE TableName = 'PaymentAllocations'"));
        Assert.True(Scalar("SELECT COUNT(*) FROM SyncOutbox") < outboxBefore);
        // The purge itself must not queue anything (it bypasses the interceptor).
        Assert.Equal(outboxBefore - paymentEntries - 85, Scalar("SELECT COUNT(*) FROM SyncOutbox"));
    }

    [Fact]
    public async Task Everything_deleted_goes_children_before_parents_with_foreign_keys_on()
    {
        await SampleAsync();
        // Meters point at each other (ReplacedMeterId): a chain inside one table must go in one statement.
        Run("UPDATE Meters SET ReplacedMeterId = (SELECT MIN(Id) FROM Meters) WHERE Id = (SELECT MAX(Id) FROM Meters)");
        foreach (var table in Counts().Keys)
        {
            Run($"UPDATE \"{table}\" SET IsDeleted = 1");
        }

        var result = await Cleaner.CleanAsync();

        Assert.Empty(result.KeptRecords);
        Assert.All(Counts().Values, c => Assert.Equal(0, c.All));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
        Assert.True(result.DatabaseBytesAfter < result.DatabaseBytesBefore, $"{result.DatabaseBytesBefore} -> {result.DatabaseBytesAfter}");
        Assert.Equal(new FileInfo(_device.DatabasePath).Length, result.DatabaseBytesAfter);
        Assert.Null(result.CompactionError);
    }

    [Fact]
    public async Task A_live_row_that_still_refers_to_a_deleted_parent_is_kept_and_reported()
    {
        await SampleAsync();
        var suppliers = _device.Get<ISupplierStore>();
        var unused = new Supplier { Name = "Unused supplier" };
        await suppliers.SaveAsync(unused);
        var usedId = Text("SELECT SupplierId FROM Bills WHERE SupplierId IS NOT NULL AND IsDeleted = 0 LIMIT 1");
        var liveBills = Scalar($"SELECT COUNT(*) FROM Bills WHERE SupplierId = '{usedId}' AND IsDeleted = 0");
        Assert.True(liveBills > 0);
        var suppliersBefore = Counts()["Suppliers"].All;
        Run($"UPDATE Suppliers SET IsDeleted = 1 WHERE Id IN ('{usedId}', '{unused.Id.ToString().ToUpperInvariant()}')");

        var result = await Cleaner.CleanAsync();

        // The unused deleted supplier is removed; the used one stays (still marked deleted) and is reported, its bills are untouched.
        Assert.Equal(1, result.RecordsRemoved);
        Assert.Equal(suppliersBefore - 1, Counts()["Suppliers"].All);
        Assert.Equal(1, Counts()["Suppliers"].Deleted);
        Assert.Equal(liveBills, Scalar($"SELECT COUNT(*) FROM Bills WHERE SupplierId = '{usedId}' AND IsDeleted = 0"));
        var line = Assert.Single(result.KeptRecords);
        Assert.StartsWith("Suppliers: 1 deleted record(s) kept", line, StringComparison.Ordinal);
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task A_deleted_parent_of_a_kept_deleted_child_is_kept_too()
    {
        await SampleAsync();
        // Contract C is deleted but still has live bills, so it stays. Its supplier S2 is deleted and used by C only: it must stay as well.
        var suppliers = _device.Get<ISupplierStore>();
        var s2 = new Supplier { Name = "Only for one contract" };
        await suppliers.SaveAsync(s2);
        var contractId = Text("SELECT ContractId FROM Bills WHERE ContractId IS NOT NULL AND IsDeleted = 0 LIMIT 1");
        var s2Id = s2.Id.ToString().ToUpperInvariant();
        Run($"UPDATE Contracts SET SupplierId = '{s2Id}' WHERE Id = '{contractId}'");
        Run($"UPDATE Contracts SET IsDeleted = 1 WHERE Id = '{contractId}'");
        Run($"UPDATE Suppliers SET IsDeleted = 1 WHERE Id = '{s2Id}'");

        var result = await Cleaner.CleanAsync();

        Assert.Equal(0, result.RecordsRemoved);
        Assert.Contains(result.KeptRecords, k => k.StartsWith("Contracts:", StringComparison.Ordinal));
        Assert.Contains(result.KeptRecords, k => k.StartsWith("Suppliers:", StringComparison.Ordinal));
        Assert.Equal(0, Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task Attachment_files_of_deleted_and_missing_rows_are_removed_and_those_of_live_rows_kept()
    {
        await _device.InitializeAsync();
        var store = _device.Get<IAttachmentStore>();
        var owner = Guid.NewGuid();
        var live = await store.AddAsync(new Attachment { FileName = "live.jpg", ContentType = "image/jpeg", OwnerKind = AttachmentOwnerKind.MeterReading, OwnerId = owner }, new MemoryStream(new byte[1000]));
        var deleted = await store.AddAsync(new Attachment { FileName = "gone.jpg", ContentType = "image/jpeg", OwnerKind = AttachmentOwnerKind.MeterReading, OwnerId = owner }, new MemoryStream(new byte[3000]));
        await store.DeleteAsync(deleted.Id);
        var orphan = Guid.NewGuid() + ".pdf";
        _device.WriteAttachment(orphan, new string('x', 500));
        _device.WriteAttachment(Guid.NewGuid() + ".jpg.part", "download in progress");
        _device.WriteAttachment("notes.txt", "not ours");

        var preview = await Cleaner.PreviewAsync();

        // 1 deleted attachment row + 2 files (the deleted attachment's and the orphan) = 3000 + 500 bytes.
        Assert.Equal(new CleanUpPreview(1, 2, 3500), preview);

        var result = await Cleaner.CleanAsync();

        Assert.Equal(1, result.RecordsRemoved);
        Assert.Equal(2, result.FilesRemoved);
        Assert.Equal(3500, result.FileBytesRemoved);
        Assert.True(File.Exists(Path.Combine(_device.AttachmentsFolder, live.Id + ".jpg")));
        Assert.False(File.Exists(Path.Combine(_device.AttachmentsFolder, deleted.Id + ".jpg")));
        Assert.False(File.Exists(Path.Combine(_device.AttachmentsFolder, orphan)));
        Assert.Equal(3, Directory.GetFiles(_device.AttachmentsFolder).Length); // live file, ".part", notes.txt
        Assert.NotNull(await store.GetAsync(live.Id));
    }

    [Fact]
    public async Task Nothing_to_clean_is_reported_as_empty_and_changes_no_row()
    {
        await SampleAsync();
        var before = Counts();

        var preview = await Cleaner.PreviewAsync();
        var result = await Cleaner.CleanAsync();

        Assert.True(preview.IsEmpty);
        Assert.Equal(new CleanUpPreview(0, 0, 0), preview);
        Assert.Equal(0, result.RecordsRemoved);
        Assert.Equal(0, result.FilesRemoved);
        Assert.Equal(before, Counts());
    }

    [Fact]
    public async Task Preview_changes_nothing()
    {
        await SampleAsync();
        Run("UPDATE PaymentAllocations SET IsDeleted = 1");
        var before = Counts();
        var bytes = File.ReadAllBytes(_device.DatabasePath);

        var preview = await Cleaner.PreviewAsync();

        Assert.Equal(85, preview.DeletedRecords);
        Assert.Equal(before, Counts());
        Assert.Equal(bytes.Length, new FileInfo(_device.DatabasePath).Length);
    }

    [Fact]
    public async Task A_missing_database_has_nothing_to_clean()
    {
        var preview = await Cleaner.PreviewAsync();
        var result = await Cleaner.CleanAsync();

        Assert.True(preview.IsEmpty);
        Assert.Equal(0, result.RecordsRemoved);
    }

    private string Text(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)command.ExecuteScalar()!;
    }
}
