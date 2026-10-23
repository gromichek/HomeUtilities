using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class PropertyEditViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly FakeLanguageService _language = new();
    private readonly FakePropertyStore _store = new();
    private readonly FakeMeterStore _meters = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task SaveNewProperty_PassesEnteredDataAndParameterForeignKeyToTheStore()
    {
        _store.Stored.Add(new Property { Name = "Existing", SortOrder = 4 });
        var vm = await CreateNewAsync();
        vm.Name = "  Apartment ";
        vm.SelectedKind = Option(vm.Kinds, PropertyKind.Apartment);
        vm.Address = "1 Example street";
        vm.Notes = "";
        await AddParameter(vm, PropertyParameterKind.TotalArea, "52,3", new(2020, 1, 1));
        await AddParameter(vm, PropertyParameterKind.DeclaredResidents, "3", new(2020, 1, 1));

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Equal("Apartment", saved.Name);
        Assert.Equal(PropertyKind.Apartment, saved.Kind);
        Assert.Equal("1 Example street", saved.Address);
        Assert.Null(saved.Notes);
        Assert.Equal(5, saved.SortOrder);
        Assert.Equal(2, saved.Parameters.Count);
        Assert.All(saved.Parameters, p => Assert.Equal(saved.Id, p.PropertyId));
        Assert.Equal(52.3m, saved.GetParameter(PropertyParameterKind.TotalArea, Today));
        Assert.Equal(3m, saved.GetParameter(PropertyParameterKind.DeclaredResidents, Today));
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsNew);
    }

    [Fact]
    public async Task SaveWithBlankName_ShowsErrorAndDoesNotSave()
    {
        var vm = await CreateNewAsync();
        vm.Name = "   ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.True(vm.HasErrors);
        Assert.Equal("Error_property_name_required [en]", vm.NameError);
        Assert.Equal(0, _navigation.BackCount);
    }

    [Fact]
    public async Task Save_InPage_StaysOpenAndReportsOutcome()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;
        EditOutcome? outcome = null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };
        vm.Name = "Flat";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, _navigation.BackCount);
        Assert.Equal(EditOutcome.Saved, outcome);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("2,5")]
    public async Task AddingFractionalResidents_ShowsErrorInTheDialogAndAddsNothing(string text)
    {
        var vm = await CreateNewAsync();

        var editor = await TryAddAsync(vm, PropertyParameterKind.DeclaredResidents, text, new(2020, 1, 1));

        Assert.False(editor.Saved);
        Assert.Equal("Error_property_parameter_residents_not_whole [en]", editor.ValueError);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task AddingTextAsValue_ShowsNumberError()
    {
        var vm = await CreateNewAsync();

        var editor = await TryAddAsync(vm, PropertyParameterKind.TotalArea, "abc", new(2020, 1, 1));

        Assert.Equal("Error_common_number_invalid [en]", editor.ValueError);
    }

    [Fact]
    public async Task AddingCustomParameterWithoutName_ShowsNameError()
    {
        var vm = await CreateNewAsync();

        var editor = await TryAddAsync(vm, PropertyParameterKind.Custom, "5", new(2020, 1, 1));

        Assert.True(editor.IsCustom);
        Assert.Equal("Error_property_parameter_name_required [en]", editor.NameError);
    }

    [Fact]
    public async Task AddingSameKindWithSameValidFrom_ShowsDuplicateError()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));

        var editor = await TryAddAsync(vm, PropertyParameterKind.TotalArea, "55", new(2020, 1, 1));

        Assert.StartsWith("Error_versioned_duplicate_valid_from", editor.ValidFromError);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task ParameterDialog_Save_AddsRowAndMarksDirty()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Flat";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.IsDirty);

        await AddParameter(vm, PropertyParameterKind.TotalArea, "54,7", new(2026, 10, 20));

        var row = Assert.Single(vm.Rows);
        Assert.Equal(54.7m, row.Value);
        Assert.Equal("ParameterNew [en]", _dialogs.LastEditor!.Title);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task ParameterDialog_Cancel_ChangesNothing()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        vm.IsDirty = false;

        _dialogs.EditorScript = e =>
        {
            var editor = (ParameterEditorViewModel)e;
            editor.ValueText = "99";
            editor.CancelCommand.Execute(null);
        };
        await vm.AddParameterCommand.ExecuteAsync(null);
        await vm.EditParameterCommand.ExecuteAsync(vm.Rows[0]);

        var row = Assert.Single(vm.Rows);
        Assert.Equal(50m, row.Value);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task ParameterDialog_Edit_PrefillsAndUpdatesTheRowInPlace()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        var row = vm.Rows[0];
        _dialogs.EditorScript = e =>
        {
            var editor = (ParameterEditorViewModel)e;
            Assert.Equal("50", editor.ValueText);
            Assert.Equal("ParameterEdit [en]", editor.Title);
            editor.ValueText = "51,5";
            editor.SaveCommand.Execute(null);
        };

        await vm.EditParameterCommand.ExecuteAsync(row);

        Assert.Single(vm.Rows);
        Assert.Equal(51.5m, row.Value);
    }

    [Fact]
    public async Task ParameterDialog_EditingARowKeepingItsOwnDate_IsNotADuplicate()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        _dialogs.EditorScript = e => e.SaveCommand.Execute(null);

        await vm.EditParameterCommand.ExecuteAsync(vm.Rows[0]);

        Assert.True(_dialogs.LastEditor!.Saved);
    }

    [Fact]
    public async Task InvalidStoredParameter_ShowsErrorOnItsRowAndDoesNotSave()
    {
        var stored = new Property { Name = "Flat" };
        stored.Parameters.Add(new() { Kind = PropertyParameterKind.TotalArea, ValidFrom = new(2020, 1, 1), Value = 50 });
        stored.Parameters.Add(new() { Kind = PropertyParameterKind.DeclaredResidents, ValidFrom = new(2020, 1, 1), Value = 2.5m });
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.Equal(string.Empty, vm.Rows[0].ErrorText);
        Assert.Equal("Error_property_parameter_residents_not_whole [en]", vm.Rows[1].ErrorText);
        Assert.True(vm.Rows[1].HasError);
    }

    [Fact]
    public async Task Groups_ShowCurrentValueAndHistoryNewestFirst()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.DeclaredResidents, "3", new(2020, 1, 1));
        await AddParameter(vm, PropertyParameterKind.DeclaredResidents, "2", new(2026, 6, 1));
        await AddParameter(vm, PropertyParameterKind.DeclaredResidents, "1", new(2027, 1, 1));
        await AddParameter(vm, PropertyParameterKind.TotalArea, "52,3", new(2020, 1, 1));
        await AddParameter(vm, PropertyParameterKind.Custom, "7", new(2021, 1, 1), name: "Parking places");

        Assert.Equal(
            ["Enum_PropertyParameterKind_TotalArea [en]", "Enum_PropertyParameterKind_DeclaredResidents [en]", "Parking places"],
            vm.Groups.Select(g => g.Title));
        var residents = vm.Groups[1];
        Assert.Equal([new DateOnly(2027, 1, 1), new(2026, 6, 1), new(2020, 1, 1)], residents.Rows.Select(r => r.ValidFrom));
        Assert.StartsWith("ParameterCurrent 2|", residents.CurrentText);
        Assert.StartsWith("ParameterCurrent 52.3|", vm.Groups[0].CurrentText);
    }

    [Fact]
    public async Task Group_WithOnlyFutureValues_HasNoCurrentValue()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2030, 1, 1));

        Assert.Equal("ParameterNoCurrent [en]", vm.Groups[0].CurrentText);
    }

    [Fact]
    public async Task RemoveParameter_AfterConfirmation_DropsItFromTheSavedGraph()
    {
        var stored = new Property { Name = "Flat" };
        stored.Parameters.Add(new() { Kind = PropertyParameterKind.TotalArea, ValidFrom = new(2020, 1, 1), Value = 50 });
        stored.Parameters.Add(new() { Kind = PropertyParameterKind.TotalArea, ValidFrom = new(2023, 1, 1), Value = 55 });
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);

        await vm.RemoveParameterCommand.ExecuteAsync(vm.Groups[0].Rows[0]);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirmations);
        var saved = Assert.Single(_store.Saved);
        var remaining = Assert.Single(saved.Parameters);
        Assert.Equal(new DateOnly(2020, 1, 1), remaining.ValidFrom);
    }

    [Fact]
    public async Task RemoveParameter_WhenDeclined_KeepsIt()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        _dialogs.ConfirmResult = false;

        await vm.RemoveParameterCommand.ExecuteAsync(vm.Rows[0]);

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task CanLeave_WithoutChanges_IsTrueWithoutDialog()
    {
        var vm = await CreateNewAsync();

        Assert.True(await vm.CanLeaveAsync());

        Assert.Equal(0, _dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task CanLeave_DirtyAndSaveWithValidData_SavesAndIsTrue()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Typed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.True(await vm.CanLeaveAsync());

        Assert.Equal(1, _dialogs.UnsavedAsked);
        Assert.Equal("Typed", Assert.Single(_store.Saved).Name);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task CanLeave_DirtyAndSaveWithInvalidData_IsFalseAndShowsErrors()
    {
        var vm = await CreateNewAsync();
        vm.Name = " ";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.False(await vm.CanLeaveAsync());

        Assert.Empty(_store.Saved);
        Assert.True(vm.HasErrors);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task CanLeave_DirtyAndDontSave_IsTrueAndSavesNothing()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Typed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;

        Assert.True(await vm.CanLeaveAsync());

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task CanLeave_DirtyAndCancel_IsFalse()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Typed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        Assert.False(await vm.CanLeaveAsync());

        Assert.Empty(_store.Saved);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task Back_AfterTogglingArchived_AsksTheUnsavedChangesDialog()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);
        vm.IsHostedInPage = true;
        Assert.False(vm.IsDirty);

        vm.IsArchived = true;
        Assert.True(vm.IsDirty);

        _dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        await vm.CancelCommand.ExecuteAsync(null);
        Assert.Equal(1, _dialogs.UnsavedAsked);
        Assert.Equal(0, _navigation.BackCount);

        _dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;
        await vm.CancelCommand.ExecuteAsync(null);
        Assert.Equal(1, _navigation.BackCount);
        Assert.Empty(_store.Saved);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Back_WithSaveChoice_SavesThenLeaves()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);
        vm.IsHostedInPage = true;
        vm.IsArchived = true;
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_store.Saved).IsArchived);
        Assert.Equal(1, _navigation.BackCount);
    }

    [Fact]
    public async Task Back_WithoutChanges_LeavesImmediately()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(0, _dialogs.UnsavedAsked);
        Assert.Equal(1, _navigation.BackCount);
    }

    [Fact]
    public async Task Load_ResetsIsDirty()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Typed";
        Assert.True(vm.IsDirty);

        await vm.LoadAsync(null);

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Save_ClearsIsDirtyAndStaysOpen()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;
        EditOutcome? outcome = null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };
        vm.Name = "Flat";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsDirty);
        Assert.Equal(0, _navigation.BackCount);
        Assert.Equal(EditOutcome.Saved, outcome);
    }

    [Fact]
    public async Task Delete_ClearsIsDirtyAndRequestsClose()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);
        vm.IsHostedInPage = true;
        EditOutcome? outcome = null;
        vm.Finished = o =>
        {
            outcome = o;
            return Task.CompletedTask;
        };
        vm.Name = "Changed";

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.False(vm.IsDirty);
        Assert.Equal(1, _navigation.BackCount);
        Assert.Equal(EditOutcome.Deleted, outcome);
    }

    [Fact]
    public async Task KindOptions_SelectedKindIsAnElementOfTheList_AfterLoadAndAfterLanguageChange()
    {
        var stored = new Property { Name = "Cottage", Kind = PropertyKind.Cottage };
        _store.Stored.Add(stored);
        var vm = Create();

        await vm.LoadAsync(stored.Id);

        Assert.Contains(vm.SelectedKind, vm.Kinds);
        Assert.Equal(PropertyKind.Cottage, vm.SelectedKind!.As<PropertyKind>());
        Assert.Equal(vm.Kinds.ToList().IndexOf(vm.SelectedKind), vm.SelectedKindIndex);

        _language.SetLanguage("ru");

        Assert.Contains(vm.SelectedKind, vm.Kinds);
        Assert.Equal(PropertyKind.Cottage, vm.SelectedKind!.As<PropertyKind>());
    }

    [Fact]
    public async Task SelectedKindIndex_IgnoresOutOfRangeValues()
    {
        var vm = await CreateNewAsync();
        var before = vm.SelectedKind;

        vm.SelectedKindIndex = -1;
        vm.SelectedKindIndex = 99;

        Assert.Same(before, vm.SelectedKind);
        vm.SelectedKindIndex = 1;
        Assert.Same(vm.Kinds[1], vm.SelectedKind);
    }

    [Fact]
    public async Task ParameterDialog_SelectedKindIsAnElementOfTheList()
    {
        var vm = await CreateNewAsync();
        _dialogs.EditorScript = null;

        await vm.AddParameterCommand.ExecuteAsync(null);

        var editor = (ParameterEditorViewModel)_dialogs.LastEditor!;
        Assert.Contains(editor.SelectedKind, editor.Kinds);
    }

    [Fact]
    public async Task Footer_IsShownInPaneAndDeleteInFormOnPage()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);

        Assert.True(vm.ShowFooter);
        Assert.False(vm.ShowFormDelete);

        vm.IsHostedInPage = true;

        Assert.False(vm.ShowFooter);
        Assert.True(vm.ShowFormDelete);
    }

    [Fact]
    public async Task HistoryToggle_SurvivesRebuildOfGroups()
    {
        var vm = await CreateNewAsync();
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        await AddParameter(vm, PropertyParameterKind.TotalArea, "55", new(2023, 1, 1));
        Assert.False(vm.Groups[0].IsExpanded);
        Assert.Equal("ParameterHistory 2 [en]", vm.Groups[0].HistoryText);

        vm.Groups[0].ToggleHistoryCommand.Execute(null);
        _language.SetLanguage("ru");

        Assert.True(vm.Groups[0].IsExpanded);
    }

    [Fact]
    public async Task Delete_AsksToConfirmAndDeletes()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        var (title, message) = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("Dialog_Delete_Title Cottage [en]", title);
        Assert.Equal("Dialog_Delete_Message [en]", message);
        Assert.Equal([stored.Id], _store.Deleted);
    }

    [Fact]
    public async Task Delete_WhenDeclined_DoesNothing()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);
        _dialogs.ConfirmResult = false;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task Delete_IsNotAvailableForNewProperty()
    {
        var vm = await CreateNewAsync();

        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Archiving_IsSavedWithTheProperty()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);

        vm.IsArchived = true;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_store.Saved).IsArchived);
        Assert.Equal(stored.Id, _store.Saved[0].Id);
    }

    [Fact]
    public async Task LanguageChange_KeepsEnteredDataAndSelectedKind()
    {
        var vm = await CreateNewAsync();
        vm.Name = "Typed";
        vm.SelectedKind = Option(vm.Kinds, PropertyKind.Cottage);
        await AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));

        _language.SetLanguage("ru");

        Assert.Equal("Typed", vm.Name);
        Assert.Equal(PropertyKind.Cottage, vm.SelectedKind!.As<PropertyKind>());
        Assert.Equal("Enum_PropertyKind_Cottage [ru]", vm.SelectedKind.DisplayName);
        Assert.Single(vm.Rows);
        Assert.Equal("Enum_PropertyParameterKind_TotalArea [ru]", vm.Groups[0].Title);
    }

    [Fact]
    public async Task EnteringNumberWithDecimalComma_WorksInAnyCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            var vm = await CreateNewAsync();
            await AddParameter(vm, PropertyParameterKind.TotalArea, "52,3", new(2020, 1, 1));
            await AddParameter(vm, PropertyParameterKind.HeatedArea, "52.3", new(2020, 1, 1));

            Assert.Equal(52.3m, vm.Rows[0].Value);
            Assert.Equal(52.3m, vm.Rows[1].Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task MetersCard_GroupsMetersByResourceAndHidesRemovedOnes()
    {
        var property = StoreProperty();
        AddMeter(property, MeterResource.HotWater, "Bathroom", "HW-1");
        AddMeter(property, MeterResource.ColdWater, "Kitchen", "CW-1");
        AddMeter(property, MeterResource.ColdWater, "Bathroom", "CW-2");
        AddMeter(property, MeterResource.Electricity, null, "E-1");
        AddMeter(property, MeterResource.ColdWater, "Old", "CW-0").RemovedOn = new DateOnly(2026, 1, 1);
        AddMeter(Guid.NewGuid(), MeterResource.Gas, "Elsewhere", "G-1");
        var vm = Create();

        await vm.LoadAsync(property);

        Assert.Equal(
            ["Enum_MeterResource_ColdWater [en]", "Enum_MeterResource_HotWater [en]", "Enum_MeterResource_Electricity [en]"],
            vm.MeterGroups.Select(g => g.Header));
        Assert.Equal(["Bathroom", "Kitchen"], vm.MeterGroups[0].Rows.Select(r => r.Title));
        Assert.Equal(["MeterNumbered E-1 [en]"], vm.MeterGroups[2].Rows.Select(r => r.Title));
        Assert.False(vm.HasNoMeters);
    }

    [Fact]
    public async Task MetersCard_ShowRemovedAddsThemMutedAfterTheInstalledOnes()
    {
        var property = StoreProperty();
        AddMeter(property, MeterResource.ColdWater, "Kitchen", "CW-2");
        AddMeter(property, MeterResource.ColdWater, "Aaa old", "CW-1").RemovedOn = new DateOnly(2026, 10, 23);
        var vm = Create();
        await vm.LoadAsync(property);
        Assert.Single(vm.MeterGroups.Single().Rows);

        vm.ShowRemovedMeters = true;

        var rows = vm.MeterGroups.Single().Rows;
        Assert.Equal(["Kitchen", "Aaa old"], rows.Select(r => r.Title));
        Assert.True(rows[1].IsRemoved);
        Assert.Equal($"MeterRemoved {new DateOnly(2026, 10, 23)} [en]", rows[1].RemovedText);
        Assert.False(rows[0].IsRemoved);
    }

    [Fact]
    public async Task MetersCard_ShowsVerificationBadgesAndRegisterSummary()
    {
        var property = StoreProperty();
        var overdue = AddMeter(property, MeterResource.ColdWater, "Overdue", "A");
        overdue.VerificationIntervalMonths = 72; // installed 2020-10-01 + 72 months = 2026-10-01; today is 2026-10-20
        var soon = AddMeter(property, MeterResource.ColdWater, "Soon", "B");
        soon.NextVerificationDue = new DateOnly(2026, 12, 1);
        var far = AddMeter(property, MeterResource.ColdWater, "Far", "C");
        far.VerifiedOn = new DateOnly(2021, 1, 15);
        far.VerificationIntervalMonths = 72;
        var none = AddMeter(property, MeterResource.ColdWater, "None", "D");
        var tariff = AddMeter(property, MeterResource.Electricity, "Panel", "E");
        tariff.Registers.Clear();
        foreach (var kind in new[] { RegisterKind.Day, RegisterKind.Night, RegisterKind.Export })
        {
            tariff.Registers.Add(new MeterRegister
            {
                MeterId = tariff.Id,
                Kind = kind,
                Direction = kind == RegisterKind.Export ? EnergyDirection.Export : EnergyDirection.Import,
            });
        }

        var vm = Create();
        await vm.LoadAsync(property);

        var water = vm.MeterGroups[0].Rows.ToDictionary(r => r.Title);
        Assert.True(water["Overdue"].IsOverdue);
        Assert.Equal($"MeterVerificationOverdue {new DateOnly(2026, 10, 1)} [en]", water["Overdue"].BadgeText);
        Assert.True(water["Soon"].IsDueSoon);
        Assert.Equal($"MeterVerificationDue {new DateOnly(2026, 12, 1)} [en]", water["Soon"].BadgeText);
        Assert.False(water["Far"].HasBadge);
        Assert.False(water["None"].HasBadge);
        Assert.False(water["None"].HasRegisters);
        Assert.Equal("Enum_RegisterKind_Day [en] / Enum_RegisterKind_Night [en] / Enum_RegisterKind_Export [en]", vm.MeterGroups[1].Rows[0].RegistersText);
        Assert.Equal("E", vm.MeterGroups[1].Rows[0].SerialText);
    }

    [Fact]
    public async Task MetersCard_AddIsDisabledUntilThePropertyIsSaved()
    {
        var vm = await CreateNewAsync();

        Assert.False(vm.AddMeterCommand.CanExecute(null));
        Assert.False(vm.HasNoMeters);
        vm.Name = "Flat";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.AddMeterCommand.CanExecute(null));
        Assert.True(vm.HasNoMeters);
    }

    [Fact]
    public async Task MetersCard_OpensTheMeterEditorOnARowAndReloadsAfterwards()
    {
        var property = StoreProperty();
        var meter = AddMeter(property, MeterResource.ColdWater, "Kitchen", "CW-1");
        var kit = new ContractTestKit(Today);
        kit.Meters.Stored.AddRange(_meters.Stored);
        kit.Properties.Stored.Clear();
        kit.Properties.Stored.AddRange(_store.Stored);
        var vm = kit.CreatePropertyEditor();
        await vm.LoadAsync(property);

        vm.MeterGroups[0].Rows[0].SelectCommand.Execute(null);
        await Task.Yield();

        var (route, parameters) = Assert.Single(kit.Navigation.Visited);
        Assert.Equal(Routes.MeterEdit, route);
        var editor = Assert.IsType<MeterEditViewModel>(parameters![EditorNavigation.EditorParameter]);
        Assert.Equal(meter.Id, editor.EntityId);
        Assert.True(editor.IsHostedInPage);

        kit.Meters.Stored.Add(new Meter { PropertyId = property, Resource = MeterResource.Gas, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 1, 1) });
        await editor.Finished!(EditOutcome.Saved);
        Assert.Equal(2, vm.MeterGroups.Count);
    }

    private PropertyEditViewModel Create() =>
        new(
            _store,
            new FakeContractStore(),
            _meters,
            new FakeSupplierStore(),
            new FakeCategoryStore(),
            new TestClock(Today),
            new FakeLocalizer(_language),
            _dialogs,
            _navigation,
            new FakeToastService(),
            () => throw new NotSupportedException(),
            () => throw new NotSupportedException());

    private Guid StoreProperty()
    {
        var property = new Property { Name = "Flat", Kind = PropertyKind.Apartment };
        _store.Stored.Add(property);
        return property.Id;
    }

    private Meter AddMeter(Guid propertyId, MeterResource resource, string? location, string serial)
    {
        var meter = new Meter
        {
            PropertyId = propertyId,
            Resource = resource,
            Unit = MeterUnitRules.DefaultUnit(resource),
            Location = location,
            SerialNumber = serial,
            InstalledOn = new DateOnly(2020, 10, 1),
        };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        _meters.Stored.Add(meter);
        return meter;
    }

    private async Task<PropertyEditViewModel> CreateNewAsync()
    {
        var vm = Create();
        await vm.LoadAsync(null);
        return vm;
    }

    private static EnumOption Option<TEnum>(IReadOnlyList<EnumOption> options, TEnum value)
        where TEnum : struct, Enum => options.First(o => o.Value.Equals(value));

    /// <summary>Opens the add dialog, fills it and presses Save; the dialog may stay open when the data is invalid.</summary>
    private async Task<ParameterEditorViewModel> TryAddAsync(
        PropertyEditViewModel vm, PropertyParameterKind kind, string valueText, DateOnly validFrom, string name = "")
    {
        _dialogs.EditorScript = e =>
        {
            var editor = (ParameterEditorViewModel)e;
            editor.SelectedKind = Option(editor.Kinds, kind);
            editor.Name = name;
            editor.ValueText = valueText;
            editor.ValidFrom = validFrom;
            editor.SaveCommand.Execute(null);
        };
        await vm.AddParameterCommand.ExecuteAsync(null);
        return (ParameterEditorViewModel)_dialogs.LastEditor!;
    }

    private async Task AddParameter(
        PropertyEditViewModel vm, PropertyParameterKind kind, string valueText, DateOnly validFrom, string name = "")
    {
        var editor = await TryAddAsync(vm, kind, valueText, validFrom, name);
        Assert.True(editor.Saved, editor.ValueError + editor.NameError + editor.ValidFromError);
    }

    [Fact]
    public async Task ContractsCard_ListsContractsOfTheProperty_AndHidesEndedAndArchivedByDefault()
    {
        var kit = new ContractTestKit(Today);
        var supplier = new Supplier { Name = "Water utility" };
        kit.Suppliers.Stored.Add(supplier);
        var water = Contract(kit, supplier, "Water", ServiceKind.ColdWater);
        var ended = Contract(kit, supplier, "Old internet", ServiceKind.Internet);
        ended.ValidTo = Today.AddDays(-1);
        var archived = Contract(kit, supplier, "Archived gas", ServiceKind.Gas);
        archived.IsArchived = true;
        var endsToday = Contract(kit, supplier, "Heating", ServiceKind.Heating);
        endsToday.ValidTo = Today;
        kit.Contracts.Stored.Add(new Contract { PropertyId = Guid.CreateVersion7(), Title = "Other property" });

        var vm = kit.CreatePropertyEditor();
        await vm.LoadAsync(kit.Property.Id);

        Assert.Equal(["Heating", "Water"], vm.ContractRows.Select(r => r.Title));
        var row = vm.ContractRows[1];
        Assert.Equal("Water utility", row.SupplierName);
        Assert.Equal("Enum_ServiceKind_ColdWater [en]", row.ServiceText);
        Assert.Equal(kit.Categories.Stored.First(c => c.SystemKey == ServiceKind.ColdWater).Color, row.Color);
        Assert.False(row.IsEnded);

        vm.ShowEndedContracts = true;

        Assert.Equal(["Archived gas", "Heating", "Old internet", "Water"], vm.ContractRows.Select(r => r.Title));
        Assert.True(vm.ContractRows.Single(r => r.Title == "Old internet").IsEnded);
        Assert.True(vm.ContractRows.Single(r => r.Title == "Archived gas").IsArchived);
    }

    [Fact]
    public async Task ContractsCard_AddIsDisabledUntilThePropertyIsSaved()
    {
        var kit = new ContractTestKit(Today);
        var vm = kit.CreatePropertyEditor();
        await vm.LoadAsync(null);

        Assert.False(vm.CanAddContract);
        Assert.True(vm.ShowContractsSaveFirst);
        Assert.False(vm.AddContractCommand.CanExecute(null));

        vm.Name = "New flat";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.CanAddContract);
        Assert.False(vm.ShowContractsSaveFirst);
        Assert.True(vm.AddContractCommand.CanExecute(null));
    }

    [Fact]
    public async Task ContractsCard_AddOpensTheContractEditorForThisProperty_AndRefreshesAfterSave()
    {
        var kit = new ContractTestKit(Today);
        var supplier = new Supplier { Name = "Water utility" };
        kit.Suppliers.Stored.Add(supplier);
        var vm = kit.CreatePropertyEditor();
        await vm.LoadAsync(kit.Property.Id);
        Assert.True(vm.HasNoContracts);

        await vm.AddContractCommand.ExecuteAsync(null);

        var visit = Assert.Single(kit.Navigation.Visited);
        Assert.Equal(Routes.ContractEdit, visit.Route);
        var editor = Assert.IsType<ContractEditViewModel>(visit.Parameters![EditorNavigation.EditorParameter]);
        Assert.True(editor.IsHostedInPage);
        Assert.True(editor.IsNew);
        editor.ContractTitle = "Water";
        editor.SelectedSupplier = new ChoiceOption(supplier.Id, supplier.Name, string.Empty);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(kit.Property.Id, Assert.Single(kit.Contracts.Saved).PropertyId);
        Assert.Equal(["Water"], vm.ContractRows.Select(r => r.Title));
        Assert.False(vm.HasNoContracts);
    }

    [Fact]
    public async Task ContractsCard_RowOpensTheExistingContract()
    {
        var kit = new ContractTestKit(Today);
        var supplier = new Supplier { Name = "Water utility" };
        kit.Suppliers.Stored.Add(supplier);
        var water = Contract(kit, supplier, "Water", ServiceKind.ColdWater);
        var vm = kit.CreatePropertyEditor();
        await vm.LoadAsync(kit.Property.Id);

        vm.ContractRows[0].SelectCommand.Execute(null);
        await Task.Yield();

        var editor = Assert.IsType<ContractEditViewModel>(
            Assert.Single(kit.Navigation.Visited).Parameters![EditorNavigation.EditorParameter]);
        Assert.Equal(water.Id, editor.EntityId);
        Assert.Equal("Water", editor.ContractTitle);
    }

    private static Contract Contract(ContractTestKit kit, Supplier supplier, string title, ServiceKind kind)
    {
        var contract = new Contract
        {
            PropertyId = kit.Property.Id,
            SupplierId = supplier.Id,
            CategoryId = kit.CategoryIdFor(kind),
            Service = kind,
            Title = title,
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        kit.Contracts.Stored.Add(contract);
        return contract;
    }
}
