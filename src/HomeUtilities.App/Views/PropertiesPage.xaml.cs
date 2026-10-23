using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class PropertiesPage : PageBase
{
    public PropertiesPage(PropertiesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }
}
