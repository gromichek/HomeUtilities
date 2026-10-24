using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Base of the list screens with an editor. In compact layout an editor opens as a separate page; in medium/expanded
/// layout it is shown next to the list. The editor instance is the same in both cases, so switching the layout keeps
/// the input. Same behaviour as <see cref="PropertiesViewModel"/>.
/// </summary>
public abstract partial class MasterDetailViewModel<TEditor> : ViewModelBase, IRecipient<LayoutModeChangedMessage>, IUnsavedChangesGuarded
    where TEditor : PaneEditorViewModel
{
    private readonly INavigationService _navigation;
    private readonly ILayoutModeService _layoutModeService;
    private readonly Func<TEditor> _editorFactory;
    private readonly string _editRoute;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(ShowSelectHint))]
    private TEditor? _selectedEditor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSelectHint), nameof(ShowAddFab), nameof(ShowAddButton))]
    private bool _isTwoPane;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _addText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    private string _selectHintText = string.Empty;

    protected MasterDetailViewModel(
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        Func<TEditor> editorFactory,
        string editRoute)
    {
        _navigation = navigation;
        _layoutModeService = layoutModeService;
        _editorFactory = editorFactory;
        _editRoute = editRoute;
        _isTwoPane = layoutModeService.Current != LayoutMode.Compact;
    }

    /// <summary>The user-guide section the "?" button of this screen opens.</summary>
    protected abstract HelpTopic GuideTopic { get; }

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, GuideTopic));

    /// <summary>Only an editor in the side pane belongs to this screen; an editor page guards itself.</summary>
    bool IUnsavedChangesGuarded.HasUnsavedChanges => SelectedEditor is { IsHostedInPage: false, IsDirty: true };

    /// <summary>
    /// Leaving the section with the side-pane editor dirty: asks the standard question. "Don't save" discards the
    /// input, so the section reopens with the saved state (a new, unsaved item is closed).
    /// </summary>
    public async Task<bool> CanLeaveAsync()
    {
        if (SelectedEditor is not { IsHostedInPage: false, IsDirty: true } editor)
        {
            return true;
        }

        if (!await editor.CanLeaveAsync())
        {
            return false;
        }

        if (editor.IsDirty)
        {
            if (editor.IsNew)
            {
                Detach(editor);
            }
            else
            {
                await editor.LoadAsync(editor.EntityId);
            }
        }

        return true;
    }

    public bool HasSelection => SelectedEditor is not null;

    public bool ShowSelectHint => IsTwoPane && !HasSelection;

    /// <summary>Floating "add" button of the phone layout.</summary>
    public bool ShowAddFab => !IsTwoPane;

    /// <summary>"New …" button above the list on tablets and desktops.</summary>
    public bool ShowAddButton => IsTwoPane;

    public bool HasItems => TotalCount > 0;

    /// <summary>True when the list was loaded and contains nothing at all (shows the empty state).</summary>
    public bool IsEmpty => IsLoaded && TotalCount == 0;

    /// <summary>The last layout switch; completes when the editor has been moved between page and pane.</summary>
    public Task PendingLayoutChange { get; private set; } = Task.CompletedTask;

    /// <summary>Number of entries before any search filter is applied.</summary>
    protected abstract int TotalCount { get; }

    /// <summary>Loads the data from the store and rebuilds the items.</summary>
    public abstract Task ReloadAsync();

    /// <summary>Highlights the list row of <see cref="SelectedEditor"/>.</summary>
    protected abstract void MarkSelectedItem();

    public override async Task OnAppearingAsync()
    {
        // Back on the list in compact layout: the editor page is gone (closed, or left with the system back gesture).
        if (!IsTwoPane && SelectedEditor is { IsHostedInPage: true } editor)
        {
            Detach(editor);
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private Task AddAsync() => RunSafeAsync(() => OpenEditorAsync(null));

    [RelayCommand]
    private Task SelectAsync(Guid id) => RunSafeAsync(() => OpenEditorAsync(id));

    partial void OnSelectedEditorChanged(TEditor? value) => MarkSelectedItem();

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        PendingLayoutChange = LayoutChangedAsync();

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
            if (!await current.CanLeaveAsync())
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
        SelectedEditor = editor;

        if (compact)
        {
            await _navigation.GoToAsync(_editRoute, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = editor });
        }
    }

    private async Task EditorFinishedAsync(TEditor editor, EditOutcome outcome)
    {
        // After a successful save the editor stays (pane or page); every other way out closes it.
        if (outcome != EditOutcome.Saved)
        {
            Detach(editor);
        }

        await ReloadAsync();
    }

    private void Detach(TEditor editor)
    {
        editor.Finished = null;
        if (ReferenceEquals(SelectedEditor, editor))
        {
            SelectedEditor = null;
        }
    }

    private async Task LayoutChangedAsync()
    {
        IsTwoPane = _layoutModeService.Current != LayoutMode.Compact;
        if (SelectedEditor is not { } editor)
        {
            return;
        }

        if (!IsTwoPane && !editor.IsHostedInPage)
        {
            editor.IsHostedInPage = true;
            await _navigation.GoToAsync(_editRoute, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = editor });
        }
        else if (IsTwoPane && editor.IsHostedInPage)
        {
            editor.IsHostedInPage = false;

            // Only pop the editor's own page. When a child page (e.g. a contract) is on top, popping would close that
            // one instead; the editor page below pops itself when it appears again (see EditorPageBase).
            if (_navigation.IsShowing(editor))
            {
                await _navigation.GoBackAsync();
            }
        }
    }
}
