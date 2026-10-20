using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class OverviewPage : PageBase
{
    public OverviewPage(OverviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
