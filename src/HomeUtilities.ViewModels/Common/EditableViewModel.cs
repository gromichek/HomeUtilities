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
/// "discard changes?" and "delete?" confirmations. Subclasses implement validation, saving and deleting.
/// </summary>
public abstract partial class EditableViewModel : ViewModelBase
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
    private string _cancelText = string.Empty;

    [ObservableProperty]
    private string _deleteText = string.Empty;

    protected EditableViewModel(ILocalizer localizer, IDialogService dialogs)
    {
        _editableLocalizer = localizer;
        _dialogs = dialogs;
    }

    public bool HasErrors => Errors.Count > 0;

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

    /// <summary>Text of the "Delete «name»?" confirmation.</summary>
    protected abstract string DeleteMessage();

    /// <summary>Called after the session ended (navigate back, refresh the list).</summary>
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

    /// <summary>True if there is nothing to lose or the user agreed to discard the changes.</summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        return await _dialogs.ConfirmAsync(
            _editableLocalizer["DiscardChangesTitle"],
            _editableLocalizer["DiscardChangesMessage"],
            _editableLocalizer["Discard"],
            _editableLocalizer["KeepEditing"]);
    }

    protected override void RefreshTexts()
    {
        SaveText = _editableLocalizer["Save"];
        CancelText = _editableLocalizer["Cancel"];
        DeleteText = _editableLocalizer["Delete"];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var errors = Validate();
        SetErrors(errors);
        if (errors.Count > 0)
        {
            return;
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

        await OnFinishedAsync(EditOutcome.Saved);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        IsDirty = false;
        await OnFinishedAsync(EditOutcome.Cancelled);
    }

    private bool CanDelete() => !IsNew;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _editableLocalizer["DeleteConfirmTitle"],
            DeleteMessage(),
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
    }
}
