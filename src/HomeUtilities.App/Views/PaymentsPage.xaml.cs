using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// The payments screen. As a menu section it shows the remembered filters; as a pushed page ("All payments" in the contract
/// editor) the query parameters <c>propertyId</c> and <c>contractId</c> choose the property and the contract.
/// </summary>
public partial class PaymentsPage : PageBase, IQueryAttributable
{
    public PaymentsPage(PaymentsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PaymentsViewModel viewModel || !TryGuid(query, EditorNavigation.PropertyIdParameter, out var propertyId))
        {
            return;
        }

        Guid? contractId = TryGuid(query, EditorNavigation.ContractIdParameter, out var contract) ? contract : null;
        _ = viewModel.OpenForAsync(propertyId, contractId);
    }

    private static bool TryGuid(IDictionary<string, object> query, string key, out Guid id)
    {
        id = Guid.Empty;
        return query.TryGetValue(key, out var value)
            && value switch
            {
                Guid guid => (id = guid) != Guid.Empty,
                string text => Guid.TryParse(text, out id),
                _ => false,
            };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ListScroll.RevealSelected(ItemList);
    }
}
