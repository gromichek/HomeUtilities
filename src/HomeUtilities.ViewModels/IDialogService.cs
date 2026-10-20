using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Returns the chosen option or null if the user cancelled.</summary>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options);

    /// <summary>The standard "Save changes?" dialog with Save / Don't save / Cancel.</summary>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync();

    /// <summary>
    /// Shows a child-item editor (popup on medium/expanded layouts, full-screen page on compact) and completes when
    /// it is closed. Returns true when the user saved.
    /// </summary>
    Task<bool> ShowEditorAsync(EditorDialogViewModel editor);
}
