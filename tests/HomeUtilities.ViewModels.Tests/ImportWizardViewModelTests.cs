using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.ViewModels.Tests;

public class ImportWizardViewModelTests
{
    private static readonly DateOnly Jan = new(2014, 1, 1);

    private readonly FakeLanguageService _language = new();
    private readonly FakeImportFileService _files = new();
    private readonly FakeWorkbookReader _reader = new();
    private readonly FakePropertyStore _properties = new();
    private readonly FakeContractStore _contracts = new();
    private readonly FakeMeterStore _meters = new();
    private readonly FakeBillStore _bills = new();
    private readonly FakeImportProfileStore _profiles = new();
    private readonly FakeImportBatchStore _batches = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeSnackbarService _snackbar = new();

    private Property _flat = new() { Name = "Flat" };
    private Contract _contract = new();
    private Meter _meter = new();

    private ImportWizardViewModel Create(bool withContract = true)
    {
        _properties.Stored.Add(_flat);
        if (withContract)
        {
            _contract = new Contract { PropertyId = _flat.Id, Title = "Water", ValidFrom = new DateOnly(2000, 1, 1), SupplierId = Guid.NewGuid(), CategoryId = Guid.NewGuid() };
            _contracts.Stored.Add(_contract);
            _meter = new Meter { PropertyId = _flat.Id, Resource = MeterResource.ColdWater, InstalledOn = new DateOnly(2000, 1, 1) };
            _meter.Registers.Add(MeterRegister.CreateDefault(_meter.Id));
            _meters.Stored.Add(_meter);
            _contracts.Links.Add(new ContractMeter { ContractId = _contract.Id, MeterId = _meter.Id, ValidFrom = new DateOnly(2000, 1, 1) });
        }

        return new ImportWizardViewModel(_files, _reader, _properties, _contracts, _meters, _bills, _profiles, _batches, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));
    }

    private static SheetData Water()
    {
        static SheetRow R(int n, params SheetCell[] cells) => new(n, cells);
        return new SheetData(
            "Water",
            [
                R(1, SheetCell.String("Месяц"), SheetCell.String("Холодная показания"), SheetCell.String("Итого"), SheetCell.String("Оплачено"), SheetCell.String("Примечание")),
                R(2, SheetCell.Date(Jan), SheetCell.Number(10m), SheetCell.Number(5m), SheetCell.String("***"), SheetCell.String("ok")),
                R(3, SheetCell.Date(Jan.AddMonths(1)), SheetCell.Number(5m), SheetCell.Number(6m), SheetCell.Empty, SheetCell.Empty),
                R(4, SheetCell.Date(Jan.AddMonths(2)), SheetCell.Number(12m), SheetCell.Empty, SheetCell.Empty, SheetCell.Empty),
            ],
            5);
    }

    private async Task<ImportWizardViewModel> ToTargetStepAsync()
    {
        var vm = Create();
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1, 2, 3]));
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        return vm;
    }

    private async Task<ImportWizardViewModel> ToMappingStepAsync()
    {
        var vm = await ToTargetStepAsync();
        await vm.NextCommand.ExecuteAsync(null);
        return vm;
    }

    private async Task<ImportWizardViewModel> ToPreviewStepAsync()
    {
        var vm = await ToMappingStepAsync();
        await vm.NextCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Picking_a_file_lists_its_sheets_and_a_single_sheet_is_selected()
    {
        var vm = Create();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.Equal(["Water"], vm.Sheets.Select(s => s.Name));
        Assert.Equal("Water", vm.SelectedSheet?.Name);
        Assert.Equal("ImportFileChosen book.xlsx [en]", vm.FileText);
    }

    [Fact]
    public async Task With_several_sheets_none_is_selected_and_Next_asks_for_one()
    {
        var vm = Create();
        _reader.Sheets["Water"] = Water();
        _reader.Sheets["Heat"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedSheet);
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportWizardStep.File, vm.Step);
        Assert.Equal("ImportChooseSheet [en]", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task A_file_that_is_not_a_workbook_is_reported_and_nothing_is_kept()
    {
        var vm = Create();
        _reader.Fail = true;
        _files.Workbook = new ImportPickedFile("notes.xlsx", new MemoryStream([1]));

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.Empty(vm.Sheets);
        Assert.Equal("ImportNotAWorkbook [en]", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Cancelling_the_file_picker_changes_nothing()
    {
        var vm = Create();

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.Empty(vm.Sheets);
        Assert.Empty(_dialogs.Alerts);
    }

    [Fact]
    public async Task The_target_step_lists_the_registers_of_the_contract_meters()
    {
        var vm = await ToTargetStepAsync();
        vm.SelectedProperty = vm.PropertyChoices[0];
        await vm.WhenLoadedAsync();
        vm.SelectedContract = vm.ContractChoices[0];
        await vm.WhenLoadedAsync();

        Assert.Equal(ImportWizardStep.Target, vm.Step);
        Assert.Single(vm.MeterLines);
        Assert.Contains("Enum_MeterResource_ColdWater", vm.MeterLines[0]);
        Assert.True(vm.HasMeters);
    }

    [Fact]
    public async Task Without_properties_the_target_step_says_to_create_them_first()
    {
        var vm = new ImportWizardViewModel(_files, _reader, _properties, _contracts, _meters, _bills, _profiles, _batches, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal("ImportNoProperties [en]", vm.TargetHintText);
    }

    [Fact]
    public async Task A_property_without_contracts_gets_the_no_contracts_hint()
    {
        var vm = Create(withContract: false);
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal("ImportNoContracts [en]", vm.TargetHintText);
    }

    [Fact]
    public async Task Next_without_a_contract_asks_for_one()
    {
        var vm = Create(withContract: false);
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportWizardStep.Target, vm.Step);
        Assert.Equal("ImportChooseContract [en]", _dialogs.Alerts.Last().Message);
    }

    [Fact]
    public async Task The_mapping_step_guesses_the_columns_from_the_header_texts()
    {
        var vm = await ToMappingStepAsync();

        Assert.Equal(ImportWizardStep.Mapping, vm.Step);
        Assert.Equal(1, vm.PeriodPicker.Column);
        Assert.Equal(3, vm.TotalPicker.Column);
        Assert.Equal(4, vm.PaidPicker.Column);
        Assert.Equal([5], vm.NotesColumns);
        var register = Assert.Single(vm.RegisterMappings);
        Assert.Equal(2, register.Reading.Column);
        Assert.Equal(0, register.Date.Column);
        Assert.Equal(0, vm.NetPicker.Column);
    }

    [Fact]
    public async Task The_column_options_show_letter_and_header_text()
    {
        var vm = await ToMappingStepAsync();

        Assert.Equal("ImportColumnNone [en]", vm.ColumnOptions[0].Text);
        Assert.Equal("A — Месяц", vm.ColumnOptions[1].Text);
        Assert.Equal(5, vm.PreviewHeader.Count);
        Assert.Equal(4, vm.PreviewRows.Count);
    }

    [Fact]
    public async Task Without_the_period_column_Next_stays_and_shows_the_validation_message()
    {
        var vm = await ToMappingStepAsync();
        vm.PeriodPicker.Select(0);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportWizardStep.Mapping, vm.Step);
        Assert.Contains("Error_import_period_column_required", vm.MappingError);
        Assert.True(vm.HasMappingError);
    }

    [Fact]
    public async Task Bad_row_numbers_are_reported_without_a_crash()
    {
        var vm = await ToMappingStepAsync();
        vm.FirstRowText = "abc";

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportWizardStep.Mapping, vm.Step);
        Assert.Contains("Error_import_rows_invalid", vm.MappingError);
    }

    [Fact]
    public async Task The_preview_shows_the_dry_run_summary_and_rows()
    {
        var vm = await ToPreviewStepAsync();

        Assert.Equal(ImportWizardStep.Preview, vm.Step);
        Assert.Equal("ImportSummary 2|1|1|1|1|1 [en]", vm.SummaryText);
        Assert.Equal(3, vm.PlanRows.Count);
        Assert.True(vm.CanImport);
        Assert.Contains(vm.PlanRows[1].Issues, i => i.IsError && i.Text.Contains("Error_import_issue_reading_lower_than_previous"));
        Assert.Contains(vm.PlanRows[2].Issues, i => !i.IsError && i.Text.Contains("Error_import_issue_total_missing"));
    }

    [Fact]
    public async Task The_filters_show_only_errors_or_only_warnings()
    {
        var vm = await ToPreviewStepAsync();

        vm.SelectedFilter = vm.Filters.Single(f => f.Value == ImportRowFilter.Errors);
        Assert.Equal([3], vm.PlanRows.Select(r => r.Row.RowNumber));

        vm.SelectedFilter = vm.Filters.Single(f => f.Value == ImportRowFilter.Warnings);
        Assert.Equal([4], vm.PlanRows.Select(r => r.Row.RowNumber));

        vm.SelectedFilter = vm.Filters.Single(f => f.Value == ImportRowFilter.All);
        Assert.Equal(3, vm.PlanRows.Count);
    }

    [Fact]
    public async Task Existing_bills_make_the_dry_run_skip_duplicates()
    {
        _bills.All.Add(new Bill { ContractId = null, Period = new YearMonth(2014, 1), Gross = 1 });
        var vm = await ToMappingStepAsync();
        _bills.All.Add(new Bill { ContractId = _contract.Id, Period = new YearMonth(2014, 1), Gross = 1 });

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Contains(vm.PlanRows[0].Issues, i => i.Text.Contains("Error_import_issue_duplicate_period"));
        Assert.Equal("ImportSummary 2|0|0|1|1|2 [en]", vm.SummaryText);
    }

    [Fact]
    public async Task Import_asks_once_then_stores_the_profile_and_the_batch_and_shows_the_result()
    {
        var vm = await ToPreviewStepAsync();
        vm.ProfileName = "Water sheet";

        await vm.ImportCommand.ExecuteAsync(null);

        var confirmation = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("ImportConfirm 2|1|1 [en]", confirmation.Message);
        var request = Assert.Single(_batches.Applied);
        Assert.Equal(("book.xlsx", "Water", _contract.Id), (request.FileName, request.SheetName, request.ContractId));
        Assert.Equal(new ImportCounts(2, 1, 1, 1), request.Plan.Counts);
        var saved = Assert.Single(_profiles.Stored);
        Assert.Equal(("Water sheet", saved.Id), ("Water sheet", request.ProfileId));
        Assert.Equal((1, 3, 4, _contract.Id), (saved.Profile.PeriodColumn, saved.Profile.TotalColumn, saved.Profile.PaidColumn, saved.Profile.ContractId));
        Assert.Equal(ImportWizardStep.Done, vm.Step);
        Assert.Equal("ImportDone 2|1|1|1 [en]", vm.ResultText);
        Assert.False(vm.CanImport);
    }

    [Fact]
    public async Task Declining_the_confirmation_imports_nothing()
    {
        var vm = await ToPreviewStepAsync();
        _dialogs.ConfirmResult = false;

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Empty(_batches.Applied);
        Assert.Empty(_profiles.Stored);
        Assert.Equal(ImportWizardStep.Preview, vm.Step);
    }

    [Fact]
    public async Task A_failing_import_goes_through_the_error_handler_and_stays_on_the_preview()
    {
        var vm = await ToPreviewStepAsync();
        _batches.ApplyFailure = new InvalidOperationException("disk full");

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ImportCommand.ExecuteAsync(null));

        Assert.Equal(ImportWizardStep.Preview, vm.Step);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Back_goes_one_step_back_and_is_hidden_on_the_first_and_last_steps()
    {
        var vm = await ToMappingStepAsync();
        Assert.True(vm.ShowBack);

        vm.BackCommand.Execute(null);

        Assert.Equal(ImportWizardStep.Target, vm.Step);
        vm.BackCommand.Execute(null);
        Assert.Equal(ImportWizardStep.File, vm.Step);
        Assert.False(vm.ShowBack);
    }

    [Fact]
    public async Task A_saved_profile_for_the_sheet_is_preselected_with_its_contract_and_columns()
    {
        var vm = Create();
        var saved = new SavedImportProfile
        {
            Name = "My water",
            Profile = new ImportProfile
            {
                SheetName = "Water",
                ContractId = _contract.Id,
                PeriodColumn = 1,
                TotalColumn = 3,
                PaidMarker = "ok",
                Registers = [new RegisterColumnMapping { RegisterId = _meter.Registers[0].Id, ReadingColumn = 2, DateColumn = 1 }],
            },
        };
        _profiles.Stored.Add(saved);
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal("My water", vm.SelectedProfile?.Profile?.Name);
        Assert.Equal("My water", vm.ProfileName);
        Assert.Equal("Water", vm.SelectedContract?.Name);
        Assert.Equal(ImportWizardStep.Mapping, vm.Step);
        Assert.Equal("ok", vm.PaidMarker);
        Assert.Equal(0, vm.PaidPicker.Column);
        Assert.Equal((2, 1), (vm.RegisterMappings[0].Reading.Column, vm.RegisterMappings[0].Date.Column));

        // Importing again with that profile updates it instead of making a second one.
        await vm.NextCommand.ExecuteAsync(null);
        await vm.ImportCommand.ExecuteAsync(null);
        Assert.Single(_profiles.Stored);
        Assert.Equal(saved.Id, _batches.Applied[0].ProfileId);
    }

    [Fact]
    public async Task The_notes_dialog_changes_the_notes_columns()
    {
        var vm = await ToMappingStepAsync();
        _dialogs.EditorScript = editor =>
        {
            var dialog = (MultiSelectDialogViewModel)editor;
            foreach (var item in dialog.Items)
            {
                item.IsSelected = item.Text.StartsWith("A —", StringComparison.Ordinal) || item.Text.StartsWith("E —", StringComparison.Ordinal);
            }

            dialog.ConfirmCommand.Execute(null);
        };

        await vm.ChooseNotesCommand.ExecuteAsync(null);

        Assert.Equal([1, 5], vm.NotesColumns);
    }

    [Fact]
    public async Task Show_contract_opens_the_contract_of_the_selected_property()
    {
        var vm = await ToTargetStepAsync();
        vm.SelectedProperty = vm.PropertyChoices[0];
        await vm.WhenLoadedAsync();
        vm.SelectedContract = vm.ContractChoices[0];

        await vm.ShowContractCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.ContractEdit, route);
        Assert.Equal(_flat.Id, parameters!["propertyId"]);
        Assert.Equal(_contract.Id, parameters["id"]);
    }

    [Fact]
    public async Task Open_imports_navigates_to_the_imports_list()
    {
        var vm = Create();

        await vm.OpenImportsCommand.ExecuteAsync(null);

        Assert.Equal(Routes.Imports, Assert.Single(_navigation.Visited).Route);
    }

    [Fact]
    public async Task The_texts_follow_the_language()
    {
        var vm = Create();
        _language.SetLanguage("ru");

        Assert.Equal("ImportNext [ru]", vm.NextText);
        Assert.Equal("ImportTitle [ru]", vm.Title);
    }

    // ---- fakes ----

    private sealed class FakeImportFileService : IImportFileService
    {
        public ImportPickedFile? Workbook { get; set; }

        public ImportPickedFile? Profile { get; set; }

        public Task<ImportPickedFile?> PickWorkbookAsync() => Task.FromResult(Workbook);

        public Task<ImportPickedFile?> PickProfileAsync() => Task.FromResult(Profile);
    }

    private sealed class FakeWorkbookReader : IWorkbookReader
    {
        public Dictionary<string, SheetData> Sheets { get; } = [];

        public bool Fail { get; set; }

        public IReadOnlyList<SheetInfo> ListSheets(Stream workbook) =>
            Fail ? throw new InvalidDataException("bad") : [.. Sheets.Values.Select(s => new SheetInfo(s.Name, s.RowCount, s.ColumnCount))];

        public SheetData ReadSheet(Stream workbook, string sheetName) =>
            Fail ? throw new InvalidDataException("bad") : Sheets[sheetName];
    }
}

internal sealed class FakeImportProfileStore : IImportProfileStore
{
    public List<SavedImportProfile> Stored { get; } = [];

    public Task<SavedImportProfile?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<SavedImportProfile>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SavedImportProfile>>([.. Stored.OrderBy(p => p.Name)]);

    public Task SaveAsync(SavedImportProfile aggregate, CancellationToken ct = default)
    {
        Stored.RemoveAll(p => p.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Stored.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeImportBatchStore : IImportBatchStore
{
    public List<ImportBatch> Batches { get; } = [];

    public List<ImportApplyRequest> Applied { get; } = [];

    public List<(Guid Id, bool Confirm)> UndoCalls { get; } = [];

    public Queue<UndoImportResult> UndoResults { get; } = new();

    public Exception? ApplyFailure { get; set; }

    public Task<IReadOnlyList<ImportBatch>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ImportBatch>>([.. Batches]);

    public Task<ImportBatch?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Batches.FirstOrDefault(b => b.Id == id));

    public Task<ImportBatch> ApplyAsync(ImportApplyRequest request, CancellationToken ct = default)
    {
        if (ApplyFailure is { } failure)
        {
            throw failure;
        }

        Applied.Add(request);
        var batch = new ImportBatch
        {
            FileName = request.FileName,
            SheetName = request.SheetName,
            ContractId = request.ContractId,
            ProfileId = request.ProfileId,
            ReadingCount = request.Plan.Counts.Readings,
            BillCount = request.Plan.Counts.Bills,
            PaymentCount = request.Plan.Counts.Payments,
            SkippedCount = request.Plan.Counts.SkippedRows,
        };
        Batches.Add(batch);
        return Task.FromResult(batch);
    }

    public Task<UndoImportResult> UndoAsync(Guid batchId, bool confirmEdited, CancellationToken ct = default)
    {
        UndoCalls.Add((batchId, confirmEdited));
        var result = UndoResults.Count > 0 ? UndoResults.Dequeue() : new UndoImportResult(UndoImportOutcome.Done, 0);
        if (result.Outcome == UndoImportOutcome.Done)
        {
            Batches.RemoveAll(b => b.Id == batchId);
        }

        return Task.FromResult(result);
    }
}
