using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class SuppliersPage : PageBase
{
    public SuppliersPage(SuppliersViewModel viewModel)
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
