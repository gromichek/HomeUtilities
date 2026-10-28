using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class BillTemplatesPage : PageBase
{
    public BillTemplatesPage(BillTemplatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }
}
