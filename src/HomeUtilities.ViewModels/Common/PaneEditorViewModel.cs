using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// An editor that is shown either on its own page (compact layout) or in the right-hand pane of a list screen.
/// Adds the hosting state and the "navigate back, then tell the list" ending to <see cref="EditableViewModel"/>.
/// </summary>
public abstract partial class PaneEditorViewModel : EditableViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _saveToast;
    private readonly ILocalizer _paneLocalizer;

    /// <summary>True when the editor is shown on its own page (compact layout) instead of the right-hand pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFooter), nameof(ShowFormDelete))]
    private bool _isHostedInPage;

    [ObservableProperty]
    private string _deleteWholeText = string.Empty;

    protected PaneEditorViewModel(ILocalizer localizer, IDialogService dialogs, INavigationService navigation, ISnackbarService toast)
        : base(localizer, dialogs)
    {
        _navigation = navigation;
        _saveToast = toast;
        _paneLocalizer = localizer;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsNew))
            {
                NotifyDeleteVisibilityChanged();
            }
        };
    }

    /// <summary>The user-guide section the "?" button of this editor opens; null = the editor has no "?".</summary>
    protected virtual HelpTopic? GuideTopic => null;

    [RelayCommand]
    private Task OpenHelpAsync() => GuideTopic is { } topic
        ? RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, topic))
        : Task.CompletedTask;

    /// <summary>Called when the session ended, after the editor navigated back if it is hosted in a page.</summary>
    public Func<EditOutcome, Task>? Finished { get; set; }

    /// <summary>Id of the edited item (a fresh id while the item is new).</summary>
    public abstract Guid EntityId { get; }

    /// <summary>Save / Cancel / Delete footer of the side pane (medium and expanded layouts).</summary>
    public bool ShowFooter => !IsHostedInPage;

    /// <summary>Whether the item may be deleted at all; the default is every saved item.</summary>
    public virtual bool CanShowDelete => !IsNew;

    /// <summary>"Delete …" at the end of the form on its own page (compact layout).</summary>
    public bool ShowFormDelete => IsHostedInPage && CanShowDelete;

    /// <summary>Loads an existing item, or prepares a new one when <paramref name="id"/> is null.</summary>
    public abstract Task LoadAsync(Guid? id);

    protected void NotifyDeleteVisibilityChanged()
    {
        OnPropertyChanged(nameof(CanShowDelete));
        OnPropertyChanged(nameof(ShowFormDelete));
    }

    protected override async Task OnFinishedAsync(EditOutcome outcome)
    {
        // Save keeps the editor open (page or pane); only Cancel / Delete leave it.
        if (outcome == EditOutcome.Saved)
        {
            _saveToast.Notify(_paneLocalizer["Saved"]);
        }
        else if (IsHostedInPage)
        {
            await _navigation.GoBackAsync();
        }

        if (Finished is { } finished)
        {
            await finished(outcome);
        }
    }
}
