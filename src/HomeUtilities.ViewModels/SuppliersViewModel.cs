using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The list of suppliers with a search box; two-pane like Properties.</summary>
public sealed partial class SuppliersViewModel : MasterDetailViewModel<SupplierEditViewModel>
{
    private readonly ISupplierStore _store;
    private readonly ILocalizer _localizer;

    private IReadOnlyList<Supplier> _suppliers = [];

    [ObservableProperty]
    private IReadOnlyList<SupplierItemViewModel> _items = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _searchPlaceholder = string.Empty;

    public SuppliersViewModel(
        ISupplierStore store,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        ISectionSelectionMemory selectionMemory,
        Func<SupplierEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.SupplierEdit, selectionMemory)
    {
        _store = store;
        _localizer = localizer;
        RefreshTexts();
    }

    protected override int TotalCount => _suppliers.Count;

    public override async Task ReloadAsync()
    {
        _suppliers = await _store.ListAsync();
        RebuildItems();
        IsLoaded = true;
        RaiseItemsChanged();
    }

    partial void OnSearchTextChanged(string value) => RebuildItems();

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionSuppliers"];
        EmptyText = _localizer["SuppliersEmpty"];
        AddText = _localizer["SupplierAdd"];
        SelectHintText = _localizer["SupplierSelectHint"];
        SearchPlaceholder = _localizer["SuppliersSearch"];
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _suppliers.Any(x => x.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedEditor is { IsNew: false } editor && editor.EntityId == item.Id;
        }
    }

    private void RebuildItems()
    {
        var query = SearchText.Trim();
        Items =
        [
            .. _suppliers
                .Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(s => new SupplierItemViewModel(s, _localizer, id => SelectCommand.Execute(id))),
        ];
        MarkSelectedItem();
    }
}
