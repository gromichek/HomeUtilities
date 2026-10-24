using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// The readings screen. Leaving it with typed but unsaved values (another menu section, Back) asks the standard
/// "Save changes?" question; pushing the history page does not, the values stay on this page.
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
        if (Shell.Current is { } shell)
        {
            shell.Navigating += OnShellNavigating;
        }

        // Coming back from the browser does not make the page appear again; the window is activated instead.
        if (Window is { } window)
        {
            window.Activated += OnWindowActivated;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Shell.Current is { } shell)
        {
            shell.Navigating -= OnShellNavigating;
        }

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

    // Only a switch to another menu section leaves the screen for good; a pushed page ("..") keeps it alive.
    private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        if (BindingContext is not ReadingsViewModel viewModel
            || !viewModel.HasUnsavedValues
            || e.Source is not (ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellSectionChanged or ShellNavigationSource.ShellContentChanged))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            if (!await viewModel.CanLeaveAsync())
            {
                e.Cancel();
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    // Back on the section root: ask first, then go to the overview instead of closing the app silently.
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ReadingsViewModel { HasUnsavedValues: true } viewModel)
        {
            LeaveAsync(viewModel);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private static async void LeaveAsync(ReadingsViewModel viewModel)
    {
        if (await viewModel.CanLeaveAsync() && Shell.Current is { } shell)
        {
            await shell.GoToAsync($"//{Routes.Overview}");
        }
    }
}
