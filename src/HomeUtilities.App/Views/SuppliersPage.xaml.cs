using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class SuppliersPage : PageBase
{
    public SuppliersPage(SuppliersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }
}
