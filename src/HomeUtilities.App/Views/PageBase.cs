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

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ViewModelBase vm)
        {
            await vm.OnDisappearingAsync();
        }
    }
}
