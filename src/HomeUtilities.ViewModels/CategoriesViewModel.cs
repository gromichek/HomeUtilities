using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The list of payment categories (built-in and own), ordered by sort order.</summary>
public sealed partial class CategoriesViewModel : MasterDetailViewModel<CategoryEditViewModel>
{
    protected override HelpTopic GuideTopic => HelpTopic.Categories;

    private readonly ICategoryStore _store;
    private readonly ILocalizer _localizer;

    private IReadOnlyList<Category> _categories = [];

    [ObservableProperty]
    private IReadOnlyList<CategoryItemViewModel> _items = [];

    [ObservableProperty]
    private bool _showArchived;

    [ObservableProperty]
    private string _showArchivedText = string.Empty;

    public CategoriesViewModel(
        ICategoryStore store,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        ISectionSelectionMemory selectionMemory,
        Func<CategoryEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.CategoryEdit, selectionMemory)
    {
        _store = store;
        _localizer = localizer;
        RefreshTexts();
    }

    protected override int TotalCount => Items.Count;

    public override async Task ReloadAsync()
    {
        _categories = await _store.ListAsync();
        RebuildItems();
        IsLoaded = true;
    }

    partial void OnShowArchivedChanged(bool value) => RebuildItems();

    partial void OnItemsChanged(IReadOnlyList<CategoryItemViewModel> value) => RaiseItemsChanged();

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionCategories"];
        EmptyText = _localizer["CategoriesEmpty"];
        AddText = _localizer["CategoryAdd"];
        HelpText = _localizer["SectionHelp"];
        ShowArchivedText = _localizer["CategoriesShowArchived"];
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _categories.Any(x => x.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = HighlightedId == item.Id;
        }
    }

    private void RebuildItems()
    {
        Items =
        [
            .. _categories
                .Where(c => ShowArchived || !c.IsArchived)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => CategoryNames.DisplayName(c, _localizer), StringComparer.CurrentCultureIgnoreCase)
                .Select(c => new CategoryItemViewModel(c, _localizer, id => SelectCommand.Execute(id))),
        ];
        MarkSelectedItem();
    }
}
