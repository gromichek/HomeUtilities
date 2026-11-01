using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Property editor page, see <see cref="EditorPageBase"/>.</summary>
public partial class PropertyEditPage : EditorPageBase
{
    public PropertyEditPage(PropertyEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
