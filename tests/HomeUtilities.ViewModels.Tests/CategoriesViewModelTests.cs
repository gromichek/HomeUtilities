using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class CategoriesViewModelTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeCategoryStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeLayoutModeService _layout = new();
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task BuiltInWithoutName_ShowsLocalizedServiceKindName()
    {
        _store.Stored.Add(new Category { SystemKey = ServiceKind.ColdWater, Name = null });
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal("Enum_ServiceKind_ColdWater [en]", vm.Items[0].DisplayName);
        Assert.True(vm.Items[0].IsBuiltIn);
    }

    [Fact]
    public async Task BuiltInWithName_AndUserCategory_ShowTheirOwnName()
    {
        _store.Stored.AddRange([
            new Category { SystemKey = ServiceKind.Gas, Name = "Gas (Latvijas Gāze)", SortOrder = 0 },
            new Category { SystemKey = null, Name = "Kindergarten", SortOrder = 1 },
        ]);
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal(["Gas (Latvijas Gāze)", "Kindergarten"], vm.Items.Select(i => i.DisplayName));
        Assert.Equal([true, false], vm.Items.Select(i => i.IsBuiltIn));
    }

    [Fact]
    public async Task DisplayName_FollowsLanguageChange()
    {
        _store.Stored.Add(new Category { SystemKey = ServiceKind.Heating });
        var vm = Create();
        await vm.ReloadAsync();

        _language.SetLanguage("ru");

        Assert.Equal("Enum_ServiceKind_Heating [ru]", vm.Items[0].DisplayName);
    }

    [Fact]
    public async Task List_IsOrderedBySortOrder()
    {
        _store.Stored.AddRange([
            new Category { Name = "C", SortOrder = 2 },
            new Category { Name = "A", SortOrder = 0 },
            new Category { Name = "B", SortOrder = 1 },
        ]);
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal(["A", "B", "C"], vm.Items.Select(i => i.DisplayName));
    }

    [Fact]
    public async Task ArchivedCategories_AreHiddenUnlessToggled()
    {
        _store.Stored.AddRange([
            new Category { Name = "Active", SortOrder = 0 },
            new Category { Name = "Old", SortOrder = 1, IsArchived = true },
        ]);
        var vm = Create();
        await vm.ReloadAsync();
        Assert.Equal(["Active"], vm.Items.Select(i => i.DisplayName));

        vm.ShowArchived = true;

        Assert.Equal(["Active", "Old"], vm.Items.Select(i => i.DisplayName));
        Assert.True(vm.Items[1].IsArchived);
    }

    [Fact]
    public async Task NewCategory_GetsNextUnusedColourAndMaxPlusOneSortOrder()
    {
        _store.Stored.AddRange([
            new Category { Name = "A", SortOrder = 3, Color = CategoryNames.Palette[0] },
            new Category { Name = "B", SortOrder = 7, Color = CategoryNames.Palette[1].ToLowerInvariant(), IsArchived = true },
            new Category { Name = "C", SortOrder = 5, Color = "#123456" },
        ]);
        var vm = Create();
        await vm.ReloadAsync();

        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;

        Assert.True(editor.IsNew);
        Assert.Equal(CategoryNames.Palette[2], editor.Color);

        editor.Name = "Kindergarten";
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Null(saved.SystemKey);
        Assert.Equal(8, saved.SortOrder);
        Assert.Equal("Kindergarten", saved.Name);
        Assert.Equal(CategoryNames.Palette[2], saved.Color);
    }

    [Fact]
    public void NextColor_CyclesWhenAllPaletteColoursAreUsed()
    {
        var categories = CategoryNames.Palette.Select(c => new Category { Color = c }).ToList();

        Assert.Equal(CategoryNames.Palette[0], CategoryNames.NextColor(categories));
    }

    [Fact]
    public void Palette_Has12ValidSwatches()
    {
        Assert.Equal(12, CategoryNames.Palette.Count);
        Assert.Equal(12, CategoryNames.Palette.Distinct().Count());
        Assert.All(CategoryNames.Palette, c => Assert.Empty(CategoryValidator.Validate(new Category { Name = "x", Color = c }).Errors));
    }

    [Fact]
    public async Task Add_InCompactLayout_OpensEditorPage()
    {
        var vm = Create();

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(Routes.CategoryEdit, Assert.Single(_navigation.Visited).Route);
    }

    [Fact]
    public async Task NoCategories_ShowsEmptyState()
    {
        var vm = Create();

        await vm.ReloadAsync();

        Assert.True(vm.IsEmpty);
    }

    private CategoriesViewModel Create()
    {
        var localizer = new FakeLocalizer(_language);
        return new CategoriesViewModel(
            _store, localizer, _navigation, _layout,
            () => new CategoryEditViewModel(_store, localizer, _dialogs, _navigation, new FakeToastService()));
    }
}
