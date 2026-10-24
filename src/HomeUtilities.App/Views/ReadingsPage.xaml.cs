using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// The readings screen. Leaving it with typed but unsaved values (another menu section, Back) asks the standard
/// "Save changes?" question through the central navigation guard (see <c>AppShell</c> and <c>PageBase</c>); pushing
/// the history page does not, the values stay on this page.
/// </summary>
public partial class ReadingsPage : PageBase
{
    public ReadingsPage(ReadingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Coming back from the browser does not make the page appear again; the window is activated instead.
        if (Window is { } window)
        {
            window.Activated += OnWindowActivated;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Window is { } window)
        {
            window.Activated -= OnWindowActivated;
        }
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (BindingContext is ReadingsViewModel viewModel)
        {
            await viewModel.CheckReturnFromSubmitPageAsync();
        }
    }
}
