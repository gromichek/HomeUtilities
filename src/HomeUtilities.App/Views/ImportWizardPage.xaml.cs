using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>The Excel import wizard, pushed from Settings.</summary>
public partial class ImportWizardPage : PageBase
{
    public ImportWizardPage(ImportWizardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
