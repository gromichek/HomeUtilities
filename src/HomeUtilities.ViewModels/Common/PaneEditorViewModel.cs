using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// An editor that is shown on its own page on every device (one content pane, T-028f).
/// Adds the "navigate back, then tell the list" ending to <see cref="EditableViewModel"/>.
/// </summary>
public abstract partial class PaneEditorViewModel : EditableViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _saveSnackbar;
    private readonly ILocalizer _paneLocalizer;

    [ObservableProperty]
    private string _deleteWholeText = string.Empty;

    protected PaneEditorViewModel(ILocalizer localizer, IDialogService dialogs, INavigationService navigation, ISnackbarService snackbar)
        : base(localizer, dialogs)
    {
        _navigation = navigation;
        _saveSnackbar = snackbar;
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

    /// <summary>Called when the session ended, after the editor navigated back.</summary>
    public Func<EditOutcome, Task>? Finished { get; set; }

    /// <summary>Id of the edited item (a fresh id while the item is new).</summary>
    public abstract Guid EntityId { get; }

    /// <summary>Whether the item may be deleted at all; the default is every saved item.</summary>
    public virtual bool CanShowDelete => !IsNew;

    /// <summary>"Delete …" at the end of the form.</summary>
    public bool ShowFormDelete => CanShowDelete;

    /// <summary>Loads an existing item, or prepares a new one when <paramref name="id"/> is null.</summary>
    public abstract Task LoadAsync(Guid? id);

    /// <summary>
    /// Starts <see cref="LoadAsync"/> at once (same moment as a direct call) and observes its failure: the standard error dialog
    /// and an error-log entry instead of a lost exception; the page stays as it is (R-5).
    /// </summary>
    public Task LoadSafeAsync(Guid? id) => RunSafeAsync(() => LoadAsync(id), nameof(LoadAsync));

    protected void NotifyDeleteVisibilityChanged()
    {
        OnPropertyChanged(nameof(CanShowDelete));
        OnPropertyChanged(nameof(ShowFormDelete));
    }

    /// <summary>Text of the "saved" message when the save did more than the usual (returns null for the standard "Saved").</summary>
    protected virtual string? TakeSavedMessage() => null;

    protected override async Task OnFinishedAsync(EditOutcome outcome)
    {
        // Save keeps the editor page open; only Cancel / Delete leave it.
        if (outcome == EditOutcome.Saved)
        {
            _saveSnackbar.Notify(TakeSavedMessage() ?? _paneLocalizer["Saved"]);
        }
        else
        {
            await _navigation.GoBackAsync();
        }

        if (Finished is { } finished)
        {
            await finished(outcome);
        }
    }
}
