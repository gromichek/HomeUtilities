using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class CategoriesPage : PageBase
{
    public CategoriesPage(CategoriesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }
}
