using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The highlight of the last opened item survives a recreated page / view model (menu → other section → menu).</summary>
public class SectionSelectionMemoryTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakePropertyStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeLayoutModeService _layout = new();
    private readonly TestClock _clock = new(new DateOnly(2026, 10, 24));
    private readonly SectionSelectionMemory _memory = new();

    [Fact]
    public async Task RecreatedViewModel_HighlightsTheLastOpenedItem()
    {
        var (first, _) = await SelectFirstAsync();

        var second = Create();
        await second.OnAppearingAsync();

        Assert.NotSame(first, second);
        Assert.Null(second.SelectedEditor);
        Assert.True(second.Items[0].IsSelected);
        Assert.Single(_navigation.Visited);
        Assert.Equal(0, _dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task ReturningToTheSameViewModel_DetachesTheEditorAndKeepsTheHighlight()
    {
        var (vm, editor) = await SelectFirstAsync();

        await vm.OnAppearingAsync();

        Assert.Null(vm.SelectedEditor);
        Assert.Null(editor.Finished);
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task DeletedItem_IsNotHighlighted()
    {
        await SelectFirstAsync();
        _store.Stored.Clear();

        var second = Create();
        await second.OnAppearingAsync();

        Assert.Empty(second.Items);
    }

    [Fact]
    public async Task ArchivedItemHiddenByTheFilter_IsNotHighlighted()
    {
        await SelectFirstAsync();
        _store.Stored.Single().IsArchived = true;

        var second = Create();
        await second.OnAppearingAsync();

        Assert.Empty(second.Items);
    }

    [Fact]
    public async Task Delete_ForgetsTheSelection()
    {
        var (_, editor) = await SelectFirstAsync();

        await editor.DeleteCommand.ExecuteAsync(null);

        Assert.Null(_memory.Recall(Routes.PropertyEdit));
    }

    [Fact]
    public async Task Cancel_KeepsTheSelection()
    {
        var (_, editor) = await SelectFirstAsync();

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Equal(editor.EntityId, _memory.Recall(Routes.PropertyEdit));
    }

    [Fact]
    public async Task NewUnsavedItem_IsNotRemembered()
    {
        var vm = Create();
        await vm.ReloadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        Assert.NotNull(vm.SelectedEditor);
        Assert.Null(_memory.Recall(Routes.PropertyEdit));
    }

    [Fact]
    public async Task NewItem_IsRememberedOnceSaved()
    {
        var vm = Create();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.Name = "Cottage";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(editor.EntityId, _memory.Recall(Routes.PropertyEdit));
    }

    [Fact]
    public void Memory_IsPerSection()
    {
        var id = Guid.NewGuid();
        _memory.Remember(Routes.PropertyEdit, id);

        Assert.Equal(id, _memory.Recall(Routes.PropertyEdit));
        Assert.Null(_memory.Recall(Routes.SupplierEdit));
    }

    private async Task<(PropertiesViewModel Vm, PropertyEditViewModel Editor)> SelectFirstAsync()
    {
        _store.Stored.Add(NewProperty("Flat"));
        var vm = Create();
        await vm.ReloadAsync();
        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);
        return (vm, vm.SelectedEditor!);
    }

    private PropertiesViewModel Create() => new(
        _store,
        _clock,
        new FakeLocalizer(_language),
        _navigation,
        _layout,
        _memory,
        () => new PropertyEditViewModel(
            _store,
            new FakeContractStore(),
            new FakeMeterStore(),
            new FakeSupplierStore(),
            new FakeCategoryStore(),
            _clock,
            new FakeLocalizer(_language),
            _dialogs,
            _navigation,
            new FakeSnackbarService(),
            () => throw new NotSupportedException(),
            () => throw new NotSupportedException()));

    private static Property NewProperty(string name) => new() { Name = name, Kind = PropertyKind.Apartment };
}
