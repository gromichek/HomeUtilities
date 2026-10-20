using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// Compact-layout editor page. Query parameter <c>editor</c> hands over a running editor (so entered data
/// survives a layout switch); otherwise <c>id</c> (absent = new property) is loaded into a fresh editor.
/// </summary>
public partial class PropertyEditPage : PageBase, IQueryAttributable
{
    public PropertyEditPage(PropertyEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Set in code (not bound in XAML) so the toolbar Back always has its command; the default arrow is kept.
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(LeaveAsync) });
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(PropertiesViewModel.EditorParameter, out var editor) && editor is PropertyEditViewModel running)
        {
            BindingContext = running;
        }
        else if (BindingContext is PropertyEditViewModel fresh)
        {
            fresh.IsHostedInPage = true;
            Guid? id = query.TryGetValue("id", out var value)
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
        if (BindingContext is PropertyEditViewModel viewModel)
        {
            await viewModel.CancelCommand.ExecuteAsync(null);
        }
    }
}
