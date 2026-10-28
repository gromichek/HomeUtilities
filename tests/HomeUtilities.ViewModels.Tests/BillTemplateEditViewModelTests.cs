using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The bill template editor and the Bill templates list. Today is 15 Oct 2026; FakeLocalizer renders texts as "key a|b [en]".</summary>
public class BillTemplateEditViewModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public BillTemplateEditViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private async Task<BillTemplateEditViewModel> NewEditorAsync()
    {
        var vm = _kit.Kit.CreateTemplateEditor();
        await vm.LoadAsync(null);
        return vm;
    }

    private async Task<BillTemplateEditViewModel> OpenAsync(Guid id)
    {
        var vm = _kit.Kit.CreateTemplateEditor();
        await vm.LoadAsync(id);
        return vm;
    }

    /// <summary>Fills the editor the way the owner would for the home insurance: yearly in March, 120,00, due after 14 days.</summary>
    private void FillInsurance(BillTemplateEditViewModel vm)
    {
        vm.TemplateName = "Insurance";
        vm.SelectedCategory = new ChoiceOption(_kit.Kit.CategoryIdFor(ServiceKind.Insurance), "Insurance", "#112233");
        vm.SelectedKindIndex = vm.Kinds.ToList().FindIndex(k => k.Value.Equals(ScheduleKind.Yearly));
        vm.StartMonth = new YearMonth(2026, 3);
        vm.Amounts.GrossText = "120,00";
        vm.DayText = "10";
        vm.DueOffsetText = "14";
    }

    [Fact]
    public async Task A_new_template_starts_monthly_in_arrears_from_this_month_with_the_only_property()
    {
        var vm = await NewEditorAsync();

        Assert.True(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Equal("Sample flat", vm.SelectedPropertyName);
        Assert.Equal(ScheduleKind.Monthly, vm.SelectedKind!.As<ScheduleKind>());
        Assert.Equal(BillingTiming.InArrears, vm.SelectedTiming!.As<BillingTiming>());
        Assert.Equal(new YearMonth(2026, 10), vm.StartMonth);
        Assert.True(vm.IsActive);
        Assert.Equal("BillTemplateNewTitle [en]", vm.Title);
    }

    [Fact]
    public async Task The_preview_lists_the_next_three_drafts_with_their_dates()
    {
        var vm = await NewEditorAsync();
        vm.StartMonth = new YearMonth(2026, 1);
        vm.DayText = "5";
        vm.DueOffsetText = "14";

        // in arrears on 15.10.2026 the next periods are 10, 11, 12.2026 (issued the month after)
        Assert.StartsWith("BillTemplatePreview ", vm.PreviewText, StringComparison.Ordinal);
        Assert.Contains("BillTemplatePreviewIssuedDue 10.2026|05.11.2026|19.11.2026", vm.PreviewText, StringComparison.Ordinal);
        Assert.Contains("BillTemplatePreviewIssuedDue 12.2026|05.01.2027|19.01.2027", vm.PreviewText, StringComparison.Ordinal);
        Assert.True(vm.HasPreview);
    }

    [Fact]
    public async Task The_preview_follows_the_timing_and_hides_when_the_input_is_unreadable()
    {
        var vm = await NewEditorAsync();
        vm.StartMonth = new YearMonth(2026, 1);
        vm.DayText = "5";

        // prepaid on 15.10.2026: 11.2026 is already due, the next draft is for 12.2026 (issued 05.11.2026)
        vm.SelectedTimingIndex = vm.Timings.ToList().FindIndex(t => t.Value.Equals(BillingTiming.InAdvance));
        Assert.Contains("BillTemplatePreviewIssued 12.2026|05.11.2026", vm.PreviewText, StringComparison.Ordinal);

        vm.DayText = "abc";
        Assert.Equal(string.Empty, vm.PreviewText);
    }

    [Fact]
    public async Task A_once_template_has_no_period_fields_and_its_own_preview()
    {
        var vm = await NewEditorAsync();

        vm.SelectedKindIndex = vm.Kinds.ToList().FindIndex(k => k.Value.Equals(ScheduleKind.Once));

        Assert.False(vm.ShowPeriodFields);
        Assert.Equal("BillTemplatePreviewOnce [en]", vm.PreviewText);
    }

    [Fact]
    public async Task Every_n_months_shows_the_number_field()
    {
        var vm = await NewEditorAsync();

        vm.SelectedKindIndex = vm.Kinds.ToList().FindIndex(k => k.Value.Equals(ScheduleKind.EveryNMonths));

        Assert.True(vm.ShowEveryMonths);
        Assert.True(vm.ShowPeriodFields);
    }

    [Fact]
    public async Task Save_stores_the_template_and_asks_the_store_for_the_drafts_that_are_due()
    {
        var vm = await NewEditorAsync();
        FillInsurance(vm);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Templates.All);
        Assert.Equal(("Insurance", 120m, 14), (saved.Name, saved.Gross, saved.DueDayOffset));
        Assert.Equal(_kit.Kit.Property.Id, saved.PropertyId);
        Assert.Equal((ScheduleKind.Yearly, new YearMonth(2026, 3), 10), (saved.Schedule.Kind, saved.Schedule.StartMonth, saved.Schedule.DayOfMonth));
        Assert.Null(saved.ContractId);
        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Equal([Today], _kit.Templates.Generated);
    }

    [Fact]
    public async Task Save_with_nothing_filled_in_shows_the_required_fields()
    {
        var vm = _kit.Kit.CreateTemplateEditor();
        _kit.Kit.Properties.Stored.Add(new Property { Name = "Second", Kind = PropertyKind.House });
        await vm.LoadAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Templates.All);
        Assert.False(string.IsNullOrEmpty(vm.NameError));
        Assert.False(string.IsNullOrEmpty(vm.PropertyError));
        Assert.False(string.IsNullOrEmpty(vm.CategoryError));
    }

    [Fact]
    public async Task Nonsense_numbers_are_errors_not_crashes()
    {
        var vm = await NewEditorAsync();
        FillInsurance(vm);
        vm.Amounts.GrossText = "1x";
        vm.DayText = "32";
        vm.DueOffsetText = "-3";
        vm.RemindText = "soon";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Templates.All);
        Assert.False(string.IsNullOrEmpty(vm.GrossError));
        Assert.False(string.IsNullOrEmpty(vm.DayError));
        Assert.False(string.IsNullOrEmpty(vm.DueOffsetError));
        Assert.False(string.IsNullOrEmpty(vm.RemindError));
    }

    [Fact]
    public async Task Empty_amounts_are_allowed()
    {
        var vm = await NewEditorAsync();
        FillInsurance(vm);
        vm.Amounts.GrossText = string.Empty;

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Templates.All);
        Assert.Null(saved.Gross);
        Assert.Null(saved.Net);
    }

    [Fact]
    public async Task Net_and_vat_percent_are_enough_for_an_amount()
    {
        var vm = await NewEditorAsync();
        FillInsurance(vm);
        vm.Amounts.GrossText = string.Empty;
        vm.Amounts.NetText = "100";
        vm.Amounts.VatPercentText = "21";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Templates.All);
        Assert.Equal((100m, 21m, 121m), (saved.Net, saved.VatPercent, saved.Gross));
    }

    [Fact]
    public async Task An_end_month_before_the_start_is_an_error()
    {
        var vm = await NewEditorAsync();
        FillInsurance(vm);
        vm.HasEnd = true;
        vm.EndMonth = new YearMonth(2026, 1);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Templates.All);
        Assert.False(string.IsNullOrEmpty(vm.EndError));
    }

    [Fact]
    public async Task A_saved_template_opens_again_with_everything_it_has()
    {
        var contract = _kit.AddWaterContract("Internet", tariff: false);
        var template = new BillTemplate
        {
            Name = "Internet + TV",
            PropertyId = _kit.Kit.Property.Id,
            CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.InternetTv),
            ContractId = contract.Id,
            SupplierId = _kit.Supplier.Id,
            Title = "Internet",
            Gross = 22.95m,
            Timing = BillingTiming.InAdvance,
            DueDayOffset = 14,
            RemindDaysBefore = 3,
            IsActive = false,
            Notes = "Invented.",
            Lines = [new BillTemplateLine { Description = "TV", Gross = 5m }],
            Schedule = new Schedule { Kind = ScheduleKind.EveryNMonths, EveryMonths = 3, StartMonth = new YearMonth(2026, 2), EndMonth = new YearMonth(2027, 2), DayOfMonth = 5 },
        };
        template.CustomFields = [new CustomFieldDefinition { OwnerId = template.Id, Name = "Ref", Kind = CustomFieldKind.Text }];
        _kit.Templates.All.Add(template);
        _kit.Kit.Suppliers.Stored.Add(_kit.Supplier);

        var vm = await OpenAsync(template.Id);

        Assert.False(vm.IsNew);
        Assert.Equal(("Internet + TV", "Internet", "Invented."), (vm.TemplateName, vm.BillTitle, vm.Notes));
        Assert.False(vm.IsActive);
        Assert.Equal("Internet", vm.SelectedContractName);
        Assert.Equal("Water utility Ltd", vm.SelectedSupplierName);
        Assert.Equal("22.95", vm.Amounts.GrossText);
        Assert.Equal(ScheduleKind.EveryNMonths, vm.SelectedKind!.As<ScheduleKind>());
        Assert.Equal("3", vm.EveryMonthsText);
        Assert.True(vm.HasEnd);
        Assert.Equal((new YearMonth(2026, 2), new YearMonth(2027, 2)), (vm.StartMonth, vm.EndMonth));
        Assert.Equal("5", vm.DayText);
        Assert.Equal(BillingTiming.InAdvance, vm.SelectedTiming!.As<BillingTiming>());
        Assert.Equal(("14", "3"), (vm.DueOffsetText, vm.RemindText));
        Assert.Equal(["TV"], vm.LineRows.Select(l => l.Description));
        Assert.Equal(["Ref"], vm.CustomFields.Select(f => f.Name));
        Assert.False(vm.IsDirty);
        Assert.Equal("BillTemplateEditTitle Internet + TV [en]", vm.Title);
    }

    [Fact]
    public async Task Choosing_a_contract_takes_its_category_supplier_and_timing()
    {
        var contract = _kit.AddWaterContract("Internet", tariff: false, timing: BillingTiming.InAdvance);
        _kit.Kit.Suppliers.Stored.Add(_kit.Supplier);
        var vm = await NewEditorAsync();
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains("Internet") ? "Internet" : null;

        await vm.ChooseContractCommand.ExecuteAsync(null);

        Assert.Equal("Internet", vm.SelectedContractName);
        Assert.Equal("Enum_ServiceKind_ColdWater [en]", vm.SelectedCategoryName);
        Assert.Equal("Water utility Ltd", vm.SelectedSupplierName);
        Assert.Equal(BillingTiming.InAdvance, vm.SelectedTiming!.As<BillingTiming>());
        Assert.True(vm.IsDirty);
        Assert.Equal(contract.Id, vm.SelectedContract!.Id);
    }

    [Fact]
    public async Task A_template_from_a_contract_starts_with_its_data_and_the_newest_bill()
    {
        var contract = _kit.AddWaterContract("Internet", tariff: false, timing: BillingTiming.InAdvance);
        contract.CustomFields = [new CustomFieldDefinition { OwnerId = contract.Id, Name = "Reference", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.Bill }];
        var bill = _kit.AddBill(contract, new YearMonth(2026, 9), 22.95m, due: new DateOnly(2026, 9, 19));
        bill.IssuedOn = new DateOnly(2026, 9, 5);
        _kit.Kit.Suppliers.Stored.Add(_kit.Supplier);

        var vm = _kit.Kit.CreateTemplateEditor();
        vm.PresetContractId = contract.Id;
        await vm.LoadAsync(null);

        Assert.True(vm.IsNew);
        Assert.Equal(("Internet", "Internet"), (vm.TemplateName, vm.SelectedContractName));
        Assert.Equal("Water utility Ltd", vm.SelectedSupplierName);
        Assert.Equal("22.95", vm.Amounts.GrossText);
        Assert.Equal(("5", "14"), (vm.DayText, vm.DueOffsetText));
        Assert.Equal(BillingTiming.InAdvance, vm.SelectedTiming!.As<BillingTiming>());
        Assert.Equal(["Reference"], vm.CustomFields.Select(f => f.Name));
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task A_template_from_a_one_off_bill_is_a_once_template_with_its_lines()
    {
        var bill = Bill.CreateOneOff(_kit.Kit.Property.Id, _kit.Kit.CategoryIdFor(ServiceKind.Maintenance), "Boiler repair", new YearMonth(2026, 6), _kit.Kit.Clock);
        bill.Gross = 186.45m;
        bill.Lines = [new BillLine { Description = "Thermostat", Gross = 120m }, new BillLine { Description = "Service call", Gross = 66.45m }];
        _kit.Bills.All.Add(bill);

        var vm = _kit.Kit.CreateTemplateEditor();
        vm.PresetBillId = bill.Id;
        await vm.LoadAsync(null);

        Assert.Equal("Boiler repair", vm.TemplateName);
        Assert.Equal(ScheduleKind.Once, vm.SelectedKind!.As<ScheduleKind>());
        Assert.Equal("186.45", vm.Amounts.GrossText);
        Assert.Equal(["Thermostat", "Service call"], vm.LineRows.Select(l => l.Description));
        Assert.Equal("Enum_ServiceKind_Maintenance [en]", vm.SelectedCategoryName);
    }

    [Fact]
    public async Task Lines_and_custom_fields_are_added_edited_and_saved()
    {
        var vm = await NewEditorAsync();
        FillInsurance(vm);

        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var dialog = Assert.IsType<BillLineEditorViewModel>(editor);
            dialog.Description = "Building";
            dialog.Amounts.GrossText = "80";
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddLineCommand.ExecuteAsync(null);
        _kit.Kit.Dialogs.EditorScript = editor =>
        {
            var dialog = Assert.IsType<CustomFieldEditorViewModel>(editor);
            dialog.Name = "Policy number";
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddCustomFieldCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Templates.All);
        var line = Assert.Single(saved.Lines);
        Assert.Equal(("Building", 80m), (line.Description, line.Gross));
        var field = Assert.Single(saved.CustomFields);
        Assert.Equal(("Policy number", saved.Id), (field.Name, field.OwnerId));
    }

    [Fact]
    public async Task Delete_removes_the_template_after_confirmation()
    {
        var template = new BillTemplate
        {
            Name = "Old", PropertyId = _kit.Kit.Property.Id, CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Insurance),
            Schedule = new Schedule { Kind = ScheduleKind.Once },
        };
        _kit.Templates.All.Add(template);
        var vm = await OpenAsync(template.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Templates.Stored);
        Assert.Equal("Dialog_Delete_Title Old [en]", Assert.Single(_kit.Kit.Dialogs.Confirmations).Title);
    }

    [Fact]
    public async Task Leaving_with_changes_asks_to_save()
    {
        var vm = await NewEditorAsync();
        vm.TemplateName = "Draft of a template";

        _kit.Kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        var canLeave = await vm.CanLeaveAsync();

        Assert.False(canLeave);
        Assert.Equal(1, _kit.Kit.Dialogs.UnsavedAsked);
    }

    // ---- The list --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_list_shows_active_templates_first_with_their_schedule()
    {
        _kit.Templates.All.Add(new BillTemplate
        {
            Name = "Old fee", PropertyId = _kit.Kit.Property.Id, CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Other), IsActive = false,
            Schedule = new Schedule { Kind = ScheduleKind.Once },
        });
        _kit.Templates.All.Add(new BillTemplate
        {
            Name = "Insurance", PropertyId = _kit.Kit.Property.Id, CategoryId = _kit.Kit.CategoryIdFor(ServiceKind.Insurance),
            Timing = BillingTiming.InArrears,
            Schedule = new Schedule { Kind = ScheduleKind.Yearly, StartMonth = new YearMonth(2026, 3) },
        });
        var vm = _kit.Kit.CreateTemplates();

        await vm.ReloadAsync();

        Assert.Equal(["Insurance", "Old fee"], vm.Items.Select(i => i.Name));
        Assert.StartsWith("BillTemplateScheduleYearly 03.2026", vm.Items[0].ScheduleText, StringComparison.Ordinal);
        Assert.Contains("Enum_BillingTiming_InArrears", vm.Items[0].ScheduleText, StringComparison.Ordinal);
        Assert.True(vm.Items[1].IsInactive);
        Assert.Equal("Sample flat", vm.Items[0].PropertyName);
        Assert.True(vm.IsLoaded);
    }

    [Fact]
    public async Task The_list_is_empty_without_templates_and_adding_opens_an_editor()
    {
        var vm = _kit.Kit.CreateTemplates();
        await vm.ReloadAsync();
        Assert.True(vm.IsEmpty);

        await vm.AddCommand.ExecuteAsync(null);

        Assert.True(vm.SelectedEditor!.IsNew);
    }
}
