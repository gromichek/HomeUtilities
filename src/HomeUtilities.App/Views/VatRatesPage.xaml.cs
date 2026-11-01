using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class VatRatesPage : PageBase
{
    public VatRatesPage(VatRatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ListScroll.RevealSelected(ItemList);
    }
}
