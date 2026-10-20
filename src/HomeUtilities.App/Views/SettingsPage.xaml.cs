using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class SettingsPage : PageBase
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
