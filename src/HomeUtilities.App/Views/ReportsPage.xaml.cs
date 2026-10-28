using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class ReportsPage : PageBase
{
    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
