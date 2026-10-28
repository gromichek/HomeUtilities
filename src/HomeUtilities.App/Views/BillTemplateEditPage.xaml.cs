using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Compact-layout bill template editor page, see <see cref="EditorPageBase"/>; also opened with a running editor by <see cref="BillTemplateLauncher"/>.</summary>
public partial class BillTemplateEditPage : EditorPageBase
{
    public BillTemplateEditPage(BillTemplateEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
