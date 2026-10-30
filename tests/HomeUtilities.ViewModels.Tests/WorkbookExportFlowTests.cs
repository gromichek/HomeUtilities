using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Export;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.ViewModels.Tests;

public class WorkbookExportFlowTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSnackbarService _snackbar = new();
    private readonly FakePaymentStore _payments = new();
    private readonly FakeSupplierStore _suppliers = new();
    private readonly FakeImportProfileStore _profiles = new();
    private readonly FakeWriter _writer = new();
    private readonly FakeSaver _saver = new();
    private readonly FakeLoader _loader = new();
    private readonly Property _flat = new() { Name = "Flat" };
    private readonly Supplier _aqua = new() { Name = "Aqua" };
    private readonly Contract _water;
    private readonly Contract _power;

    public WorkbookExportFlowTests()
    {
        _suppliers.Stored.Add(_aqua);
        _water = NewContract("Water");
        _power = NewContract("Power");
        var bills = new[] { NewBill(_water, new YearMonth(2026, 1), 20m), NewBill(_power, new YearMonth(2026, 1), 30m) };
        _loader.Data = new ReportData([_flat], [], [_water, _power], [], [], bills, [], []);
    }

    private Contract NewContract(string title) => new()
    {
        PropertyId = _flat.Id,
        SupplierId = _aqua.Id,
        CategoryId = Guid.NewGuid(),
        Title = title,
        ValidFrom = new DateOnly(2025, 1, 1),
    };

    private Bill NewBill(Contract contract, YearMonth period, decimal gross) => new()
    {
        PropertyId = _flat.Id,
        CategoryId = contract.CategoryId,
        ContractId = contract.Id,
        Period = period,
        Gross = gross,
    };

    private WorkbookExportFlow Create() => new(
        _loader, _payments, _suppliers, _profiles, _writer, _saver, _dialogs, _snackbar, new FakeLocalizer(_language), new TestClock(new DateOnly(2026, 10, 29)));

    // Answers the dialogs one after the other: contracts, period, currency.
    private void Answer(Action<MultiSelectDialogViewModel>? contracts = null, Action<ReportPeriodDialogViewModel>? period = null, Action<SingleChoiceDialogViewModel>? currency = null)
    {
        _dialogs.EditorScript = editor =>
        {
            switch (editor)
            {
                case MultiSelectDialogViewModel multi:
                    contracts?.Invoke(multi);
                    multi.ConfirmCommand.Execute(null);
                    break;
                case ReportPeriodDialogViewModel range:
                    period?.Invoke(range);
                    range.ConfirmCommand.Execute(null);
                    break;
                case SingleChoiceDialogViewModel choice:
                    currency?.Invoke(choice);
                    choice.ConfirmCommand.Execute(null);
                    break;
            }
        };
    }

    [Fact]
    public async Task Everything_is_exported_by_default_and_saved_with_the_dated_file_name()
    {
        Answer(period: p => p.Presets[3].ChooseCommand.Execute(null));

        await Create().RunAsync();

        var save = Assert.Single(_saver.Saved);
        Assert.Equal(("HomeUtilities-export-2026-10-29.xlsx", WorkbookExportFlow.ContentType, "Exports"), (save.FileName, save.ContentType, save.Folder));
        Assert.Equal(["ExportTotalSheet en", "Aqua Power", "Aqua Water"], _writer.Sheets.Select(s => s.Name));
        Assert.Equal(["ExportSaved [en]"], _snackbar.Messages);
        Assert.Equal(3, _dialogs.EditorsShown);
    }

    [Fact]
    public async Task Only_the_chosen_contracts_get_a_sheet()
    {
        Answer(contracts: m => m.Items.First(i => i.Text.Contains("Power", StringComparison.Ordinal)).IsSelected = false, period: p => p.Presets[3].ChooseCommand.Execute(null));

        await Create().RunAsync();

        Assert.Equal(["ExportTotalSheet en", "Aqua Water"], _writer.Sheets.Select(s => s.Name));
    }

    [Fact]
    public async Task The_contract_list_names_property_supplier_and_title()
    {
        Answer(contracts: m => Assert.Equal(["Flat · Aqua Power", "Flat · Aqua Water"], m.Items.Select(i => i.Text)));

        await Create().RunAsync();

        Assert.NotNull(_dialogs.LastEditor);
    }

    [Fact]
    public async Task A_preset_limits_the_period_and_the_EUR_choice_converts_amounts()
    {
        var old = NewBill(_water, new YearMonth(2013, 12), 12.34m);
        old.Currency = Currency.LVL;
        _loader.Data = new ReportData([_flat], [], [_water], [], [], [old, NewBill(_water, new YearMonth(2026, 1), 20m)], [], []);
        Answer(period: p => p.Presets[0].ChooseCommand.Execute(null), currency: c => c.Items[1].ChooseCommand.Execute(null));

        await Create().RunAsync();

        // "This year" (2026) leaves out December 2013; the standard layout (no meters) has the bill total in column 7.
        var water = _writer.Sheets.Single(s => s.Name == "Aqua Water");
        Assert.Equal(20m, water.At(2, 7)?.Number);
        Assert.Null(water.At(3, 7));
    }

    [Fact]
    public async Task Cancelling_any_dialog_saves_nothing()
    {
        _dialogs.EditorScript = null;

        await Create().RunAsync();

        Assert.Empty(_saver.Saved);
        Assert.Equal(1, _dialogs.EditorsShown);
    }

    [Fact]
    public async Task Without_contracts_the_owner_is_told_there_is_nothing_to_export()
    {
        _loader.Data = new ReportData([_flat], [], [], [], [], [], [], []);

        await Create().RunAsync();

        Assert.Equal("ExportNothing [en]", Assert.Single(_dialogs.Alerts).Message);
        Assert.Empty(_saver.Saved);
    }

    [Fact]
    public async Task A_period_without_data_is_reported_instead_of_saving_an_empty_file()
    {
        Answer(period: p => p.Presets[2].ChooseCommand.Execute(null));

        await Create().RunAsync();

        Assert.Equal("ExportNothing [en]", Assert.Single(_dialogs.Alerts).Message);
        Assert.Empty(_saver.Saved);
    }

    [Fact]
    public async Task The_location_is_shown_when_the_platform_reports_one()
    {
        _saver.Location = "Documents";
        Answer(period: p => p.Presets[3].ChooseCommand.Execute(null));

        await Create().RunAsync();

        Assert.Equal(["ExportSavedTo Documents [en]"], _snackbar.Messages);
    }

    private sealed class FakeLoader : IReportDataLoader
    {
        public ReportData Data { get; set; } = null!;

        public Task<ReportData> LoadAsync(CancellationToken ct = default) => Task.FromResult(Data);
    }

    private sealed class FakeWriter : IExportWorkbookWriter
    {
        public IReadOnlyList<ExportSheet> Sheets { get; private set; } = [];

        public void Write(IReadOnlyList<ExportSheet> sheets, Stream stream) => Sheets = sheets;
    }

    private sealed class FakeSaver : IFileSaveService
    {
        public List<(string FileName, string ContentType, string Folder)> Saved { get; } = [];

        public string? Location { get; set; }

        public async Task<FileSaveResult> SaveAsync(string fileName, string contentType, string folder, Func<Stream, Task> write)
        {
            using var stream = new MemoryStream();
            await write(stream);
            Saved.Add((fileName, contentType, folder));
            return new FileSaveResult(true, Location);
        }
    }
}
