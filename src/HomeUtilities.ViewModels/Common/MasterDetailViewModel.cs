using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Base of the list screens with an editor. One content pane on every device (T-028f): the list fills the content
/// area and an editor always opens as a separate page. The item last opened stays highlighted when the user comes
/// back (the remembered id lives in <see cref="ISectionSelectionMemory"/>, because the page and this view model may be
/// recreated when the user leaves the section and comes back).
/// </summary>
public abstract partial class MasterDetailViewModel<TEditor> : ViewModelBase, IRecipient<LayoutModeChangedMessage>
    where TEditor : PaneEditorViewModel
{
    private readonly INavigationService _navigation;
    private readonly ILayoutModeService _layoutModeService;
    private readonly Func<TEditor> _editorFactory;
    private readonly string _editRoute;
    private readonly ISectionSelectionMemory _selectionMemory;

    /// <summary>The editor of the item opened last; its page is on the navigation stack until the user leaves it.</summary>
    [ObservableProperty]
    private TEditor? _selectedEditor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAddFab), nameof(ShowAddButton))]
    private bool _isWide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _addText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    protected MasterDetailViewModel(
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        Func<TEditor> editorFactory,
        string editRoute,
        ISectionSelectionMemory selectionMemory)
    {
        _selectionMemory = selectionMemory;
        _navigation = navigation;
        _layoutModeService = layoutModeService;
        _editorFactory = editorFactory;
        _editRoute = editRoute;
        _isWide = layoutModeService.Current != LayoutMode.Compact;
    }

    /// <summary>The user-guide section the "?" button of this screen opens.</summary>
    protected abstract HelpTopic GuideTopic { get; }

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, GuideTopic));

    /// <summary>Floating "add" button of the phone layout.</summary>
    public bool ShowAddFab => !IsWide;

    /// <summary>"New …" button above the list on tablets and desktops.</summary>
    public bool ShowAddButton => IsWide;

    public bool HasItems => TotalCount > 0;

    /// <summary>True when the list was loaded and contains nothing at all (shows the empty state).</summary>
    public bool IsEmpty => IsLoaded && TotalCount == 0;

    /// <summary>
    /// The item to highlight in the list: the one opened last, while it is still in the loaded list. A new, unsaved item
    /// and a deleted one are not remembered.
    /// </summary>
    protected Guid? HighlightedId => _selectionMemory.Recall(_editRoute) is { } id && ContainsItem(id) ? id : null;

    /// <summary>Number of entries before any search filter is applied.</summary>
    protected abstract int TotalCount { get; }

    /// <summary>Loads the data from the store and rebuilds the items.</summary>
    public abstract Task ReloadAsync();

    /// <summary>Whether the loaded list contains the item (archived items hidden by a filter are not contained).</summary>
    protected abstract bool ContainsItem(Guid id);

    /// <summary>Highlights the list row of <see cref="HighlightedId"/>.</summary>
    protected abstract void MarkSelectedItem();

    public override async Task OnAppearingAsync()
    {
        // Back on the list: the editor page is gone (closed, or left with the system back gesture).
        if (SelectedEditor is { } editor)
        {
            Detach(editor, forget: false);
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private Task AddAsync() => RunSafeAsync(() => OpenEditorAsync(null));

    [RelayCommand]
    private Task SelectAsync(Guid id) => RunSafeAsync(() => OpenEditorAsync(id));

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        IsWide = _layoutModeService.Current != LayoutMode.Compact;

    /// <summary>Call after the items changed.</summary>
    protected void RaiseItemsChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task OpenEditorAsync(Guid? id)
    {
        if (SelectedEditor is { } current)
        {
            Detach(current, forget: false);
        }

        var editor = _editorFactory();
        editor.Finished = outcome => EditorFinishedAsync(editor, outcome);
        await editor.LoadAsync(id);
        SelectedEditor = editor;
        RememberSelection(editor);
        MarkSelectedItem();
        await _navigation.GoToAsync(_editRoute, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = editor });
    }

    /// <summary>Only a saved item is remembered; a new, unsaved one is not.</summary>
    private void RememberSelection(TEditor editor) =>
        _selectionMemory.Remember(_editRoute, editor.IsNew ? null : editor.EntityId);

    private async Task EditorFinishedAsync(TEditor editor, EditOutcome outcome)
    {
        // After a successful save the editor page stays open (a new item becomes the highlighted one); cancelling keeps
        // the highlight of the opened item; a deleted item has nothing to highlight.
        if (outcome == EditOutcome.Saved)
        {
            if (ReferenceEquals(SelectedEditor, editor))
            {
                RememberSelection(editor);
            }
        }
        else
        {
            Detach(editor, forget: outcome == EditOutcome.Deleted);
        }

        await ReloadAsync();
    }

    private void Detach(TEditor editor, bool forget)
    {
        editor.Finished = null;
        if (ReferenceEquals(SelectedEditor, editor))
        {
            SelectedEditor = null;
        }

        if (forget)
        {
            _selectionMemory.Remember(_editRoute, null);
        }
    }
}
