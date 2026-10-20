using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class AppShell : Shell
{
    public AppShell(ShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
