using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Contract editor page (all layouts), see <see cref="EditorPageBase"/>. Without a running editor it loads the
/// contract <c>id</c> (absent = new) of the property <c>propertyId</c>.
/// </summary>
public partial class ContractEditPage : EditorPageBase
{
    public ContractEditPage(ContractEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void PrepareFresh(PaneEditorViewModel editor, IDictionary<string, object> query)
    {
        if (editor is ContractEditViewModel contract
            && query.TryGetValue(EditorNavigation.PropertyIdParameter, out var value))
        {
            contract.PropertyId = value switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => Guid.Empty,
            };
        }
    }
}
