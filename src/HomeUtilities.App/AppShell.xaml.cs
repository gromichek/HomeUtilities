using HomeUtilities.App.Views;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class AppShell : Shell
{
    public AppShell(ShellViewModel viewModel)
    {
        InitializeComponent();
        Routing.RegisterRoute(Routes.PropertyEdit, typeof(PropertyEditPage));
        BindingContext = viewModel;
    }
}
