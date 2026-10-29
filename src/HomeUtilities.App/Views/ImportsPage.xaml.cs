using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>The finished imports (with Undo) and the saved import profiles, pushed from Settings.</summary>
public partial class ImportsPage : PageBase
{
    public ImportsPage(ImportsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
