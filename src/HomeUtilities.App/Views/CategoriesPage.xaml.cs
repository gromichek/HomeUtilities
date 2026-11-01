using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class CategoriesPage : PageBase
{
    public CategoriesPage(CategoriesViewModel viewModel)
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
