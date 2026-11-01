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
    private readonly SectionSelectionMemory _memory = new();
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

    [Theory]
    [InlineData(LayoutMode.Compact)]
    [InlineData(LayoutMode.Medium)]
    [InlineData(LayoutMode.Expanded)]
    public async Task Add_OpensANewEditorPage_OnEveryLayout(LayoutMode mode)
    {
        _layout.Change(mode);
        var vm = Create();

        await vm.AddCommand.ExecuteAsync(null);

        Assert.NotNull(vm.SelectedEditor);
        Assert.True(vm.SelectedEditor.IsNew);
        var visit = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.PropertyEdit, visit.Route);
        Assert.Same(vm.SelectedEditor, visit.Parameters![EditorNavigation.EditorParameter]);
    }

    [Theory]
    [InlineData(LayoutMode.Compact)]
    [InlineData(LayoutMode.Medium)]
    [InlineData(LayoutMode.Expanded)]
    public async Task Select_NavigatesToTheEditPageWithTheLoadedEditor_OnEveryLayout(LayoutMode mode)
    {
        _layout.Change(mode);
        var flat = Prop("Flat");
        _store.Stored.Add(flat);
        var vm = Create();
        await vm.ReloadAsync();

        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        var visit = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.PropertyEdit, visit.Route);
        Assert.Same(vm.SelectedEditor, visit.Parameters![EditorNavigation.EditorParameter]);
        Assert.Equal(flat.Id, vm.SelectedEditor!.EntityId);
        Assert.Equal("Flat", vm.SelectedEditor.Name);
        Assert.False(vm.SelectedEditor.IsNew);
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task Saving_KeepsTheEditorPageOpenAndRefreshesTheList()
    {
        var vm = Create();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.Name = "Flat";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal(0, _navigation.BackCount);
        Assert.Single(_store.Saved);
        Assert.Equal(["Flat"], vm.Items.Select(i => i.Name));
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task Cancelling_GoesBackAndKeepsTheOpenedItemHighlighted()
    {
        _store.Stored.Add(Prop("Flat"));
        var vm = Create();
        await vm.ReloadAsync();
        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        await vm.SelectedEditor!.CancelCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedEditor);
        Assert.Equal(1, _navigation.BackCount);
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task Deleting_GoesBackAndRefreshesTheList()
    {
        _store.Stored.Add(Prop("Flat"));
        var vm = Create();
        await vm.ReloadAsync();
        await vm.SelectCommand.ExecuteAsync(vm.Items[0].Id);

        await vm.SelectedEditor!.DeleteCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedEditor);
        Assert.Empty(vm.Items);
        Assert.Equal(1, _navigation.BackCount);
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
