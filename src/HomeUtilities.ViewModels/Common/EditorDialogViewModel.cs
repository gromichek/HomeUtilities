using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Base of the dialogs that add or edit a child item of an editor (a parameter, a link, a line...). The dialog has
/// its own title, Save and Cancel; it never edits the parent directly — the parent reads the result after Save.
/// </summary>
public abstract partial class EditorDialogViewModel : ViewModelBase
{
    private readonly ILocalizer _dialogLocalizer;

    protected EditorDialogViewModel(ILocalizer localizer)
    {
        _dialogLocalizer = localizer;
        RefreshDialogTexts();
    }

    /// <summary>True once the user saved successfully.</summary>
    public bool Saved { get; private set; }

    public string SaveText { get; private set; } = string.Empty;

    public string CancelText { get; private set; } = string.Empty;

    /// <summary>
    /// The dialog's primary action, shown as ✓ in the top bar of the phone dialog frame (UI_GUIDELINES §3.4): Save by default,
    /// OK for the choice dialogs.
    /// </summary>
    public virtual ICommand PrimaryCommand => SaveCommand;

    /// <summary>Whether the top bar shows the ✓ at all (a choice dialog whose rows answer at once has none).</summary>
    public virtual bool ShowPrimary => true;

    /// <summary>Raised when the dialog wants to be closed (after Save or Cancel).</summary>
    public event Action? CloseRequested;

    /// <summary>Validates and stores the entered data; returns false (with errors shown) when it is invalid.</summary>
    protected abstract bool TryApply();

    [RelayCommand]
    private void Save()
    {
        if (!TryApply())
        {
            return;
        }

        Saved = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    /// <summary>For dialogs whose confirm button does asynchronous work itself: marks the dialog saved and closes it.</summary>
    protected void CloseSaved()
    {
        Saved = true;
        CloseRequested?.Invoke();
    }

    protected override void RefreshTexts()
    {
        RefreshDialogTexts();
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(CancelText));
    }

    private void RefreshDialogTexts()
    {
        SaveText = _dialogLocalizer["Save"];
        CancelText = _dialogLocalizer["Cancel"];
    }
}
