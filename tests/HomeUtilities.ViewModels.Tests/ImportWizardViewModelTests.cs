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
    private readonly FakeEqualizedPlanStore _plans = new();
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

        return new ImportWizardViewModel(_files, _reader, _properties, _contracts, _meters, _bills, _plans, _profiles, _batches, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));
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

    private async Task<ImportWizardViewModel> ToPreviewStepAsync(bool detectReplacements = false)
    {
        var vm = await ToMappingStepAsync();
        vm.DetectReplacements = detectReplacements;
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
        var vm = new ImportWizardViewModel(_files, _reader, _properties, _contracts, _meters, _bills, _plans, _profiles, _batches, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));
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
        Assert.Equal("ImportSummary 2|1|1|1|1|1|0 [en]", vm.SummaryText);
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
        vm.DetectReplacements = false;

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Contains(vm.PlanRows[0].Issues, i => i.Text.Contains("Error_import_issue_duplicate_period"));
        Assert.Equal("ImportSummary 2|0|0|1|1|2|0 [en]", vm.SummaryText);
    }

    [Fact]
    public async Task Import_asks_once_then_stores_the_profile_and_the_batch_and_shows_the_result()
    {
        var vm = await ToPreviewStepAsync();
        vm.ProfileName = "Water sheet";

        await vm.ImportCommand.ExecuteAsync(null);

        var confirmation = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("ImportConfirm 2|1|1|0 [en]", confirmation.Message);
        var request = Assert.Single(_batches.Applied);
        Assert.Equal(("book.xlsx", "Water", _contract.Id), (request.FileName, request.SheetName, request.ContractId));
        Assert.Equal(new ImportCounts(2, 1, 1, 1), request.Plan.Counts);
        var saved = Assert.Single(_profiles.Stored);
        Assert.Equal(("Water sheet", saved.Id), ("Water sheet", request.ProfileId));
        Assert.Equal((1, 3, 4, _contract.Id), (saved.Profile.PeriodColumn, saved.Profile.TotalColumn, saved.Profile.PaidColumn, saved.Profile.ContractId));
        Assert.Equal(ImportWizardStep.Done, vm.Step);
        Assert.Equal("ImportDone 2|1|1|1|0 [en]", vm.ResultText);
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

    // ---- T-027: period rules, positions, replacements ----

    [Fact]
    public async Task The_period_rule_is_suggested_from_the_kind_of_service()
    {
        var vm = Create();
        _contract.Service = ServiceKind.Heating;
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportPeriodRule.PreviousMonth, vm.SelectedPeriodRule!.Value);
        Assert.False(vm.DateIsPaymentDate);
    }

    [Fact]
    public async Task A_fixed_amount_contract_suggests_previous_month_and_the_date_as_payment_date()
    {
        var vm = Create();
        _contract.Service = ServiceKind.Maintenance;
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal((ImportPeriodRule.PreviousMonth, true), (vm.SelectedPeriodRule!.Value, vm.DateIsPaymentDate));
    }

    [Fact]
    public async Task The_day_limit_is_shown_only_for_the_rule_that_uses_it()
    {
        var vm = await ToMappingStepAsync();

        vm.SelectedPeriodRule = vm.PeriodRules.Single(r => r.Value == ImportPeriodRule.PreviousMonthIfDayUpTo);
        Assert.True(vm.ShowDayLimit);

        vm.SelectedPeriodRule = vm.PeriodRules.Single(r => r.Value == ImportPeriodRule.MonthOfDate);
        Assert.False(vm.ShowDayLimit);
    }

    [Fact]
    public async Task A_bad_day_limit_is_reported_on_the_mapping_step()
    {
        var vm = await ToMappingStepAsync();
        vm.SelectedPeriodRule = vm.PeriodRules.Single(r => r.Value == ImportPeriodRule.PreviousMonthIfDayUpTo);
        vm.PeriodDayLimitText = "40";

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportWizardStep.Mapping, vm.Step);
        Assert.Contains("Error_import_day_limit_invalid", vm.MappingError);
    }

    [Fact]
    public async Task The_rule_changes_the_period_of_the_rows_and_is_saved_in_the_profile()
    {
        var vm = await ToMappingStepAsync();
        vm.SelectedPeriodRule = vm.PeriodRules.Single(r => r.Value == ImportPeriodRule.PreviousMonth);
        vm.AddRuleChangeCommand.Execute(null);
        var change = Assert.Single(vm.RuleChanges);
        change.FromDate = new DateTime(2014, 2, 1);
        change.SelectedRule = change.Rules.Single(r => r.Value == ImportPeriodRule.NextMonth);

        await vm.NextCommand.ExecuteAsync(null);
        await vm.ImportCommand.ExecuteAsync(null);

        // Rows dated 1 January, 1 February, 1 March: previous month (December), then next month from February (March, April).
        Assert.Equal(
            [new YearMonth(2013, 12), new YearMonth(2014, 3), new YearMonth(2014, 4)],
            _batches.Applied.Single().Plan.Rows.Select(r => r.Period!.Value));
        var saved = Assert.Single(_profiles.Stored).Profile;
        Assert.Equal(ImportPeriodRule.PreviousMonth, saved.PeriodRule);
        var savedChange = Assert.Single(saved.PeriodRuleChanges);
        Assert.Equal((new DateOnly(2014, 2, 1), ImportPeriodRule.NextMonth), (savedChange.FromDate, savedChange.Rule));
    }

    [Fact]
    public async Task A_rule_change_can_be_removed()
    {
        var vm = await ToMappingStepAsync();
        vm.AddRuleChangeCommand.Execute(null);

        vm.RuleChanges[0].RemoveCommand.Execute(null);

        Assert.Empty(vm.RuleChanges);
    }

    [Fact]
    public async Task The_saved_profile_records_the_headers_and_the_new_options()
    {
        var vm = await ToMappingStepAsync();
        vm.DateIsPaymentDate = true;
        vm.TotalFallbackPicker.Select(2);

        await vm.NextCommand.ExecuteAsync(null);
        await vm.ImportCommand.ExecuteAsync(null);

        var saved = Assert.Single(_profiles.Stored).Profile;
        Assert.True(saved.DateIsPaymentDate);
        Assert.Equal(2, saved.TotalFallbackColumn);
        Assert.Contains(saved.ColumnHeaders, h => h is { Field: "Total", Column: "C", Header: "Итого" });
        Assert.Contains(saved.ColumnHeaders, h => h.Field == $"Reading:{_meter.Registers[0].Id}" && h.Column == "B");
    }

    [Fact]
    public async Task A_saved_profile_brings_back_the_period_settings()
    {
        var vm = await ToMappingStepAsync();
        vm.SelectedPeriodRule = vm.PeriodRules.Single(r => r.Value == ImportPeriodRule.PreviousMonthIfDayUpTo);
        vm.PeriodDayLimitText = "12";
        vm.DetectReplacements = false;
        await vm.NextCommand.ExecuteAsync(null);
        await vm.ImportCommand.ExecuteAsync(null);

        var again = new ImportWizardViewModel(_files, _reader, _properties, _contracts, _meters, _bills, _plans, _profiles, _batches, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));
        await again.OnAppearingAsync();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await again.PickFileCommand.ExecuteAsync(null);
        await again.NextCommand.ExecuteAsync(null);
        await again.NextCommand.ExecuteAsync(null);

        Assert.Equal(ImportPeriodRule.PreviousMonthIfDayUpTo, again.SelectedPeriodRule!.Value);
        Assert.Equal("12", again.PeriodDayLimitText);
        Assert.False(again.DetectReplacements);
    }

    [Fact]
    public async Task A_chain_of_replaced_meters_is_one_position()
    {
        var vm = Create();
        var second = new Meter { PropertyId = _flat.Id, Resource = MeterResource.ColdWater, InstalledOn = new DateOnly(2010, 1, 1), ReplacedMeterId = _meter.Id };
        second.Registers.Add(MeterRegister.CreateDefault(second.Id));
        _meter.RemovedOn = new DateOnly(2010, 1, 1);
        _meters.Stored.Add(second);
        _contracts.Links.Add(new ContractMeter { ContractId = _contract.Id, MeterId = second.Id, ValidFrom = new DateOnly(2010, 1, 1) });
        await vm.OnAppearingAsync();
        _reader.Sheets["Water"] = Water();
        _files.Workbook = new ImportPickedFile("book.xlsx", new MemoryStream([1]));
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Single(vm.MeterLines);
        await vm.NextCommand.ExecuteAsync(null);
        Assert.Equal(_meter.Registers[0].Id, Assert.Single(vm.RegisterMappings).RegisterId);
    }

    [Fact]
    public async Task A_dropping_reading_is_proposed_as_a_replacement_the_owner_can_correct()
    {
        // The sheet has 10 → 5 → 12 in the cold column (dates 1 January, 1 February, 1 March 2014).
        var vm = await ToPreviewStepAsync(detectReplacements: true);

        var row = Assert.Single(vm.ReplacementRows);
        Assert.True(vm.HasReplacements);
        Assert.Equal(3, row.RowNumber);
        Assert.Equal(("10", "0"), (row.FinalText, row.InitialText));
        Assert.True(row.HasEstimate);
        Assert.Equal(1, vm.Plan!.Counts.Replacements);
        Assert.Contains("|1 [en]", vm.SummaryText);

        row.FinalText = "11,5";
        row.InitialText = "0.2";
        row.SerialText = "SN-77";
        await vm.RecalculateCommand.ExecuteAsync(null);

        var corrected = Assert.Single(vm.ReplacementRows);
        Assert.Equal(("11.5", "0.2", "SN-77"), (corrected.FinalText.Replace(',', '.'), corrected.InitialText.Replace(',', '.'), corrected.SerialText));
        Assert.False(corrected.HasEstimate);
        Assert.Equal("SN-77", vm.Plan!.Replacements.Single().NewMeter.SerialNumber);
    }

    [Fact]
    public async Task Importing_sends_the_replacements_with_the_plan()
    {
        var vm = await ToPreviewStepAsync(detectReplacements: true);

        await vm.ImportCommand.ExecuteAsync(null);

        var request = Assert.Single(_batches.Applied);
        Assert.Single(request.Plan.Replacements);
        Assert.Contains("ImportConfirm", Assert.Single(_dialogs.Confirmations).Message);
        Assert.EndsWith("|1 [en]", Assert.Single(_dialogs.Confirmations).Message);
    }

    [Fact]
    public async Task Switching_the_detection_off_proposes_no_replacements()
    {
        var vm = await ToPreviewStepAsync(detectReplacements: false);

        Assert.Empty(vm.ReplacementRows);
        Assert.False(vm.HasReplacements);
    }

    [Fact]
    public async Task The_period_of_a_row_can_be_corrected_and_is_kept_in_the_profile()
    {
        var vm = await ToPreviewStepAsync();
        var row = vm.PlanRows.First(r => r.Row.RowNumber == 2);
        Assert.True(row.CanEditPeriod);
        _dialogs.EditorScript = editor =>
        {
            var dialog = Assert.IsType<ImportRowOverrideEditorViewModel>(editor);
            dialog.Period = new DateTime(2013, 11, 15);
            dialog.Covers = true;
            dialog.Until = new DateTime(2013, 12, 1);
            dialog.Remark = " incl. connection fee ";
            dialog.SaveCommand.Execute(null);
        };

        await row.EditPeriodCommand.ExecuteAsync(null);

        var corrected = vm.PlanRows.First(r => r.Row.RowNumber == 2).Row;
        Assert.Equal(new YearMonth(2013, 11), corrected.Period);
        Assert.Equal(new DateOnly(2013, 12, 31), corrected.Bill!.ServiceTo);
        Assert.Equal("Примечание: ok; incl. connection fee", corrected.Bill.Notes);

        await vm.ImportCommand.ExecuteAsync(null);
        var saved = Assert.Single(_profiles.Stored).Profile;
        var stored = Assert.Single(saved.RowOverrides);
        Assert.Equal((Jan, new DateOnly(2013, 11, 1), new DateOnly(2013, 12, 1), "incl. connection fee"), (stored.RowDate, stored.Period!.Value, stored.CoversUntil!.Value, stored.Remark));
    }

    [Fact]
    public async Task Resetting_the_correction_of_a_row_removes_it()
    {
        var vm = await ToPreviewStepAsync();
        _dialogs.EditorScript = editor =>
        {
            var dialog = Assert.IsType<ImportRowOverrideEditorViewModel>(editor);
            dialog.Remark = "x";
            dialog.SaveCommand.Execute(null);
        };
        await vm.PlanRows.First(r => r.Row.RowNumber == 2).EditPeriodCommand.ExecuteAsync(null);
        Assert.NotNull(vm.PlanRows.First(r => r.Row.RowNumber == 2).Row.Override);

        _dialogs.EditorScript = editor => Assert.IsType<ImportRowOverrideEditorViewModel>(editor).ClearCommand.Execute(null);
        await vm.PlanRows.First(r => r.Row.RowNumber == 2).EditPeriodCommand.ExecuteAsync(null);

        Assert.Null(vm.PlanRows.First(r => r.Row.RowNumber == 2).Row.Override);
    }

    [Fact]
    public async Task A_last_covered_month_before_the_period_is_refused_by_the_dialog()
    {
        var vm = await ToPreviewStepAsync();
        var row = vm.PlanRows.First(r => r.Row.RowNumber == 2);
        ImportRowOverrideEditorViewModel? shown = null;
        _dialogs.EditorScript = editor =>
        {
            shown = Assert.IsType<ImportRowOverrideEditorViewModel>(editor);
            shown.Covers = true;
            shown.Until = shown.Period.AddMonths(-2);
            shown.SaveCommand.Execute(null);
        };

        await row.EditPeriodCommand.ExecuteAsync(null);

        Assert.False(shown!.Saved);
        Assert.Contains("ImportOverrideRangeInvalid", shown.RangeError);
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
            ReplacementCount = request.Plan.Counts.Replacements,
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
