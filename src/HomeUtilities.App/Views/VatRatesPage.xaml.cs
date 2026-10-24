using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class VatRatesPage : PageBase
{
    public VatRatesPage(VatRatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }
}
