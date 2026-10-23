using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Compact-layout editor page. Query parameter <c>editor</c> hands over a running editor (so entered data
/// survives a layout switch); otherwise <c>id</c> (absent = new item) is loaded into a fresh editor.
/// </summary>
public abstract class EditorPageBase : PageBase, IQueryAttributable
{
    protected EditorPageBase()
    {
        // Set in code (not bound in XAML) so the toolbar Back always has its command; the default arrow is kept.
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(LeaveAsync) });
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(EditorNavigation.EditorParameter, out var editor) && editor is PaneEditorViewModel running)
        {
            BindingContext = running;
        }
        else if (BindingContext is PaneEditorViewModel fresh)
        {
            fresh.IsHostedInPage = true;
            Guid? id = query.TryGetValue(EditorNavigation.IdParameter, out var value)
                ? value switch
                {
                    Guid guid => guid,
                    string text when Guid.TryParse(text, out var parsed) => parsed,
                    _ => null,
                }
                : null;
            _ = fresh.LoadAsync(id);
        }
    }

    // Hardware / gesture Back and the toolbar Back arrow take the same path: unsaved changes dialog, then close.
    protected override bool OnBackButtonPressed()
    {
        LeaveAsync();
        return true;
    }

    private async void LeaveAsync()
    {
        if (BindingContext is PaneEditorViewModel viewModel)
        {
            await viewModel.CancelCommand.ExecuteAsync(null);
        }
    }
}
