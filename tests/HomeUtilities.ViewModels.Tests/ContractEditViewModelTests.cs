using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class ContractEditViewModelTests
{
    private const string ValidIban = "LV80BANK0000435195001";

    private readonly ContractTestKit _kit = new(new DateOnly(2026, 10, 23));
    private readonly Supplier _supplier = new()
    {
        Name = "Water utility",
        PaymentDetails = new PaymentDetails("Water utility Ltd", null, ValidIban, null, null, "Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}"),
        Links = [new Link { Kind = LinkKind.Website, Url = "https://example.com", SortOrder = 0 }],
    };

    public ContractEditViewModelTests()
    {
        _kit.Suppliers.Stored.Add(_supplier);
    }

    [Fact]
    public async Task NewContract_PreselectsTheCategoryOfTheServiceKind_WhileTheUserHasNotPickedOne()
    {
        var vm = await CreateNewAsync();
        Assert.Equal(_kit.CategoryIdFor(ServiceKind.ColdWater), vm.SelectedCategory!.Id);

        vm.SelectedService = Option(vm.Services, ServiceKind.Electricity);
        Assert.Equal(_kit.CategoryIdFor(ServiceKind.Electricity), vm.SelectedCategory!.Id);

        vm.SelectedService = Option(vm.Services, ServiceKind.Internet);
        Assert.Equal(_kit.CategoryIdFor(ServiceKind.Internet), vm.SelectedCategory!.Id);
    }

    [Fact]
    public async Task NewContract_KeepsTheCategoryTheUserPicked_WhenTheServiceKindChanges()
    {
        var vm = await CreateNewAsync();
        var gas = _kit.CategoryIdFor(ServiceKind.Gas);
        _kit.Dialogs.ChooseScript = options => options.Single(o => o == "Enum_ServiceKind_Gas [en]");

        await vm.ChooseCategoryCommand.ExecuteAsync(null);
        vm.SelectedService = Option(vm.Services, ServiceKind.Electricity);

        Assert.Equal(gas, vm.SelectedCategory!.Id);
    }

    [Fact]
    public async Task ExistingContract_DoesNotChangeTheCategory_WhenTheServiceKindChanges()
    {
        var contract = AddContract(ServiceKind.ColdWater);
        var vm = _kit.CreateContractEditor();
        await vm.LoadAsync(contract.PropertyId, contract.Id);

        vm.SelectedService = Option(vm.Services, ServiceKind.Electricity);

        Assert.Equal(contract.CategoryId, vm.SelectedCategory!.Id);
    }

    [Fact]
    public async Task NewSupplierFlow_OpensTheSupplierEditorAndSelectsTheCreatedSupplier()
    {
        var vm = await CreateNewAsync();
        var newSupplier = "ContractNewSupplier [en]";
        _kit.Dialogs.ChooseScript = options =>
        {
            Assert.Equal(newSupplier, options[^1]);
            return newSupplier;
        };

        await vm.ChooseSupplierCommand.ExecuteAsync(null);

        var visit = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.SupplierEdit, visit.Route);
        var editor = Assert.IsType<SupplierEditViewModel>(visit.Parameters![EditorNavigation.EditorParameter]);
        Assert.True(editor.IsHostedInPage);
        Assert.Null(vm.SelectedSupplier);

        editor.Name = "Internet provider";
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Internet provider", vm.SelectedSupplier!.Name);
        Assert.Equal(editor.EntityId, vm.SelectedSupplier.Id);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task NewSupplierFlow_CancelledSupplierEditor_KeepsTheSelection()
    {
        var vm = await CreateNewAsync();
        vm.SelectedSupplier = Supplier(_supplier);

        await vm.NewSupplierCommand.ExecuteAsync(null);
        var editor = Assert.IsType<SupplierEditViewModel>(_kit.Navigation.Visited.Single().Parameters![EditorNavigation.EditorParameter]);
        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Equal(_supplier.Id, vm.SelectedSupplier!.Id);
    }

    [Fact]
    public async Task EndDate_IsSavedOnlyWhileTheSwitchIsOn()
    {
        var vm = await FilledNewAsync();
        vm.HasEndDate = true;
        vm.ValidTo = new DateOnly(2027, 5, 31);

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2027, 5, 31), _kit.Contracts.Saved[^1].ValidTo);

        vm.HasEndDate = false;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(_kit.Contracts.Saved[^1].ValidTo);
    }

    [Fact]
    public async Task EndDate_BeforeStart_ShowsErrorUnderTheField()
    {
        var vm = await FilledNewAsync();
        vm.ValidFrom = new DateOnly(2026, 6, 1);
        vm.HasEndDate = true;
        vm.ValidTo = new DateOnly(2026, 5, 31);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.Saved);
        Assert.Equal("Error_contract_valid_to_before_valid_from [en]", vm.ValidToError);
    }

    [Fact]
    public async Task ReadingWindow_WithOnlyOneDay_ShowsErrorAndDoesNotSave()
    {
        var vm = await FilledNewAsync();
        vm.HasReadingWindow = true;
        vm.ReadingDayFromText = "25";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.Saved);
        Assert.Equal("Error_contract_reading_window_incomplete [en]", vm.ReadingWindowError);
    }

    [Fact]
    public async Task ReadingWindow_IsSavedWhenCompleteAndDroppedWhenTheSwitchIsOff()
    {
        var vm = await FilledNewAsync();
        vm.HasReadingWindow = true;
        vm.ReadingDayFromText = "25";
        vm.ReadingDayToText = "28";
        vm.BillDayText = "5";
        vm.PaymentDueDayText = "20";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = _kit.Contracts.Saved[^1];
        Assert.Equal((25, 28, 5, 20), (saved.ReadingDayFrom, saved.ReadingDayTo, saved.BillDay, saved.PaymentDueDay));

        vm.HasReadingWindow = false;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(_kit.Contracts.Saved[^1].ReadingDayFrom);
        Assert.Null(_kit.Contracts.Saved[^1].ReadingDayTo);
    }

    [Fact]
    public async Task Days_WithTextOrOutOfRange_ShowErrors()
    {
        var vm = await FilledNewAsync();
        vm.BillDayText = "abc";
        vm.PaymentDueDayText = "32";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.Saved);
        Assert.Equal("Error_common_number_invalid [en]", vm.BillDayError);
        Assert.Equal("Error_contract_day_out_of_range 32 [en]", vm.PaymentDueDayError);
    }

    [Fact]
    public async Task InheritedLinks_ShowSupplierLinksTheContractDoesNotOverride()
    {
        _supplier.Links.Add(new Link { Kind = LinkKind.CustomerPortal, Url = "https://example.com/portal", SortOrder = 1 });
        var vm = await FilledNewAsync();

        Assert.Equal(["https://example.com", "https://example.com/portal"], vm.InheritedLinks.Select(l => l.Url));
        Assert.Equal("LinkFromSupplier [en]", vm.InheritedLinks[0].FromSupplierText);

        await AddLink(vm, LinkKind.CustomerPortal, "Customer portal", "https://example.com/my");

        Assert.Equal(["https://example.com"], vm.InheritedLinks.Select(l => l.Url));
        Assert.Single(vm.Links);
        Assert.False(vm.HasNoLinks);
    }

    [Fact]
    public async Task InheritedLinks_OfKindOther_AreNeverHiddenByOwnLinks()
    {
        _supplier.Links.Clear();
        _supplier.Links.Add(new Link { Kind = LinkKind.Other, Url = "https://example.com/a" });
        var vm = await FilledNewAsync();

        await AddLink(vm, LinkKind.Other, "Mine", "https://example.com/b");

        Assert.Equal(["https://example.com/a"], vm.InheritedLinks.Select(l => l.Url));
    }

    [Fact]
    public async Task InheritedLinks_ChangeWithTheSelectedSupplier_AndOpenInTheBrowser()
    {
        var vm = await CreateNewAsync();
        Assert.Empty(vm.InheritedLinks);

        vm.SelectedSupplier = Supplier(_supplier);
        vm.InheritedLinks[0].OpenCommand.Execute(null);

        Assert.Equal(new Uri("https://example.com"), Assert.Single(_kit.Launcher.Opened));
    }

    [Fact]
    public async Task OwnLinks_AreSavedWithOwnerAndOrder()
    {
        var vm = await FilledNewAsync();
        await AddLink(vm, LinkKind.CustomerPortal, "Customer portal", "https://example.com/my");
        await AddLink(vm, LinkKind.Website, "", "https://example.com/site");

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Contracts.Saved);
        Assert.All(saved.Links, l => Assert.Equal(saved.Id, l.OwnerId));
        Assert.Equal([0, 1], saved.Links.Select(l => l.SortOrder));
        Assert.Equal("Customer portal", saved.Links[0].Title);
        Assert.Null(saved.Links[1].Title);
    }

    [Fact]
    public async Task PaymentFields_ShowThePlaceholdersOfTheSupplier()
    {
        var vm = await FilledNewAsync();

        Assert.Equal(string.Empty, vm.IbanText);
        Assert.Equal("PaymentFromSupplier LV80 BANK 0000 4351 9500 1 [en]", vm.IbanPlaceholder);
        Assert.Equal("PaymentFromSupplier Water utility Ltd [en]", vm.RecipientNamePlaceholder);
        Assert.Equal(string.Empty, vm.BicPlaceholder);
        Assert.Equal(string.Empty, vm.BankNamePlaceholder);
    }

    [Fact]
    public async Task EffectiveDetails_UseTheSupplierValuesWhereTheContractIsBlank()
    {
        var vm = await FilledNewAsync();
        Assert.Equal("LV80 BANK 0000 4351 9500 1", vm.EffectiveIban);
        Assert.Equal("Water utility Ltd", vm.EffectiveRecipient);

        vm.RecipientName = "Own recipient";

        Assert.Equal("Own recipient", vm.EffectiveRecipient);
        Assert.Equal("LV80 BANK 0000 4351 9500 1", vm.EffectiveIban);
    }

    [Fact]
    public async Task EffectivePurpose_InArrears_UsesThePreviousMonthAndTheRealCustomerNumber()
    {
        var vm = await FilledNewAsync();
        vm.CustomerNumber = "CUST-1";

        Assert.Equal("Abon. Nr. CUST-1, 09.2026", vm.EffectivePurpose);
    }

    [Fact]
    public async Task EffectivePurpose_InAdvance_UsesTheNextMonth()
    {
        var vm = await FilledNewAsync();
        vm.CustomerNumber = "CUST-1";

        vm.SelectedBillingTiming = Option(vm.BillingTimings, BillingTiming.InAdvance);

        Assert.Equal("Abon. Nr. CUST-1, 11.2026", vm.EffectivePurpose);
    }

    [Fact]
    public async Task EffectivePurpose_ContractTemplateOverridesTheSuppliers_AndFillsPropertyAndContractNumber()
    {
        var vm = await FilledNewAsync();
        vm.CustomerNumber = "CUST-1";
        vm.ContractNumber = "C-77";
        vm.PurposeTemplate = "{PropertyName} {ContractNumber} {CustomerNumber}";

        Assert.Equal("Sample flat C-77 CUST-1", vm.EffectivePurpose);
    }

    [Fact]
    public async Task CopyButtons_PutTheEffectiveValuesOnTheClipboard()
    {
        var vm = await FilledNewAsync();
        vm.CustomerNumber = "CUST-1";

        await vm.CopyIbanCommand.ExecuteAsync(null);
        await vm.CopyRecipientCommand.ExecuteAsync(null);
        await vm.CopyPurposeCommand.ExecuteAsync(null);

        Assert.Equal([ValidIban, "Water utility Ltd", "Abon. Nr. CUST-1, 09.2026"], _kit.Clipboard.Texts);
    }

    [Fact]
    public async Task Save_StoresOnlyOwnPaymentDetails_NotTheInheritedOnes()
    {
        var vm = await FilledNewAsync();
        vm.IbanText = "lv80 bank 0000 4351 9500 1";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new PaymentDetails(null, null, ValidIban, null, null, null), _kit.Contracts.Saved[0].PaymentDetails);
    }

    [Fact]
    public async Task Save_WithoutOwnPaymentDetails_StoresNull()
    {
        var vm = await FilledNewAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(_kit.Contracts.Saved[0].PaymentDetails);
    }

    [Fact]
    public async Task CustomFields_CanBeAddedEditedAndRemoved()
    {
        var vm = await FilledNewAsync();

        await AddField(vm, "Losses m³", CustomFieldKind.Number, CustomFieldTarget.Bill);
        await AddField(vm, "Meter note", CustomFieldKind.Text, CustomFieldTarget.BillLine);

        Assert.Equal(["Losses m³", "Meter note"], vm.CustomFields.Select(f => f.Name));
        Assert.False(vm.HasNoCustomFields);
        Assert.True(vm.IsDirty);

        _kit.Dialogs.EditorScript = e =>
        {
            var dialog = (CustomFieldEditorViewModel)e;
            Assert.Equal("Losses m³", dialog.Name);
            dialog.Name = "Losses";
            dialog.SelectedKind = Option(dialog.Kinds, CustomFieldKind.Money);
            dialog.SaveCommand.Execute(null);
        };
        await vm.EditCustomFieldCommand.ExecuteAsync(vm.CustomFields[0]);

        Assert.Equal("Losses", vm.CustomFields[0].Name);
        Assert.Equal(CustomFieldKind.Money, vm.CustomFields[0].Kind);

        await vm.RemoveCustomFieldCommand.ExecuteAsync(vm.CustomFields[0]);

        Assert.Equal(["Meter note"], vm.CustomFields.Select(f => f.Name));
        Assert.Single(_kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task CustomFields_RemoveNotConfirmed_KeepsTheField()
    {
        var vm = await FilledNewAsync();
        await AddField(vm, "Losses", CustomFieldKind.Number, CustomFieldTarget.Bill);
        _kit.Dialogs.ConfirmResult = false;

        await vm.RemoveCustomFieldCommand.ExecuteAsync(vm.CustomFields[0]);

        Assert.Single(vm.CustomFields);
    }

    [Fact]
    public async Task CustomFieldDialog_RejectsBlankAndDuplicateNames()
    {
        var vm = await FilledNewAsync();
        await AddField(vm, "Losses", CustomFieldKind.Number, CustomFieldTarget.Bill);

        var blank = await TryAddField(vm, "  ", CustomFieldTarget.Bill);
        var duplicate = await TryAddField(vm, "LOSSES", CustomFieldTarget.Bill);
        var otherTarget = await TryAddField(vm, "Losses", CustomFieldTarget.BillLine);

        Assert.False(blank.Saved);
        Assert.Equal("Error_custom_field_name_required [en]", blank.NameError);
        Assert.False(duplicate.Saved);
        Assert.Equal("Error_custom_field_name_duplicate [en]", duplicate.NameError);
        Assert.True(otherTarget.Saved);
        Assert.Equal(2, vm.CustomFields.Count);
    }

    [Fact]
    public async Task Save_StoresCustomFieldsInOrder_AndMapsErrorsToTheRow()
    {
        var vm = await FilledNewAsync();
        await AddField(vm, "Losses", CustomFieldKind.Number, CustomFieldTarget.Bill);
        await AddField(vm, "Note", CustomFieldKind.Text, CustomFieldTarget.Bill);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Contracts.Saved);
        Assert.Equal(["Losses", "Note"], saved.CustomFields.Select(f => f.Name));
        Assert.Equal([0, 1], saved.CustomFields.Select(f => f.SortOrder));
        Assert.All(saved.CustomFields, f => Assert.Equal(saved.Id, f.OwnerId));

        vm.CustomFields[1].Name = "losses";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(_kit.Contracts.Saved);
        Assert.Equal(string.Empty, vm.CustomFields[0].ErrorText);
        Assert.Equal("Error_custom_field_name_duplicate [en]", vm.CustomFields[1].ErrorText);
    }

    [Fact]
    public async Task Save_StoresGeneralFieldsWithThePropertyForeignKey()
    {
        var vm = await CreateNewAsync();
        vm.ContractTitle = "  Water ";
        vm.SelectedService = Option(vm.Services, ServiceKind.ColdWater);
        vm.SelectedSupplier = Supplier(_supplier);
        vm.CustomerNumber = " CUST-1 ";
        vm.ContractNumber = "";
        vm.PortalLogin = "login";
        vm.ValidFrom = new DateOnly(2024, 3, 1);
        vm.SelectedBillingMode = Option(vm.BillingModes, BillingMode.Equalized);
        vm.Notes = "A note";
        vm.IsArchived = true;

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Contracts.Saved);
        Assert.Equal(_kit.Property.Id, saved.PropertyId);
        Assert.Equal(_supplier.Id, saved.SupplierId);
        Assert.Equal(_kit.CategoryIdFor(ServiceKind.ColdWater), saved.CategoryId);
        Assert.Equal("Water", saved.Title);
        Assert.Equal(ServiceKind.ColdWater, saved.Service);
        Assert.Equal("CUST-1", saved.CustomerNumber);
        Assert.Null(saved.ContractNumber);
        Assert.Equal("login", saved.PortalLogin);
        Assert.Equal(new DateOnly(2024, 3, 1), saved.ValidFrom);
        Assert.Equal(BillingMode.Equalized, saved.BillingMode);
        Assert.Equal(BillingTiming.InArrears, saved.BillingTiming);
        Assert.Equal(Currency.EUR, saved.Currency);
        Assert.Equal("A note", saved.Notes);
        Assert.True(saved.IsArchived);
        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Save_WithoutRequiredFields_ShowsErrorsAndDoesNotSave()
    {
        var vm = await CreateNewAsync();
        vm.SelectedCategory = null;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.Saved);
        Assert.Equal("Error_contract_title_required [en]", vm.ContractTitleError);
        Assert.Equal("Error_contract_supplier_required [en]", vm.SupplierError);
        Assert.Equal("Error_contract_category_required [en]", vm.CategoryError);
        Assert.Equal(0, _kit.Navigation.BackCount);
    }

    [Fact]
    public async Task Save_InPage_StaysOpenReportsTheOutcomeAndConfirms()
    {
        var vm = await FilledNewAsync();
        EditOutcome? outcome = null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, _kit.Navigation.BackCount);
        Assert.Equal(EditOutcome.Saved, outcome);
        Assert.False(vm.IsDirty);
        Assert.Contains("Saved [en]", _kit.Toast.Messages);
    }

    [Fact]
    public async Task Currency_OffersLatsOnlyForAContractThatAlreadyUsesThem()
    {
        var euro = AddContract(ServiceKind.ColdWater);
        var legacy = AddContract(ServiceKind.Gas);
        legacy.Currency = Currency.LVL;

        var vmEuro = _kit.CreateContractEditor();
        await vmEuro.LoadAsync(euro.PropertyId, euro.Id);
        var vmLegacy = _kit.CreateContractEditor();
        await vmLegacy.LoadAsync(legacy.PropertyId, legacy.Id);
        var vmNew = await CreateNewAsync();

        Assert.False(vmEuro.ShowCurrencyPicker);
        Assert.False(vmNew.ShowCurrencyPicker);
        Assert.True(vmLegacy.ShowCurrencyPicker);
        Assert.Equal(Currency.LVL, vmLegacy.SelectedCurrency!.As<Currency>());
    }

    [Fact]
    public async Task Load_ExistingContract_FillsAllFields()
    {
        var contract = AddContract(ServiceKind.ColdWater);
        contract.CustomerNumber = "CUST-1";
        contract.ValidTo = new DateOnly(2030, 1, 31);
        contract.ReadingDayFrom = 25;
        contract.ReadingDayTo = 28;
        contract.BillingTiming = BillingTiming.InAdvance;
        contract.Links = [new Link { Kind = LinkKind.CustomerPortal, Url = "https://example.com/my", Title = "Portal" }];
        contract.CustomFields = [new CustomFieldDefinition { Name = "Losses", Kind = CustomFieldKind.Number }];
        var vm = _kit.CreateContractEditor();

        await vm.LoadAsync(contract.PropertyId, contract.Id);

        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Equal("Water", vm.ContractTitle);
        Assert.Equal("CUST-1", vm.CustomerNumber);
        Assert.True(vm.HasEndDate);
        Assert.Equal(new DateOnly(2030, 1, 31), vm.ValidTo);
        Assert.True(vm.HasReadingWindow);
        Assert.Equal(("25", "28"), (vm.ReadingDayFromText, vm.ReadingDayToText));
        Assert.Equal(BillingTiming.InAdvance, vm.SelectedBillingTiming!.As<BillingTiming>());
        Assert.Equal("Water utility", vm.SelectedSupplierName);
        Assert.Equal(_kit.CategoryIdFor(ServiceKind.ColdWater), vm.SelectedCategory!.Id);
        Assert.Equal(vm.SelectedServiceIndex, vm.Services.ToList().IndexOf(Option(vm.Services, ServiceKind.ColdWater)));
        Assert.Equal(["Portal"], vm.Links.Select(l => l.DisplayText));
        Assert.Equal(["Losses"], vm.CustomFields.Select(f => f.Name));
        Assert.Equal("ContractTimingHint_InAdvance [en]", vm.BillingTimingHint);
    }

    [Fact]
    public async Task CanLeave_WithoutChanges_DoesNotAsk()
    {
        var vm = await CreateNewAsync();

        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _kit.Dialogs.UnsavedAsked);
    }

    [Theory]
    [InlineData(UnsavedChangesChoice.DontSave, true, 0)]
    [InlineData(UnsavedChangesChoice.Cancel, false, 0)]
    [InlineData(UnsavedChangesChoice.Save, true, 1)]
    public async Task CanLeave_WithChanges_FollowsTheChoice(UnsavedChangesChoice choice, bool expected, int saves)
    {
        var vm = await FilledNewAsync();
        vm.Notes = "changed";
        _kit.Dialogs.UnsavedChoice = choice;

        Assert.Equal(expected, await vm.CanLeaveAsync());
        Assert.Equal(1, _kit.Dialogs.UnsavedAsked);
        Assert.Equal(saves, _kit.Contracts.Saved.Count);
    }

    [Fact]
    public async Task CanLeave_SaveChosenButInvalid_StaysInTheEditor()
    {
        var vm = await CreateNewAsync();
        vm.Notes = "changed";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.False(await vm.CanLeaveAsync());
        Assert.True(vm.HasErrors);
        Assert.Empty(_kit.Contracts.Saved);
    }

    [Fact]
    public async Task Cancel_WithChanges_AsksAndLeavesOnDontSave()
    {
        var vm = await FilledNewAsync();
        vm.Notes = "changed";

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, _kit.Dialogs.UnsavedAsked);
        Assert.Equal(1, _kit.Navigation.BackCount);
        Assert.Empty(_kit.Contracts.Saved);
    }

    [Fact]
    public async Task Delete_AfterConfirmation_DeletesTheContractAndLeaves()
    {
        var contract = AddContract(ServiceKind.ColdWater);
        var vm = _kit.CreateContractEditor();
        await vm.LoadAsync(contract.PropertyId, contract.Id);
        vm.IsHostedInPage = true;
        EditOutcome? outcome = null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };
        Assert.True(vm.ShowFormDelete);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([contract.Id], _kit.Contracts.Deleted);
        Assert.Equal("Dialog_Delete_Title Water [en]", Assert.Single(_kit.Dialogs.Confirmations).Title);
        Assert.Equal(EditOutcome.Deleted, outcome);
        Assert.Equal(1, _kit.Navigation.BackCount);
    }

    [Fact]
    public async Task Delete_NotConfirmed_KeepsTheContract()
    {
        var contract = AddContract(ServiceKind.ColdWater);
        var vm = _kit.CreateContractEditor();
        await vm.LoadAsync(contract.PropertyId, contract.Id);
        _kit.Dialogs.ConfirmResult = false;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.Deleted);
    }

    [Fact]
    public async Task NewContract_CannotBeDeleted()
    {
        var vm = await CreateNewAsync();

        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.False(vm.ShowFormDelete);
    }

    [Fact]
    public async Task Texts_AreRefreshedAfterTheLanguageChanged_AndSelectionsAreKept()
    {
        var contract = AddContract(ServiceKind.Electricity);
        var vm = _kit.CreateContractEditor();
        await vm.LoadAsync(contract.PropertyId, contract.Id);
        vm.Notes = "edited";

        _kit.Language.SetLanguage("ru");

        Assert.Equal("ContractGeneral [ru]", vm.GeneralHeader);
        Assert.Equal("Enum_ServiceKind_Electricity [ru]", vm.SelectedService!.DisplayName);
        Assert.Equal(contract.CategoryId, vm.SelectedCategory!.Id);
        Assert.Equal("Water utility", vm.SelectedSupplierName);
        Assert.Equal("edited", vm.Notes);
    }

    private static EnumOption Option<TEnum>(IReadOnlyList<EnumOption> options, TEnum value)
        where TEnum : struct, Enum => options.First(o => o.Value.Equals(value));

    private static ChoiceOption Supplier(Supplier supplier) => new(supplier.Id, supplier.Name, string.Empty);

    private Contract AddContract(ServiceKind kind)
    {
        var contract = new Contract
        {
            PropertyId = _kit.Property.Id,
            SupplierId = _supplier.Id,
            CategoryId = _kit.CategoryIdFor(kind),
            Service = kind,
            Title = "Water",
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Contracts.Stored.Add(contract);
        return contract;
    }

    private async Task<ContractEditViewModel> CreateNewAsync()
    {
        var vm = _kit.CreateContractEditor();
        vm.IsHostedInPage = true;
        await vm.LoadAsync(_kit.Property.Id, null);
        return vm;
    }

    /// <summary>A new contract with title and supplier, so that it can be saved.</summary>
    private async Task<ContractEditViewModel> FilledNewAsync()
    {
        var vm = await CreateNewAsync();
        vm.ContractTitle = "Water";
        vm.SelectedSupplier = Supplier(_supplier);
        return vm;
    }

    private async Task AddLink(ContractEditViewModel vm, LinkKind kind, string title, string url)
    {
        _kit.Dialogs.EditorScript = e =>
        {
            var dialog = (LinkEditorViewModel)e;
            dialog.SelectedKind = Option(dialog.Kinds, kind);
            dialog.LinkTitle = title;
            dialog.Url = url;
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddLinkCommand.ExecuteAsync(null);
    }

    private async Task<CustomFieldEditorViewModel> TryAddField(ContractEditViewModel vm, string name, CustomFieldTarget target)
    {
        _kit.Dialogs.EditorScript = e =>
        {
            var dialog = (CustomFieldEditorViewModel)e;
            dialog.Name = name;
            dialog.SelectedTarget = Option(dialog.Targets, target);
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddCustomFieldCommand.ExecuteAsync(null);
        return (CustomFieldEditorViewModel)_kit.Dialogs.LastEditor!;
    }

    private async Task AddField(ContractEditViewModel vm, string name, CustomFieldKind kind, CustomFieldTarget target)
    {
        _kit.Dialogs.EditorScript = e =>
        {
            var dialog = (CustomFieldEditorViewModel)e;
            dialog.Name = name;
            dialog.SelectedKind = Option(dialog.Kinds, kind);
            dialog.SelectedTarget = Option(dialog.Targets, target);
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddCustomFieldCommand.ExecuteAsync(null);
    }
}
