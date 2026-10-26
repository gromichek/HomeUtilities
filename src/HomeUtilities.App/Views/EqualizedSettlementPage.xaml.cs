using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// The settlement of an equalized contract, pushed from the contract editor. Query parameter <c>contractId</c>.
/// Standard Back; there is nothing unsaved on this page.
/// </summary>
public partial class EqualizedSettlementPage : PageBase, IQueryAttributable
{
    public EqualizedSettlementPage(EqualizedSettlementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is EqualizedSettlementViewModel viewModel
            && query.TryGetValue(EditorNavigation.ContractIdParameter, out var value))
        {
            var id = value switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => Guid.Empty,
            };
            if (id != Guid.Empty)
            {
                _ = viewModel.LoadAsync(id);
            }
        }
    }
}
