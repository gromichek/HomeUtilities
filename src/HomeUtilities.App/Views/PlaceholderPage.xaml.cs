using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class PlaceholderPage : PageBase
{
    public PlaceholderPage(PlaceholderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
