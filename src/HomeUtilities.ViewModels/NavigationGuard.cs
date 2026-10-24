namespace HomeUtilities.ViewModels;

public sealed class NavigationGuard : INavigationGuard
{
    private readonly IActiveEditorProvider _active;
    private bool _asking;

    public NavigationGuard(IActiveEditorProvider active) => _active = active;

    public async Task<bool> CanLeaveAsync()
    {
        // A second navigation while the dialog is open must not stack another dialog.
        if (_asking)
        {
            return false;
        }

        if (_active.Current is not { HasUnsavedChanges: true } editor)
        {
            return true;
        }

        _asking = true;
        try
        {
            return await editor.CanLeaveAsync();
        }
        finally
        {
            _asking = false;
        }
    }
}
