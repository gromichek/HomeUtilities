using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The list of bill templates (opened from Settings); two-pane like Properties, the editor is <see cref="BillTemplateEditViewModel"/>.</summary>
public sealed partial class BillTemplatesViewModel : MasterDetailViewModel<BillTemplateEditViewModel>
{
    protected override HelpTopic GuideTopic => HelpTopic.BillTemplates;

    private readonly IBillTemplateStore _store;
    private readonly IPropertyStore _propertyStore;
    private readonly ICategoryStore _categoryStore;
    private readonly ILocalizer _localizer;

    private IReadOnlyList<BillTemplate> _templates = [];
    private IReadOnlyList<Property> _properties = [];
    private IReadOnlyList<Category> _categories = [];

    [ObservableProperty]
    private IReadOnlyList<BillTemplateItemViewModel> _items = [];

    public BillTemplatesViewModel(
        IBillTemplateStore store,
        IPropertyStore propertyStore,
        ICategoryStore categoryStore,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        ISectionSelectionMemory selectionMemory,
        Func<BillTemplateEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.BillTemplateEdit, selectionMemory)
    {
        _store = store;
        _propertyStore = propertyStore;
        _categoryStore = categoryStore;
        _localizer = localizer;
        RefreshTexts();
    }

    protected override int TotalCount => _templates.Count;

    public override async Task ReloadAsync()
    {
        _templates = await _store.ListAsync();
        _properties = await _propertyStore.ListAsync(true);
        _categories = await _categoryStore.ListAsync();
        RebuildItems();
        IsLoaded = true;
        RaiseItemsChanged();
    }

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionBillTemplates"];
        EmptyText = _localizer["BillTemplatesEmpty"];
        AddText = _localizer["BillTemplateAdd"];
        HelpText = _localizer["SectionHelp"];
        SelectHintText = _localizer["BillTemplateSelectHint"];
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _templates.Any(t => t.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedEditor is { IsNew: false } editor && editor.EntityId == item.Id;
        }
    }

    private void RebuildItems()
    {
        var names = _properties.ToDictionary(p => p.Id, p => p.Name);
        var colors = _categories.ToDictionary(c => c.Id, c => c.Color);
        Items =
        [
            .. _templates
                .OrderByDescending(t => t.IsActive)
                .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new BillTemplateItemViewModel(
                    t,
                    names.GetValueOrDefault(t.PropertyId, string.Empty),
                    colors.GetValueOrDefault(t.CategoryId, "#808080"),
                    _localizer,
                    id => SelectCommand.Execute(id))),
        ];
        MarkSelectedItem();
    }
}
