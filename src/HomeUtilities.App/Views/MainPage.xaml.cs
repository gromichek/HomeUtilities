using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((MainViewModel)BindingContext).InitializeCommand.Execute(null);
    }
}
