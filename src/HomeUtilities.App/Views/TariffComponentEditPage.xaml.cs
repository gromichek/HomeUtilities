using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Tariff component editor page (all layouts), see <see cref="EditorPageBase"/>. Loads the component <c>id</c> (absent = new)
/// of the contract <c>contractId</c>.
/// </summary>
public partial class TariffComponentEditPage : EditorPageBase
{
    public TariffComponentEditPage(TariffComponentEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void PrepareFresh(PaneEditorViewModel editor, IDictionary<string, object> query)
    {
        if (editor is TariffComponentEditViewModel component
            && query.TryGetValue(EditorNavigation.ContractIdParameter, out var value))
        {
            component.ContractId = value switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => Guid.Empty,
            };
        }
    }
}
