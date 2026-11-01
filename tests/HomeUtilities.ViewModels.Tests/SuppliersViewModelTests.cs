using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class SuppliersViewModelTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeSupplierStore _store = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeLayoutModeService _layout = new();
    private readonly SectionSelectionMemory _memory = new();
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task List_IsOrderedByNameIgnoringCase()
    {
        _store.Stored.AddRange([new Supplier { Name = "water" }, new Supplier { Name = "Gas" }, new Supplier { Name = "Electricity" }]);
        var vm = Create();

        await vm.ReloadAsync();

        Assert.Equal(["Electricity", "Gas", "water"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Search_FiltersByNameCaseInsensitiveContains()
    {
        _store.Stored.AddRange([new Supplier { Name = "Riga Water" }, new Supplier { Name = "Gas Co" }, new Supplier { Name = "Waterfall Internet" }]);
        var vm = Create();
        await vm.ReloadAsync();

        vm.SearchText = "WATER";

        Assert.Equal(["Riga Water", "Waterfall Internet"], vm.Items.Select(i => i.Name));

        vm.SearchText = "";

        Assert.Equal(3, vm.Items.Count);
    }

    [Fact]
    public async Task SearchWithoutMatch_IsNotTheEmptyState()
    {
        _store.Stored.Add(new Supplier { Name = "Gas Co" });
        var vm = Create();
        await vm.ReloadAsync();

        vm.SearchText = "zzz";

        Assert.Empty(vm.Items);
        Assert.False(vm.IsEmpty);
        Assert.True(vm.HasItems);
    }

    [Fact]
    public async Task NoSuppliers_ShowsEmptyState()
    {
        var vm = Create();
        Assert.False(vm.IsEmpty);

        await vm.ReloadAsync();

        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Row_ShowsLinkCountAndPaymentDetails()
    {
        _store.Stored.Add(new Supplier
        {
            Name = "Water",
            Links = [new Link { Url = "https://example.com" }, new Link { Url = "https://example.com/2" }, new Link { Url = "https://deleted.example", IsDeleted = true }],
            PaymentDetails = new(null, null, null, null, null, "x"),
        });
        _store.Stored.Add(new Supplier { Name = "Gas" });
        var vm = Create();

        await vm.ReloadAsync();

        var gas = vm.Items[0];
        var water = vm.Items[1];
        Assert.Equal("SupplierLinksCount 2 [en]", water.LinksText);
        Assert.True(water.HasPaymentDetails);
        Assert.False(gas.HasLinks);
        Assert.False(gas.HasPaymentDetails);
    }

    [Fact]
    public async Task Add_InCompactLayout_OpensEditorPage()
    {
        var vm = Create();

        await vm.AddCommand.ExecuteAsync(null);

        var visit = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.SupplierEdit, visit.Route);
        var editor = Assert.IsType<SupplierEditViewModel>(visit.Parameters![EditorNavigation.EditorParameter]);
        Assert.True(editor.IsNew);
    }

    [Fact]
    public async Task Select_OnAWideLayout_OpensTheEditorPageToo()
    {
        var supplier = new Supplier { Name = "Water" };
        _store.Stored.Add(supplier);
        _layout.Change(LayoutMode.Expanded);
        var vm = Create();
        await vm.ReloadAsync();

        await vm.SelectCommand.ExecuteAsync(supplier.Id);

        var visit = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.SupplierEdit, visit.Route);
        Assert.True(vm.IsWide);
        Assert.Equal("Water", vm.SelectedEditor!.Name);
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task Saving_ReloadsTheListAndKeepsTheEditorPage()
    {
        var vm = Create();
        await vm.ReloadAsync();
        await vm.AddCommand.ExecuteAsync(null);
        var editor = vm.SelectedEditor!;
        editor.Name = "New supplier";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Same(editor, vm.SelectedEditor);
        Assert.Equal(["New supplier"], vm.Items.Select(i => i.Name));
        Assert.True(vm.Items[0].IsSelected);
    }

    [Fact]
    public async Task Title_IsRefreshedAfterLanguageChange()
    {
        var vm = Create();
        Assert.Equal("SectionSuppliers [en]", vm.Title);

        _language.SetLanguage("ru");

        Assert.Equal("SectionSuppliers [ru]", vm.Title);
        await Task.CompletedTask;
    }

    private SuppliersViewModel Create()
    {
        var localizer = new FakeLocalizer(_language);
        return new SuppliersViewModel(
            _store,
            localizer,
            _navigation,
            _layout,
            _memory,
            () => new SupplierEditViewModel(
                _store, new TestClock(new DateOnly(2026, 10, 20)), localizer, _dialogs, _navigation,
                new FakeLauncherService(), new FakeClipboardService(), new FakeSnackbarService()));
    }
}
