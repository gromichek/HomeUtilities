using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>
    /// Shows the app's own choice dialog (radio rows, the current value marked; UI_GUIDELINES §3a) and returns the
    /// **index** of the chosen option, or null if the user cancelled. Callers map the index back to their record, never
    /// the display text (two records may have the same name).
    /// </summary>
    /// <remarks>
    /// A row without extra inputs answers at once (UI_GUIDELINES §3a). With <paramref name="actionText"/> an extra row that
    /// creates something new ("＋ New supplier…") is shown last, set apart, and answers with the index
    /// <c>options.Count</c>.
    /// </remarks>
    /// <param name="selectedIndex">The option marked when the dialog opens (null = none).</param>
    /// <param name="actionText">Text of the "create new" action row; null = none.</param>
    Task<int?> ChooseAsync(string title, IReadOnlyList<string> options, int? selectedIndex = null, string? actionText = null);

    /// <summary>
    /// Picks a **command** (e.g. Camera / Gallery) with the system action sheet. Returns the chosen text or null if the
    /// user cancelled. Never use it to pick a record or an option (use <see cref="ChooseAsync"/>).
    /// </summary>
    Task<string?> ChooseCommandAsync(string title, IReadOnlyList<string> commands);

    /// <summary>The standard "Save changes?" dialog with Save / Don't save / Cancel.</summary>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync();

    /// <summary>
    /// Shows a child-item editor (popup on medium/expanded layouts, full-screen page on compact) and completes when
    /// it is closed. Returns true when the user saved.
    /// </summary>
    Task<bool> ShowEditorAsync(EditorDialogViewModel editor);
}
