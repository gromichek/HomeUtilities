using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class BillTemplatesPage : PageBase
{
    public BillTemplatesPage(BillTemplatesViewModel viewModel)
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
