using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>VAT rate editor page, see <see cref="EditorPageBase"/>.</summary>
public partial class VatRateEditPage : EditorPageBase
{
    public VatRateEditPage(VatRateEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
