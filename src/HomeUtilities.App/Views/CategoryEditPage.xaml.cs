using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Category editor page, see <see cref="EditorPageBase"/>.</summary>
public partial class CategoryEditPage : EditorPageBase
{
    public CategoryEditPage(CategoryEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
