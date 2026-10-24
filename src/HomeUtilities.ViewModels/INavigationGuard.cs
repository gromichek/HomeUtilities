namespace HomeUtilities.ViewModels;

/// <summary>
/// The one place that decides whether the user may navigate away (menu, Back, window close). Asks the currently
/// active editor, which shows the standard "Save changes?" dialog when it has unsaved input.
/// </summary>
public interface INavigationGuard
{
    /// <summary>
    /// True if navigation may go on. False if the user cancelled, the save failed, or another request is already
    /// waiting for the dialog.
    /// </summary>
    Task<bool> CanLeaveAsync();
}
