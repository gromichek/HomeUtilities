namespace HomeUtilities.ViewModels;

/// <summary>A view model that can hold unsaved input and asks the user before it is left.</summary>
public interface IUnsavedChangesGuarded
{
    /// <summary>True while there is input that would be lost by leaving.</summary>
    bool HasUnsavedChanges { get; }

    /// <summary>
    /// True if the screen may be left: nothing is unsaved, or the user chose "Save" and it worked, or "Don't save".
    /// False if the user cancelled the dialog or the save failed.
    /// </summary>
    Task<bool> CanLeaveAsync();
}
