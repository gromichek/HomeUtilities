using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The list of properties. In compact layout an editor opens as a separate page; in medium/expanded layout it is
/// shown next to the list. The editor instance is the same in both cases, so switching the layout keeps the input.
/// </summary>
public sealed partial class PropertiesViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    /// <summary>Name of the navigation parameter that carries the editor instance to <see cref="Routes.PropertyEdit"/>.</summary>
    public const string EditorParameter = "editor";

    private readonly IPropertyStore _store;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly ILayoutModeService _layoutModeService;
    private readonly Func<PropertyEditViewModel> _editorFactory;

    private IReadOnlyList<Property> _properties = [];

    [ObservableProperty]
    private IReadOnlyList<PropertyItemViewModel> _items = [];

    [ObservableProperty]
    private bool _showArchived;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(ShowSelectHint))]
    private PropertyEditViewModel? _selectedProperty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSelectHint))]
    private bool _isTwoPane;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _addText = string.Empty;

    [ObservableProperty]
    private string _showArchivedText = string.Empty;

    [ObservableProperty]
    private string _selectHintText = string.Empty;

    public PropertiesViewModel(
        IPropertyStore store,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        Func<PropertyEditViewModel> editorFactory)
    {
        _store = store;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _layoutModeService = layoutModeService;
        _editorFactory = editorFactory;
        _isTwoPane = layoutModeService.Current != LayoutMode.Compact;
        RefreshTexts();
    }

    public bool HasSelection => SelectedProperty is not null;

    public bool ShowSelectHint => IsTwoPane && !HasSelection;

    public bool HasItems => Items.Count > 0;

    /// <summary>True when the list was loaded and contains nothing (shows the empty state).</summary>
    public bool IsEmpty => IsLoaded && Items.Count == 0;

    /// <summary>The last reload started by toggling <see cref="ShowArchived"/>.</summary>
    public Task PendingReload { get; private set; } = Task.CompletedTask;

    /// <summary>The last layout switch; completes when the editor has been moved between page and pane.</summary>
    public Task PendingLayoutChange { get; private set; } = Task.CompletedTask;

    public override async Task OnAppearingAsync()
    {
        // Back on the list in compact layout: the editor page is gone (closed, or left with the system back gesture).
        if (!IsTwoPane && SelectedProperty is { IsHostedInPage: true } editor)
        {
            Detach(editor);
        }

        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        _properties = await _store.ListAsync(ShowArchived);
        RebuildItems();
        IsLoaded = true;
    }

    [RelayCommand]
    private Task AddAsync() => OpenEditorAsync(null);

    [RelayCommand]
    private Task SelectAsync(PropertyItemViewModel item) => OpenEditorAsync(item.Id);

    partial void OnShowArchivedChanged(bool value) => PendingReload = ReloadAsync();

    partial void OnItemsChanged(IReadOnlyList<PropertyItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSelectedPropertyChanged(PropertyEditViewModel? value) => MarkSelectedItem();

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        PendingLayoutChange = LayoutChangedAsync();

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionProperties"];
        EmptyText = _localizer["PropertiesEmpty"];
        AddText = _localizer["PropertyAdd"];
        ShowArchivedText = _localizer["PropertiesShowArchived"];
        SelectHintText = _localizer["PropertySelectHint"];
        RebuildItems();
    }

    private async Task OpenEditorAsync(Guid? id)
    {
        if (SelectedProperty is { } current)
        {
            if (!await current.ConfirmDiscardAsync())
            {
                return;
            }

            Detach(current);
        }

        var compact = _layoutModeService.Current == LayoutMode.Compact;
        var editor = _editorFactory();
        editor.IsHostedInPage = compact;
        editor.Finished = outcome => EditorFinishedAsync(editor, outcome);
        await editor.LoadAsync(id);
        SelectedProperty = editor;

        if (compact)
        {
            await _navigation.GoToAsync(Routes.PropertyEdit, new Dictionary<string, object> { [EditorParameter] = editor });
        }
    }

    private async Task EditorFinishedAsync(PropertyEditViewModel editor, EditOutcome outcome)
    {
        // After a successful save in the side pane the editor stays; every other way out closes it.
        if (outcome != EditOutcome.Saved || editor.IsHostedInPage)
        {
            Detach(editor);
        }

        await ReloadAsync();
    }

    private void Detach(PropertyEditViewModel editor)
    {
        editor.Finished = null;
        if (ReferenceEquals(SelectedProperty, editor))
        {
            SelectedProperty = null;
        }
    }

    private async Task LayoutChangedAsync()
    {
        IsTwoPane = _layoutModeService.Current != LayoutMode.Compact;
        if (SelectedProperty is not { } editor)
        {
            return;
        }

        if (!IsTwoPane && !editor.IsHostedInPage)
        {
            editor.IsHostedInPage = true;
            await _navigation.GoToAsync(Routes.PropertyEdit, new Dictionary<string, object> { [EditorParameter] = editor });
        }
        else if (IsTwoPane && editor.IsHostedInPage)
        {
            editor.IsHostedInPage = false;
            await _navigation.GoBackAsync();
        }
    }

    private void RebuildItems()
    {
        var today = _clock.Today;
        Items = [.. _properties.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => new PropertyItemViewModel(p, today, _localizer, item => SelectCommand.Execute(item)))];
        MarkSelectedItem();
    }

    private void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedProperty is { IsNew: false } editor && editor.PropertyId == item.Id;
        }
    }
}
