using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class PropertiesPage : PageBase
{
    public PropertiesPage(PropertiesViewModel viewModel)
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
