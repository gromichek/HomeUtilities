using HomeUtilities.App.Views;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class AppShell : Shell
{
    public AppShell(ShellViewModel viewModel)
    {
        InitializeComponent();
        Routing.RegisterRoute(Routes.PropertyEdit, typeof(PropertyEditPage));
        Routing.RegisterRoute(Routes.SupplierEdit, typeof(SupplierEditPage));
        Routing.RegisterRoute(Routes.ContractEdit, typeof(ContractEditPage));
        Routing.RegisterRoute(Routes.MeterEdit, typeof(MeterEditPage));
        Routing.RegisterRoute(Routes.Categories, typeof(CategoriesPage));
        Routing.RegisterRoute(Routes.CategoryEdit, typeof(CategoryEditPage));
        BindingContext = viewModel;
    }
}
