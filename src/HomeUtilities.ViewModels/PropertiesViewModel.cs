using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The list of properties; two-pane like Suppliers, with a switch that also shows archived properties.</summary>
public sealed partial class PropertiesViewModel : MasterDetailViewModel<PropertyEditViewModel>
{
    private readonly IPropertyStore _store;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;

    private IReadOnlyList<Property> _properties = [];

    [ObservableProperty]
    private IReadOnlyList<PropertyItemViewModel> _items = [];

    [ObservableProperty]
    private bool _showArchived;

    [ObservableProperty]
    private string _showArchivedText = string.Empty;

    public PropertiesViewModel(
        IPropertyStore store,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        Func<PropertyEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.PropertyEdit)
    {
        _store = store;
        _clock = clock;
        _localizer = localizer;
        RefreshTexts();
    }

    /// <summary>The last reload started by toggling <see cref="ShowArchived"/>.</summary>
    public Task PendingReload { get; private set; } = Task.CompletedTask;

    protected override int TotalCount => _properties.Count;

    public override async Task ReloadAsync()
    {
        _properties = await _store.ListAsync(ShowArchived);
        RebuildItems();
        IsLoaded = true;
        RaiseItemsChanged();
    }

    partial void OnShowArchivedChanged(bool value) => PendingReload = ReloadAsync();

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionProperties"];
        EmptyText = _localizer["PropertiesEmpty"];
        AddText = _localizer["PropertyAdd"];
        ShowArchivedText = _localizer["PropertiesShowArchived"];
        SelectHintText = _localizer["PropertySelectHint"];
        RebuildItems();
    }

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedEditor is { IsNew: false } editor && editor.EntityId == item.Id;
        }
    }

    private void RebuildItems()
    {
        var today = _clock.Today;
        Items = [.. _properties.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => new PropertyItemViewModel(p, today, _localizer, item => SelectCommand.Execute(item.Id)))];
        MarkSelectedItem();
    }
}
