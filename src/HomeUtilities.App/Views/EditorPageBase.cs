using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Editor page, used on every device and window size (one content pane, T-028f). Query parameter <c>editor</c> hands over a
/// running editor (the list screen creates and loads it); otherwise <c>id</c> (absent = new item) is loaded into a fresh editor.
/// </summary>
public abstract class EditorPageBase : PageBase, IQueryAttributable
{
    protected EditorPageBase()
    {
        // Set in code (not bound in XAML) so the toolbar Back always has its command; the default arrow is kept.
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(LeaveAsync) });
    }

    private bool _queryApplied;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell applies a plain query dictionary again whenever the user comes back to this page (e.g. after a child
        // dialog page was closed). Loading again would throw away the unsaved input (owner bug 2026-10-30: the name of a
        // new tariff component vanished after adding a price), so the query is applied only once per page.
        if (_queryApplied)
        {
            return;
        }

        _queryApplied = true;
        if (query.TryGetValue(EditorNavigation.EditorParameter, out var editor) && editor is PaneEditorViewModel running)
        {
            BindingContext = running;
        }
        else if (BindingContext is PaneEditorViewModel fresh)
        {
            PrepareFresh(fresh, query);
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

    /// <summary>Lets a page pass extra query parameters to a fresh editor before it is loaded.</summary>
    protected virtual void PrepareFresh(PaneEditorViewModel editor, IDictionary<string, object> query)
    {
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
