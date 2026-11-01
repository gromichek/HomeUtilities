using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Supplier editor page, see <see cref="EditorPageBase"/>.</summary>
public partial class SupplierEditPage : EditorPageBase
{
    public SupplierEditPage(SupplierEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
