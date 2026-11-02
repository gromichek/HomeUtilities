using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The list of properties; with a switch that also shows archived properties.</summary>
public sealed partial class PropertiesViewModel : MasterDetailViewModel<PropertyEditViewModel>
{
    protected override HelpTopic GuideTopic => HelpTopic.Properties;

    private readonly IPropertyStore _store;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;

    private IReadOnlyList<Property> _properties = [];

    /// <summary>The cards; updated in place so the list keeps its scroll position.</summary>
    public ObservableCollection<PropertyItemViewModel> Items { get; } = [];

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
        ISectionSelectionMemory selectionMemory,
        Func<PropertyEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.PropertyEdit, selectionMemory)
    {
        _store = store;
        _clock = clock;
        _localizer = localizer;
        RefreshTexts();
    }

    /// <summary>The last reload started by toggling <see cref="ShowArchived"/>.</summary>
    public Task PendingReload { get; private set; } = Task.CompletedTask;

    protected override int TotalCount => _properties.Count;

    public override async Task ReloadAsync(CancellationToken ct = default)
    {
        var showArchived = ShowArchived;
        var loaded = await OffUiAsync(() => _store.ListAsync(showArchived, ct), ct);
        ct.ThrowIfCancellationRequested();
        _properties = loaded;
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
        HelpText = _localizer["SectionHelp"];
        ShowArchivedText = _localizer["PropertiesShowArchived"];
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _properties.Any(x => x.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = HighlightedId == item.Id;
        }
    }

    private void SetItems(IReadOnlyList<PropertyItemViewModel> items)
    {
        ItemsSync.Update(Items, items, item => item.Id);
    }

    private void RebuildItems()
    {
        var today = _clock.Today;
        SetItems([.. _properties.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => new PropertyItemViewModel(p, today, _localizer, item => SelectCommand.Execute(item.Id)))]);
        MarkSelectedItem();
    }
}
