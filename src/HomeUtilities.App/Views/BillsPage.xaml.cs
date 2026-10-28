using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// The bills screen. As a menu section it shows the remembered filters; as a pushed page ("All bills" in the contract editor)
/// the query parameters <c>propertyId</c> and <c>contractId</c> choose the property and the contract to highlight.
/// </summary>
public partial class BillsPage : PageBase, IQueryAttributable
{
    public BillsPage(BillsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        TwoPaneLayout.Attach(Root, viewModel, () => viewModel.IsTwoPane);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not BillsViewModel viewModel || !TryGuid(query, EditorNavigation.PropertyIdParameter, out var propertyId))
        {
            return;
        }

        Guid? contractId = TryGuid(query, EditorNavigation.ContractIdParameter, out var contract) ? contract : null;
        var status = query.TryGetValue(BillsViewModel.StatusParameter, out var name) && name is string text && Enum.TryParse<BillStatusFilter>(text, out var parsed)
            ? parsed
            : BillStatusFilter.All;
        int? year = query.TryGetValue(BillsViewModel.YearParameter, out var shown) && shown is int value ? value : null;
        YearMonth? month = query.TryGetValue(BillsViewModel.MonthParameter, out var shownMonth) && shownMonth is string monthText && YearMonth.TryParse(monthText, out var parsedMonth)
            ? parsedMonth
            : null;
        Guid? categoryId = TryGuid(query, BillsViewModel.CategoryParameter, out var category) ? category : null;
        _ = viewModel.OpenForAsync(propertyId, contractId, status, year, month, categoryId);
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
}
