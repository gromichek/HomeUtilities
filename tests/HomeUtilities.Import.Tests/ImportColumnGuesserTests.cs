using HomeUtilities.Core.Common;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ImportColumnGuesserTests
{
    private static SheetData Headers(params string[] headers) =>
        new("S", [new SheetRow(1, [.. headers.Select(SheetCell.String)])], headers.Length);

    private static readonly Guid Cold = Guid.NewGuid();
    private static readonly Guid Hot = Guid.NewGuid();

    [Fact]
    public void Russian_headers_are_recognised()
    {
        var profile = ImportColumnGuesser.Guess(
            Headers("Месяц", "Холодная, показания", "Горячая, показания", "Сумма без НДС", "НДС %", "НДС", "Итого", "Оплачено", "Дата оплаты", "Примечание"),
            1,
            [Cold, Hot]);

        Assert.Equal((1, 7, 4, 5, 6, 8, 9), (profile.PeriodColumn, profile.TotalColumn, profile.NetColumn, profile.VatPercentColumn, profile.VatColumn, profile.PaidColumn, profile.PaidDateColumn));
        Assert.Equal([10], profile.NotesColumns);
        Assert.Equal([(Cold, 2), (Hot, 3)], profile.Registers.Select(r => (r.RegisterId, r.ReadingColumn)));
    }

    [Fact]
    public void Latvian_headers_are_recognised()
    {
        var profile = ImportColumnGuesser.Guess(
            Headers("Mēnesis", "Rādījums", "Bez PVN", "PVN %", "PVN", "Kopā", "Apmaksāts", "Piezīmes"),
            1,
            [Cold]);

        Assert.Equal((1, 6, 3, 4, 5, 7), (profile.PeriodColumn, profile.TotalColumn, profile.NetColumn, profile.VatPercentColumn, profile.VatColumn, profile.PaidColumn));
        Assert.Equal(2, Assert.Single(profile.Registers).ReadingColumn);
        Assert.Equal([8], profile.NotesColumns);
    }

    [Fact]
    public void English_headers_are_recognised_and_the_first_match_wins()
    {
        var profile = ImportColumnGuesser.Guess(
            Headers("Date", "Reading", "Total", "Paid", "Total (old)", "Comment"),
            1,
            [Cold]);

        Assert.Equal((1, 3, 4), (profile.PeriodColumn, profile.TotalColumn, profile.PaidColumn));
        Assert.Equal([6], profile.NotesColumns);
    }

    [Fact]
    public void More_reading_columns_than_registers_leave_the_extra_ones_unmapped()
    {
        var profile = ImportColumnGuesser.Guess(Headers("Date", "Reading A", "Reading B", "Total"), 1, [Cold]);

        Assert.Equal(2, Assert.Single(profile.Registers).ReadingColumn);
    }

    [Fact]
    public void Unknown_headers_and_a_missing_header_row_give_an_empty_guess()
    {
        var unknown = ImportColumnGuesser.Guess(Headers("A", "B"), 1, [Cold]);
        var missing = ImportColumnGuesser.Guess(Headers("Date"), 9, [Cold]);

        Assert.Equal(0, unknown.PeriodColumn);
        Assert.Empty(unknown.Registers);
        Assert.Equal((10, 0), (missing.FirstDataRow, missing.PeriodColumn));
    }

    [Fact]
    public void The_profile_file_round_trips_and_holds_no_values()
    {
        var profile = new ImportProfile
        {
            SheetName = "Water",
            PeriodColumn = 1,
            TotalColumn = 4,
            PaidMarker = "ok",
            CurrencyRule = ImportCurrencyRule.Eur,
            Registers = [new RegisterColumnMapping { RegisterId = Cold, ReadingColumn = 2, DateColumn = 3 }],
            NotesColumns = [5, 6],
        };

        var json = ImportProfileFile.Write("Water sheet", profile);
        var (name, read, error) = ImportProfileFile.Read(json);

        Assert.Null(error);
        Assert.Equal("Water sheet", name);
        Assert.Equal(("Water", 1, 4, "ok", ImportCurrencyRule.Eur), (read.SheetName, read.PeriodColumn, read.TotalColumn, read.PaidMarker, read.CurrencyRule));
        Assert.Equal((Cold, 2, 3), (read.Registers[0].RegisterId, read.Registers[0].ReadingColumn, read.Registers[0].DateColumn));
        Assert.Equal([5, 6], read.NotesColumns);
        Assert.Contains("\"Eur\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"version\":9,\"profile\":{}}")]
    [InlineData("[1,2]")]
    public void Bad_profile_files_are_rejected_without_an_exception(string json)
    {
        var (_, _, error) = ImportProfileFile.Read(json);

        Assert.Equal(ErrorCodes.ImportProfileFileInvalid, error?.Code);
    }

    [Fact]
    public void A_profile_without_a_name_takes_the_sheet_name_and_long_names_are_cut()
    {
        var nameless = ImportProfileFile.Read("{\"version\":1,\"profile\":{\"sheetName\":\"Heat\"}}");
        var longName = ImportProfileFile.Read(ImportProfileFile.Write(new string('x', 300), new ImportProfile()));

        Assert.Equal("Heat", nameless.Name);
        Assert.Equal(InputLimits.Name, longName.Name.Length);
    }

    [Theory]
    [InlineData("Water: 2014/15", "Water_ 2014_15.import-profile.json")]
    [InlineData("  ", "import-profile.import-profile.json")]
    public void File_names_are_safe(string name, string expected)
    {
        Assert.Equal(expected, ImportProfileFile.SuggestFileName(name));
    }
}
