using HomeUtilities.ViewModels.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The central guard asks the editor in the side pane (two-pane layout) through the list view model.</summary>
public class PaneGuardTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakePropertyStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeLayoutModeService _layout = new();
    private readonly TestClock _clock = new(new DateOnly(2026, 10, 24));

    [Fact]
    public async Task CleanPane_LeavesWithoutDialog()
    {
        var (vm, _) = await OpenAsync();

        Assert.False(((IUnsavedChangesGuarded)vm).HasUnsavedChanges);
        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task DirtyPane_Cancel_StaysAndKeepsInput()
    {
        var (vm, editor) = await OpenAsync();
        editor.Name = "Changed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        Assert.False(await vm.CanLeaveAsync());

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal("Changed", editor.Name);
    }

    [Fact]
    public async Task DirtyPane_DontSave_DiscardsInputButKeepsSelection()
    {
        var (vm, editor) = await OpenAsync();
        editor.Name = "Changed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;

        Assert.True(await vm.CanLeaveAsync());

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal("Flat", editor.Name);
        Assert.False(((IUnsavedChangesGuarded)vm).HasUnsavedChanges);
    }

    [Fact]
    public async Task DirtyPane_Save_StoresTheChange()
    {
        var (vm, editor) = await OpenAsync();
        editor.Name = "Renamed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.True(await vm.CanLeaveAsync());

        Assert.Equal("Renamed", _store.Stored.Single().Name);
        Assert.Same(editor, vm.SelectedEditor);
    }

    [Fact]
    public async Task DirtyPane_SaveWithInvalidInput_Stays()
    {
        var (vm, editor) = await OpenAsync();
        editor.Name = "";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.False(await vm.CanLeaveAsync());

        Assert.True(editor.HasErrors);
        Assert.Equal("Flat", _store.Stored.Single().Name);
    }

    [Fact]
    public async Task NewUnsavedItem_DontSave_ClosesThePane()
    {
        _layout.Change(LayoutMode.Expanded);
        var vm = Create();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        vm.SelectedEditor!.Name = "Typed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;

        Assert.True(await vm.CanLeaveAsync());

        Assert.Null(vm.SelectedEditor);
        Assert.Empty(_store.Stored);
    }

    [Fact]
    public async Task EditorOnItsOwnPage_IsNotGuardedByTheList()
    {
        _layout.Change(LayoutMode.Compact);
        _store.Stored.Add(NewProperty("Flat"));
        var vm = Create();
        await vm.ReloadAsync();
        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);
        vm.SelectedEditor!.Name = "Changed";

        Assert.False(((IUnsavedChangesGuarded)vm).HasUnsavedChanges);
        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _dialogs.UnsavedAsked);
    }

    private async Task<(PropertiesViewModel Vm, PropertyEditViewModel Editor)> OpenAsync()
    {
        _layout.Change(LayoutMode.Expanded);
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
        new SectionSelectionMemory(),
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
