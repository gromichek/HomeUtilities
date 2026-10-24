using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Common;

/// <summary>How an editing session ended.</summary>
public enum EditOutcome
{
    Saved,
    Cancelled,
    Deleted,
}

/// <summary>
/// Base of all edit screens: dirty tracking, validation errors for binding, Save / Cancel / Delete with the
/// "Save changes?" and "Delete «name»?" dialogs of <c>docs/UI_GUIDELINES.md</c>. Subclasses implement validation, saving and deleting.
/// </summary>
public abstract partial class EditableViewModel : ViewModelBase, IUnsavedChangesGuarded
{
    private static readonly IReadOnlyDictionary<string, string> NoErrors = new Dictionary<string, string>();

    private readonly ILocalizer _editableLocalizer;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    private IReadOnlyDictionary<string, string> _errors = NoErrors;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isNew = true;

    [ObservableProperty]
    private string _saveText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    private string _cancelText = string.Empty;

    [ObservableProperty]
    private string _deleteText = string.Empty;

    protected EditableViewModel(ILocalizer localizer, IDialogService dialogs)
    {
        _editableLocalizer = localizer;
        _dialogs = dialogs;
    }

    public bool HasErrors => Errors.Count > 0;

    bool IUnsavedChangesGuarded.HasUnsavedChanges => IsDirty;

    protected IDialogService Dialogs => _dialogs;

    /// <summary>While true, property changes do not mark the editor dirty (loading, text refresh).</summary>
    protected bool SuppressDirty { get; set; }

    /// <summary>Called by the subclass for every user edit.</summary>
    protected void MarkDirty()
    {
        if (!SuppressDirty)
        {
            IsDirty = true;
        }
    }

    /// <summary>Validates the entered data; the field of each error is the key in <see cref="Errors"/>.</summary>
    protected abstract IReadOnlyList<ValidationError> Validate();

    protected abstract Task SaveCoreAsync();

    protected abstract Task DeleteCoreAsync();

    /// <summary>Name of the item shown in the "Delete «name»?" confirmation.</summary>
    protected abstract string DeleteName();

    /// <summary>
    /// Called after the session ended: Cancelled / Deleted leave the editor; Saved does not (the editor stays open,
    /// owner rule 2026-10-23) — the subclass only refreshes the list and confirms.
    /// </summary>
    protected abstract Task OnFinishedAsync(EditOutcome outcome);

    /// <summary>Called whenever <see cref="Errors"/> changed, to refresh the per-field error properties.</summary>
    protected virtual void OnErrorsChanged()
    {
    }

    protected void SetErrors(IEnumerable<ValidationError> errors)
    {
        Errors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_editableLocalizer, e))));
        OnErrorsChanged();
    }

    protected void ClearErrors() => SetErrors([]);

    /// <summary>
    /// True if the editor may be left: nothing is unsaved, or the user chose "Save" and it succeeded, or "Don't save".
    /// False if the user cancelled the dialog or the save failed (errors are shown). Does not close the editor.
    /// </summary>
    public async Task<bool> CanLeaveAsync()
    {
        var canLeave = false;
        await RunSafeAsync(async () => canLeave = await CanLeaveCoreAsync());
        return canLeave;
    }

    private async Task<bool> CanLeaveCoreAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        switch (await _dialogs.AskUnsavedChangesAsync())
        {
            case UnsavedChangesChoice.Save:
                return await TrySaveAsync();
            case UnsavedChangesChoice.DontSave:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Validates and saves; false when validation failed or another save is running.</summary>
    private async Task<bool> TrySaveAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        var errors = Validate();
        SetErrors(errors);
        if (errors.Count > 0)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await SaveCoreAsync();
            IsDirty = false;
        }
        finally
        {
            IsBusy = false;
        }

        return true;
    }

    protected override void RefreshTexts()
    {
        SaveText = _editableLocalizer["Save"];
        HelpText = _editableLocalizer["SectionHelp"];
        CancelText = _editableLocalizer["Cancel"];
        DeleteText = _editableLocalizer["Delete"];
    }

    [RelayCommand]
    private Task SaveAsync() => RunSafeAsync(async () =>
    {
        if (await TrySaveAsync())
        {
            await OnFinishedAsync(EditOutcome.Saved);
        }
    });

    /// <summary>Cancel button, toolbar Back and hardware Back: leave the editor, asking about unsaved changes first.</summary>
    [RelayCommand]
    private Task CancelAsync() => RunSafeAsync(async () =>
    {
        if (!await CanLeaveAsync())
        {
            return;
        }

        IsDirty = false;
        await OnFinishedAsync(EditOutcome.Cancelled);
    });

    private bool CanDelete() => !IsNew;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync() => RunSafeAsync(async () =>
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _editableLocalizer.Get("Dialog_Delete_Title", DeleteName()),
            _editableLocalizer["Dialog_Delete_Message"],
            _editableLocalizer["Delete"],
            _editableLocalizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await DeleteCoreAsync();
            IsDirty = false;
        }
        finally
        {
            IsBusy = false;
        }

        await OnFinishedAsync(EditOutcome.Deleted);
    });
}
