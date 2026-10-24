using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Forwards the page lifecycle to the view model.</summary>
public abstract class PageBase : ContentPage
{
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
        if (BindingContext is IUnsavedChangesGuarded { HasUnsavedChanges: true } guarded
            && Shell.Current is { } shell
            && shell.CurrentItem?.CurrentItem?.Stack.Count is null or <= 1)
        {
            LeaveSectionAsync(guarded, shell);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private static async void LeaveSectionAsync(IUnsavedChangesGuarded guarded, Shell shell)
    {
        if (await guarded.CanLeaveAsync())
        {
            await shell.GoToAsync($"//{Routes.Overview}");
        }
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
