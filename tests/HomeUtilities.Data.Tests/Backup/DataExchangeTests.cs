using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Data.Tests.Backup;

public partial class DataExchangeTests
{
    [Fact]
    public async Task Sample_data_survives_export_import_export_with_identical_json()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.FillSampleAsync();
        var firstProperty = (await source.Properties.ListAsync(includeArchived: true)).First();
        await source.Properties.DeleteAsync(firstProperty.Id);

        var first = await ExportTextAsync(source);

        using var target = new BackupFixture();
        var result = await target.DataExchange.StageImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(first)));
        Assert.True(result.IsValid, string.Join(", ", result.Issues));
        Assert.True(target.Backup.ApplyPendingRestore()!.Succeeded);
        await target.InitializeAsync();
        var second = await ExportTextAsync(target);

        Assert.Equal(WithoutExportTime(first), WithoutExportTime(second));
        Assert.Equal(result.RowCount, CountRows(first));
    }

    [Fact]
    public async Task Export_holds_every_table_the_soft_deleted_rows_and_the_documented_value_shapes()
    {
        using var device = new BackupFixture();
        await device.InitializeAsync();
        await device.FillSampleAsync();
        var deleted = (await device.Properties.ListAsync(includeArchived: true)).First();
        await device.Properties.DeleteAsync(deleted.Id);

        using var document = JsonDocument.Parse(await ExportTextAsync(device));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal("1.0.0", root.GetProperty("appVersion").GetString());
        Assert.Equal("2026-10-24T09:30:00.0000000Z", root.GetProperty("exportedAt").GetString());
        var tables = root.GetProperty("tables");
        var names = tables.EnumerateObject().Select(t => t.Name).ToList();
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        foreach (var expected in new[]
        {
            "Properties", "PropertyParameters", "Categories", "Suppliers", "Contracts", "ContractMeters", "Meters", "MeterRegisters",
            "MeterReadings", "VatRates", "VatRateVersions", "TariffComponents", "TariffRates", "EqualizedPlans", "Bills", "BillLines",
            "BillTemplates", "Payments", "PaymentAllocations", "Attachments", "ImportBatches", "ImportProfiles",
        })
        {
            Assert.Contains(expected, names);
        }

        foreach (var table in new[] { "Properties", "Suppliers", "Contracts", "Meters", "MeterReadings", "Bills", "Payments", "TariffRates", "EqualizedPlans", "BillTemplates", "Categories", "VatRates", "ImportProfiles" })
        {
            Assert.True(tables.GetProperty(table).GetArrayLength() > 0, table + " is empty");
        }

        var deletedRow = tables.GetProperty("Properties").EnumerateArray().Single(r => string.Equals(r.GetProperty("Id").GetString(), deleted.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.True(deletedRow.GetProperty("IsDeleted").GetBoolean());

        var bill = tables.GetProperty("Bills").EnumerateArray().First(b => b.GetProperty("IssuedOn").ValueKind == JsonValueKind.String);
        Assert.Equal(JsonValueKind.Number, bill.GetProperty("Gross").ValueKind);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", bill.GetProperty("IssuedOn").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$", bill.GetProperty("CreatedAt").GetString());
        Assert.Matches(@"^[0-9A-F]{8}-[0-9A-F]{4}-", bill.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task Export_with_attachments_is_a_zip_and_its_import_restores_the_files()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("Flat");
        source.WriteAttachment("a1.jpg", "photo one");
        using var zipStream = new MemoryStream();
        await source.DataExchange.ExportAsync(zipStream, withAttachments: true);
        zipStream.Position = 0;
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            Assert.Equal(["attachments/a1.jpg", "data.json"], zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        }

        using var target = new BackupFixture();
        var result = await target.DataExchange.StageImportAsync(zipStream);
        Assert.True(result.IsValid);
        Assert.True(target.Backup.ApplyPendingRestore()!.Succeeded);
        await target.InitializeAsync();
        Assert.Equal(["Flat"], (await target.Properties.ListAsync()).Select(p => p.Name));
        Assert.Equal("photo one", File.ReadAllText(Path.Combine(target.AttachmentsFolder, "a1.jpg")));
    }

    [Fact]
    public async Task A_file_of_a_newer_format_version_is_refused_and_nothing_is_staged()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        var json = (await ExportTextAsync(source)).Replace("\"formatVersion\": 1", "\"formatVersion\": 2", StringComparison.Ordinal);

        using var target = new BackupFixture();
        var result = await target.DataExchange.StageImportAsync(ToStream(json));

        Assert.False(result.IsValid);
        Assert.Equal(DataImportIssueCode.NewerFormat, Assert.Single(result.Issues).Code);
        AssertNothingStaged(target);
    }

    [Fact]
    public async Task A_file_from_a_newer_database_version_is_refused()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        var json = LastMigration().Replace(await ExportTextAsync(source), "\"lastMigration\": \"99991231000000_Future\"");

        using var target = new BackupFixture();
        var result = await target.DataExchange.ValidateAsync(ToStream(json));

        Assert.Equal(DataImportIssueCode.NewerFormat, Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("this is not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"hello\": 1 }")]
    [InlineData("{ \"formatVersion\": \"one\", \"tables\": {} }")]
    public async Task Files_that_are_not_data_exports_are_refused(string content)
    {
        using var target = new BackupFixture();

        var result = await target.DataExchange.StageImportAsync(ToStream(content));

        Assert.Equal(DataImportIssueCode.NotADataFile, Assert.Single(result.Issues).Code);
        AssertNothingStaged(target);
    }

    [Fact]
    public async Task A_zip_without_data_json_is_refused_even_if_it_is_a_normal_backup()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        using var backupZip = await source.BackupToMemoryAsync();
        using var target = new BackupFixture();

        var result = await target.DataExchange.StageImportAsync(backupZip);

        Assert.Equal(DataImportIssueCode.NotADataFile, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task Bad_content_is_reported_with_table_row_and_column_and_nothing_is_staged()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("Flat");
        var json = await ExportTextAsync(source);
        var broken = json
            .Replace("\"IsDeleted\": false", "\"IsDeleted\": \"no\"", StringComparison.Ordinal)
            .Replace("\"tables\": {", "\"tables\": {\n\"Mystery\": [],", StringComparison.Ordinal);

        using var target = new BackupFixture();
        var result = await target.DataExchange.StageImportAsync(ToStream(broken));

        Assert.False(result.IsValid);
        Assert.Contains(new DataImportIssue(DataImportIssueCode.UnknownTable, "Mystery"), result.Issues);
        Assert.Contains(result.Issues, i => i.Code == DataImportIssueCode.BadValue && i.Table is not null && i.Column == "IsDeleted");
        AssertNothingStaged(target);
    }

    [Fact]
    public async Task A_missing_required_column_and_an_unknown_column_are_reported()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("Flat");
        var json = await ExportTextAsync(source);
        var broken = Regex.Replace(json, "\"Name\": \"Flat\",", "\"Colour\": \"red\",", RegexOptions.None);

        using var target = new BackupFixture();
        var result = await target.DataExchange.ValidateAsync(ToStream(broken));

        Assert.Contains(result.Issues, i => i is { Code: DataImportIssueCode.MissingColumn, Table: "Properties", Column: "Name" });
        Assert.Contains(result.Issues, i => i is { Code: DataImportIssueCode.UnknownColumn, Table: "Properties", Column: "Colour" });
    }

    [Fact]
    public async Task Rows_that_break_a_database_rule_are_refused_and_nothing_is_staged()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.AddPropertiesAsync("Flat");
        var node = System.Text.Json.Nodes.JsonNode.Parse(await ExportTextAsync(source))!;
        var properties = node["tables"]!["Properties"]!.AsArray();
        properties.Add(System.Text.Json.Nodes.JsonNode.Parse(properties[0]!.ToJsonString()));

        using var target = new BackupFixture();
        var result = await target.DataExchange.StageImportAsync(ToStream(node.ToJsonString()));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(DataImportIssueCode.ConstraintViolation, issue.Code);
        Assert.Equal("Properties", issue.Table);
        AssertNothingStaged(target);
    }

    [Fact]
    public async Task A_reference_to_a_missing_row_is_refused()
    {
        using var source = new BackupFixture();
        await source.InitializeAsync();
        await source.FillSampleAsync();
        var node = System.Text.Json.Nodes.JsonNode.Parse(await ExportTextAsync(source))!;
        var contracts = node["tables"]!["Contracts"]!.AsArray();
        contracts[0]!["PropertyId"] = Guid.NewGuid().ToString().ToUpperInvariant();

        using var target = new BackupFixture();
        var result = await target.DataExchange.StageImportAsync(ToStream(node.ToJsonString()));

        Assert.Equal(DataImportIssueCode.ConstraintViolation, Assert.Single(result.Issues).Code);
        AssertNothingStaged(target);
    }

    private static async Task<string> ExportTextAsync(BackupFixture device)
    {
        using var stream = new MemoryStream();
        await device.DataExchange.ExportAsync(stream, withAttachments: false);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));

    private static string WithoutExportTime(string json) => ExportedAt().Replace(json, string.Empty);

    private static int CountRows(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("tables").EnumerateObject().Sum(t => t.Value.GetArrayLength());
    }

    private static void AssertNothingStaged(BackupFixture device) =>
        Assert.False(File.Exists(Path.Combine(device.Options.RestoreFolder, "pending.zip")));

    [GeneratedRegex("\"exportedAt\": \"[^\"]*\",?")]
    private static partial Regex ExportedAt();

    [GeneratedRegex("\"lastMigration\": \"[^\"]*\"")]
    private static partial Regex LastMigration();
}
