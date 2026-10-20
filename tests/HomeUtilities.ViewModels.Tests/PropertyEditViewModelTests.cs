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
        AddParameter(vm, PropertyParameterKind.TotalArea, "52,3", new(2020, 1, 1));
        AddParameter(vm, PropertyParameterKind.DeclaredResidents, "3", new(2020, 1, 1));

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
    public async Task Save_InPage_NavigatesBackAndReportsOutcome()
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

        Assert.Equal(1, _navigation.BackCount);
        Assert.Equal(EditOutcome.Saved, outcome);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("2,5")]
    public async Task AddingFractionalResidents_ShowsErrorInTheForm(string text)
    {
        var vm = await CreateNewAsync();
        vm.AddParameterCommand.Execute(null);
        vm.FormKind = Option(vm.ParameterKinds, PropertyParameterKind.DeclaredResidents);
        vm.FormValueText = text;

        vm.ApplyParameterCommand.Execute(null);

        Assert.True(vm.IsParameterFormVisible);
        Assert.Equal("Error_property_parameter_residents_not_whole [en]", vm.FormValueError);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task AddingTextAsValue_ShowsNumberError()
    {
        var vm = await CreateNewAsync();
        vm.AddParameterCommand.Execute(null);
        vm.FormValueText = "abc";

        vm.ApplyParameterCommand.Execute(null);

        Assert.Equal("Error_common_number_invalid [en]", vm.FormValueError);
    }

    [Fact]
    public async Task AddingCustomParameterWithoutName_ShowsNameError()
    {
        var vm = await CreateNewAsync();
        vm.AddParameterCommand.Execute(null);
        vm.FormKind = Option(vm.ParameterKinds, PropertyParameterKind.Custom);
        vm.FormValueText = "5";

        vm.ApplyParameterCommand.Execute(null);

        Assert.True(vm.IsFormCustom);
        Assert.Equal("Error_property_parameter_name_required [en]", vm.FormNameError);
    }

    [Fact]
    public async Task AddingSameKindWithSameValidFrom_ShowsDuplicateError()
    {
        var vm = await CreateNewAsync();
        AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        vm.AddParameterCommand.Execute(null);
        vm.FormValueText = "55";
        vm.FormValidFrom = new(2020, 1, 1);

        vm.ApplyParameterCommand.Execute(null);

        Assert.StartsWith("Error_versioned_duplicate_valid_from", vm.FormValidFromError);
        Assert.Single(vm.Rows);
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
        AddParameter(vm, PropertyParameterKind.DeclaredResidents, "3", new(2020, 1, 1));
        AddParameter(vm, PropertyParameterKind.DeclaredResidents, "2", new(2026, 6, 1));
        AddParameter(vm, PropertyParameterKind.DeclaredResidents, "1", new(2027, 1, 1));
        AddParameter(vm, PropertyParameterKind.TotalArea, "52,3", new(2020, 1, 1));
        AddParameter(vm, PropertyParameterKind.Custom, "7", new(2021, 1, 1), name: "Parking places");

        Assert.Equal(
            ["Enum_PropertyParameterKind_TotalArea [en]", "Enum_PropertyParameterKind_DeclaredResidents [en]", "Parking places"],
            vm.Groups.Select(g => g.Title));
        var residents = vm.Groups[1];
        Assert.Equal([new DateOnly(2027, 1, 1), new(2026, 6, 1), new(2020, 1, 1)], residents.Rows.Select(r => r.ValidFrom));
        Assert.Equal("ParameterCurrent 2 [en]", residents.CurrentText);
        Assert.Equal("ParameterCurrent 52.3 [en]", vm.Groups[0].CurrentText);
    }

    [Fact]
    public async Task Group_WithOnlyFutureValues_HasNoCurrentValue()
    {
        var vm = await CreateNewAsync();
        AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2030, 1, 1));

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
        AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        _dialogs.ConfirmResult = false;

        await vm.RemoveParameterCommand.ExecuteAsync(vm.Rows[0]);

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task EditParameter_ChangesTheRowInPlace()
    {
        var vm = await CreateNewAsync();
        AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));
        var row = vm.Rows[0];

        vm.EditParameterCommand.Execute(row);
        Assert.Equal("50", vm.FormValueText);
        vm.FormValueText = "51,5";
        vm.ApplyParameterCommand.Execute(null);

        Assert.Single(vm.Rows);
        Assert.Equal(51.5m, row.Value);
        Assert.False(vm.IsParameterFormVisible);
    }

    [Fact]
    public async Task Cancel_WithChanges_AsksToDiscard()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;
        vm.Name = "Typed";
        _dialogs.ConfirmResult = false;

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirmations);
        Assert.Equal(0, _navigation.BackCount);
        Assert.True(vm.IsDirty);

        _dialogs.ConfirmResult = true;
        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, _navigation.BackCount);
    }

    [Fact]
    public async Task Cancel_WithoutChanges_DoesNotAsk()
    {
        var vm = await CreateNewAsync();
        vm.IsHostedInPage = true;

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal(1, _navigation.BackCount);
    }

    [Fact]
    public async Task Delete_AsksToConfirmAndDeletes()
    {
        var stored = new Property { Name = "Cottage" };
        _store.Stored.Add(stored);
        var vm = Create();
        await vm.LoadAsync(stored.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("PropertyDeleteMessage Cottage [en]", message);
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
        AddParameter(vm, PropertyParameterKind.TotalArea, "50", new(2020, 1, 1));

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
            AddParameter(vm, PropertyParameterKind.TotalArea, "52,3", new(2020, 1, 1));
            AddParameter(vm, PropertyParameterKind.HeatedArea, "52.3", new(2020, 1, 1));

            Assert.Equal(52.3m, vm.Rows[0].Value);
            Assert.Equal(52.3m, vm.Rows[1].Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private PropertyEditViewModel Create() =>
        new(_store, new TestClock(Today), new FakeLocalizer(_language), _dialogs, _navigation);

    private async Task<PropertyEditViewModel> CreateNewAsync()
    {
        var vm = Create();
        await vm.LoadAsync(null);
        return vm;
    }

    private static EnumOption Option<TEnum>(IReadOnlyList<EnumOption> options, TEnum value)
        where TEnum : struct, Enum => options.First(o => o.Value.Equals(value));

    private static void AddParameter(
        PropertyEditViewModel vm, PropertyParameterKind kind, string valueText, DateOnly validFrom, string name = "")
    {
        vm.AddParameterCommand.Execute(null);
        vm.FormKind = Option(vm.ParameterKinds, kind);
        vm.FormName = name;
        vm.FormValueText = valueText;
        vm.FormValidFrom = validFrom;
        vm.ApplyParameterCommand.Execute(null);
        Assert.False(vm.IsParameterFormVisible, vm.FormValueError + vm.FormNameError + vm.FormValidFromError);
    }
}
