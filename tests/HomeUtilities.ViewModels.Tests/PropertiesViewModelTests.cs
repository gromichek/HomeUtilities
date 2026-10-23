using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class PropertiesViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly FakeLanguageService _language = new();
    private readonly FakePropertyStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeLayoutModeService _layout = new();
    private readonly TestClock _clock = new(Today);

    [Fact]
    public async Task List_IsSortedBySortOrderThenName()
    {
        _store.Stored.AddRange([
            Prop("Zeta", sortOrder: 0),
            Prop("Beta", sortOrder: 1),
            Prop("Alpha", sortOrder: 1),
        ]);
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal(["Zeta", "Alpha", "Beta"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task ArchivedProperties_AreHiddenUnlessToggled()
    {
        _store.Stored.AddRange([Prop("Flat"), Prop("Old cottage", archived: true)]);
        var vm = Create();
        await vm.ReloadAsync();
        Assert.Equal(["Flat"], vm.Items.Select(i => i.Name));

        vm.ShowArchived = true;
        await vm.PendingReload;

        Assert.Equal(["Flat", "Old cottage"], vm.Items.Select(i => i.Name));
        Assert.True(vm.Items[1].IsArchived);
        Assert.False(vm.Items[0].IsArchived);
    }

    [Fact]
    public async Task Summary_UsesResidentsAndAreaAsOfToday()
    {
        var flat = Prop("Flat");
        flat.Parameters.AddRange([
            Param(PropertyParameterKind.DeclaredResidents, new(2020, 1, 1), 3),
            Param(PropertyParameterKind.DeclaredResidents, new(2026, 6, 1), 2),
            Param(PropertyParameterKind.DeclaredResidents, new(2027, 1, 1), 1),
            Param(PropertyParameterKind.TotalArea, new(2020, 1, 1), 52.30m),
        ]);
        _store.Stored.Add(flat);
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal("PropertySummaryResidents 2 [en] · PropertySummaryArea 52.3 [en]", vm.Items[0].SummaryText);
        Assert.Equal("Enum_PropertyKind_Apartment [en]", vm.Items[0].KindText);
    }

    [Fact]
    public async Task Summary_IsEmptyWithoutParameters()
    {
        _store.Stored.Add(Prop("Flat"));
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal(string.Empty, vm.Items[0].SummaryText);
    }

    [Fact]
    public async Task EmptyState_IsShownOnlyAfterLoadingAnEmptyList()
    {
        var vm = Create();
        Assert.False(vm.IsEmpty);

        await vm.ReloadAsync();

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasItems);
    }

    [Fact]
    public async Task Add_InTwoPaneLayout_CreatesNewEditorWithoutNavigation()
    {
        _layout.Change(LayoutMode.Expanded);
        var vm = Create();

        await vm.AddCommand.ExecuteAsync(null);

        Assert.NotNull(vm.SelectedEditor);
        Assert.True(vm.SelectedEditor.IsNew);
        Assert.False(vm.SelectedEditor.IsHostedInPage);
        Assert.Empty(_navigation.Visited);
        Assert.True(vm.IsTwoPane);
    }

    [Fact]
    public async Task Select_InTwoPaneLayout_LoadsTheProperty()
    {
        _layout.Change(LayoutMode.Medium);
        var flat = Prop("Flat");
        _store.Stored.Add(flat);
        var vm = Create();
        await vm.ReloadAsync();

        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        Assert.Equal(flat.Id, vm.SelectedEditor!.EntityId);
        Assert.Equal("Flat", vm.SelectedEditor.Name);
        Assert.False(vm.SelectedEditor.IsNew);
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task Select_InCompactLayout_NavigatesToEditPageWithTheEditor()
    {
        _layout.Change(LayoutMode.Compact);
        _store.Stored.Add(Prop("Flat"));
        var vm = Create();
        await vm.ReloadAsync();

        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        var visit = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.PropertyEdit, visit.Route);
        Assert.Same(vm.SelectedEditor, visit.Parameters![EditorNavigation.EditorParameter]);
        Assert.True(vm.SelectedEditor!.IsHostedInPage);
    }

    [Fact]
    public async Task SelectingAnother_WithUnsavedChanges_AsksUnsavedChangesDialog()
    {
        _layout.Change(LayoutMode.Expanded);
        _store.Stored.AddRange([Prop("Flat"), Prop("House", sortOrder: 1)]);
        var vm = Create();
        await vm.ReloadAsync();
        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);
        var first = vm.SelectedEditor!;
        first.Name = "Changed";
        _dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        await vm.SelectCommand.ExecuteAsync(vm.Items[1].Id);

        Assert.Same(first, vm.SelectedEditor);
        Assert.Equal(1, _dialogs.UnsavedAsked);

        _dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;
        await vm.SelectCommand.ExecuteAsync(vm.Items[1].Id);

        Assert.NotSame(first, vm.SelectedEditor);
        Assert.Equal("House", vm.SelectedEditor!.Name);
    }

    [Fact]
    public async Task NarrowingTheWindowWhileEditing_MovesTheSameEditorToAPage()
    {
        _layout.Change(LayoutMode.Expanded);
        var vm = Create();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.Name = "Typed but not saved";

        _layout.Change(LayoutMode.Compact);
        await vm.PendingLayoutChange;

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal("Typed but not saved", editor.Name);
        Assert.True(editor.IsHostedInPage);
        Assert.Same(editor, _navigation.Visited.Single().Parameters![EditorNavigation.EditorParameter]);
        Assert.False(vm.IsTwoPane);
    }

    [Fact]
    public async Task WideningTheWindowWhileEditing_PopsThePageAndKeepsTheEditor()
    {
        _layout.Change(LayoutMode.Compact);
        var vm = Create();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;

        _layout.Change(LayoutMode.Expanded);
        await vm.PendingLayoutChange;

        Assert.Same(editor, vm.SelectedEditor);
        Assert.False(editor.IsHostedInPage);
        Assert.Equal(1, _navigation.BackCount);
    }

    [Fact]
    public async Task WideningTheWindowWhileAChildPageIsOpen_DoesNotPopTheChildPage()
    {
        _layout.Change(LayoutMode.Compact);
        var vm = Create();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        _navigation.Top = new object(); // e.g. the contract page is on top of the property editor page

        _layout.Change(LayoutMode.Expanded);
        await vm.PendingLayoutChange;

        Assert.Same(editor, vm.SelectedEditor);
        Assert.False(editor.IsHostedInPage);
        Assert.Equal(0, _navigation.BackCount);
    }

    [Fact]
    public async Task SavingInCompactLayout_KeepsTheEditorOpenAndSelected()
    {
        _layout.Change(LayoutMode.Compact);
        var vm = Create();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        ((PropertyEditViewModel)editor).Name = "Flat";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal(0, _navigation.BackCount);
        Assert.Single(_store.Saved);
    }

    [Fact]
    public async Task SavingInTwoPaneLayout_RefreshesTheListAndKeepsTheEditor()
    {
        _layout.Change(LayoutMode.Expanded);
        var vm = Create();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.Name = "Flat";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal(["Flat"], vm.Items.Select(i => i.Name));
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task DeletingInTwoPaneLayout_ClosesTheEditorAndRefreshesTheList()
    {
        _layout.Change(LayoutMode.Expanded);
        _store.Stored.Add(Prop("Flat"));
        var vm = Create();
        await vm.ReloadAsync();
        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        await vm.SelectedEditor!.DeleteCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedEditor);
        Assert.Empty(vm.Items);
        Assert.Equal(0, _navigation.BackCount);
    }

    [Fact]
    public async Task Texts_AreRefreshedAfterLanguageChanged()
    {
        _store.Stored.Add(Prop("Flat"));
        var vm = Create();
        await vm.ReloadAsync();

        _language.SetLanguage("ru");

        Assert.Equal("PropertiesEmpty [ru]", vm.EmptyText);
        Assert.Equal("Enum_PropertyKind_Apartment [ru]", vm.Items[0].KindText);
    }

    private PropertiesViewModel Create() => new(
        _store,
        _clock,
        new FakeLocalizer(_language),
        _navigation,
        _layout,
        () => new PropertyEditViewModel(
            _store,
            new FakeContractStore(),
            new FakeSupplierStore(),
            new FakeCategoryStore(),
            _clock,
            new FakeLocalizer(_language),
            _dialogs,
            _navigation,
            new FakeToastService(),
            () => throw new NotSupportedException()));

    private static Property Prop(string name, int sortOrder = 0, bool archived = false) => new()
    {
        Name = name,
        Kind = PropertyKind.Apartment,
        SortOrder = sortOrder,
        IsArchived = archived,
    };

    private static PropertyParameter Param(PropertyParameterKind kind, DateOnly from, decimal value) =>
        new() { Kind = kind, ValidFrom = from, Value = value };

    [Fact]
    public void AddControls_FollowTheLayout()
    {
        var vm = Create();
        Assert.True(vm.ShowAddFab);
        Assert.False(vm.ShowAddButton);

        _layout.Change(LayoutMode.Expanded);

        Assert.False(vm.ShowAddFab);
        Assert.True(vm.ShowAddButton);
    }
}
