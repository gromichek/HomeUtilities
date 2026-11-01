using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// One Settings section as a page. The list hands over a <see cref="SettingsSectionViewModel"/> in the query parameter
/// <c>section</c>; Back returns to the list.
/// </summary>
public partial class SettingsSectionPage : PageBase, IQueryAttributable
{
    public SettingsSectionPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell applies the query again when the page comes back to the top: keep the running view model.
        if (query.TryGetValue(SettingsViewModel.SectionParameter, out var value)
            && value is SettingsSectionViewModel viewModel
            && !ReferenceEquals(BindingContext, viewModel))
        {
            BindingContext = viewModel;
        }
    }
}
