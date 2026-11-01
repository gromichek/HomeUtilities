using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Forwards the page lifecycle to the view model.</summary>
public abstract class PageBase : ContentPage
{
    // Every page gets the in-app snackbar: a template with the page content and the snackbar host on top of it.
    // (Not a system notification — those crash the unpackaged Windows app, see docs/UI_GUIDELINES.md §6a.)
    protected PageBase()
    {
        ControlTemplate = new ControlTemplate(() =>
        {
            var host = new SnackbarHost();
            var root = new Grid();
            root.Children.Add(new ContentPresenter());
            root.Children.Add(host);
            root.SizeChanged += (_, _) => host.FitTo(root.Width);
            return root;
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModelBase vm)
        {
            await vm.OnAppearingAsync();
        }
    }

    // Back on a section root with unsaved input: ask first, then go to the overview instead of closing the app
    // silently. On a pushed page Back pops it and the central guard in AppShell asks.
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is IUnsavedChangesGuarded { HasUnsavedChanges: true }
            && Shell.Current is { } shell
            && shell.CurrentItem?.CurrentItem?.Stack.Count is null or <= 1)
        {
            // The central guard in AppShell asks the question for this navigation (asking here as well would show
            // the dialog twice after "Don't save").
            _ = shell.GoToAsync($"//{Routes.Overview}");
            return true;
        }

        // A section root other than the Overview: Back goes to the Overview; on the Overview it leaves the app (default).
        // The same path for the toolbar arrow, the system Back button and the gesture (T-028a).
        if (this is not OverviewPage
            && Shell.Current is { } current
            && current.CurrentItem?.CurrentItem?.Stack.Count is null or <= 1
            && Navigation.ModalStack.Count == 0)
        {
            _ = current.GoToAsync($"//{Routes.Overview}");
            return true;
        }

        return base.OnBackButtonPressed();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ViewModelBase vm)
        {
            await vm.OnDisappearingAsync();
        }
    }
}
