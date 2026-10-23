using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class CategoryEditViewModelTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeCategoryStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task BuiltIn_HasNoDelete_AndShowsDefaultNameHint()
    {
        var water = new Category { SystemKey = ServiceKind.ColdWater };
        _store.Stored.Add(water);
        var vm = await LoadAsync(water);
        vm.IsHostedInPage = true;

        Assert.True(vm.IsBuiltIn);
        Assert.False(vm.CanShowDelete);
        Assert.False(vm.ShowFormDelete);
        Assert.True(vm.HasNameHint);
        Assert.Equal("CategoryNameDefaultHint Enum_ServiceKind_ColdWater [en] [en]", vm.NameHint);
        Assert.Equal(string.Empty, vm.Name);
    }

    [Fact]
    public async Task UserCategory_CanBeDeleted_Softly()
    {
        var own = new Category { Name = "Kindergarten" };
        _store.Stored.Add(own);
        var vm = await LoadAsync(own);
        vm.IsHostedInPage = true;

        Assert.True(vm.CanShowDelete);
        Assert.True(vm.ShowFormDelete);
        Assert.False(vm.HasNameHint);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([own.Id], _store.Deleted);
        Assert.Equal("Dialog_Delete_Title Kindergarten [en]", _dialogs.Confirmations[0].Title);
    }

    [Fact]
    public async Task NewCategory_HasNoDelete()
    {
        var vm = await LoadAsync(null);
        vm.IsHostedInPage = true;

        Assert.False(vm.CanShowDelete);
        Assert.False(vm.ShowFormDelete);
    }

    [Fact]
    public async Task ClearingTheNameOfABuiltIn_StoresNull()
    {
        var gas = new Category { SystemKey = ServiceKind.Gas, Name = "Gas supply", Color = "#112233", SortOrder = 4 };
        _store.Stored.Add(gas);
        var vm = await LoadAsync(gas);
        Assert.Equal("Gas supply", vm.Name);
        vm.Name = "  ";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Null(saved.Name);
        Assert.Equal(ServiceKind.Gas, saved.SystemKey);
        Assert.Equal(4, saved.SortOrder);
        Assert.Equal(gas.Id, saved.Id);
    }

    [Fact]
    public async Task RenamingAndRecolouringABuiltIn_IsSaved()
    {
        var water = new Category { SystemKey = ServiceKind.ColdWater, Color = "#1E88E5" };
        _store.Stored.Add(water);
        var vm = await LoadAsync(water);
        vm.Name = "  Вода Рига ";
        vm.Swatches[1].SelectCommand.Execute(null);
        vm.IsArchived = true;
        vm.Notes = "main supply";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Equal("Вода Рига", saved.Name);
        Assert.Equal(CategoryNames.Palette[1], saved.Color);
        Assert.True(saved.IsArchived);
        Assert.Equal("main supply", saved.Notes);
        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    [InlineData("")]
    public async Task BadHexColour_ShowsErrorAndDoesNotSave(string color)
    {
        var vm = await LoadAsync(null);
        vm.Name = "Kindergarten";
        vm.Color = color;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.Equal("Error_category_color_invalid [en]", vm.ColorError);
    }

    [Fact]
    public async Task HexColour_IsTrimmedAndUpperCased()
    {
        var vm = await LoadAsync(null);
        vm.Name = "Kindergarten";
        vm.Color = " #aabbcc ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("#AABBCC", Assert.Single(_store.Saved).Color);
    }

    [Fact]
    public async Task UserCategoryWithoutName_ShowsError()
    {
        var vm = await LoadAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
        Assert.Equal("Error_category_name_required [en]", vm.NameError);
    }

    [Fact]
    public async Task SelectedSwatch_FollowsTheHexField()
    {
        var vm = await LoadAsync(null);

        vm.Color = CategoryNames.Palette[4].ToLowerInvariant();

        Assert.Equal([4], vm.Swatches.Select((s, i) => (s, i)).Where(t => t.s.IsSelected).Select(t => t.i));

        vm.Color = "#010203";

        Assert.DoesNotContain(vm.Swatches, s => s.IsSelected);
        Assert.Equal(12, vm.Swatches.Count);
    }

    [Fact]
    public async Task Cancel_WithoutChanges_DoesNotAsk()
    {
        var vm = await LoadAsync(null);
        vm.IsHostedInPage = true;

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(0, _dialogs.UnsavedAsked);
        Assert.Equal(1, _navigation.BackCount);
    }

    private async Task<CategoryEditViewModel> LoadAsync(Category? category)
    {
        var vm = new CategoryEditViewModel(_store, new FakeLocalizer(_language), _dialogs, _navigation, new FakeToastService());
        await vm.LoadAsync(category?.Id);
        return vm;
    }
}
