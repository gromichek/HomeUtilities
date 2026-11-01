using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// payment editor page, see <see cref="EditorPageBase"/>. A new payment can start from a bill (<c>billId</c>: its
/// contract, the open amount, allocated in full), several bills (<c>billIds</c>, comma-separated, "Pay whole year"), a contract (<c>contractId</c>) or a property (<c>propertyId</c>).
/// </summary>
public partial class PaymentEditPage : EditorPageBase
{
    public PaymentEditPage(PaymentEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void PrepareFresh(PaneEditorViewModel editor, IDictionary<string, object> query)
    {
        if (editor is not PaymentEditViewModel payment)
        {
            return;
        }

        payment.PresetBillId = ReadGuid(query, EditorNavigation.BillIdParameter);
        payment.PresetBillIds = ReadGuids(query, EditorNavigation.BillIdsParameter);
        payment.PresetContractId = ReadGuid(query, EditorNavigation.ContractIdParameter);
        payment.PresetPropertyId = ReadGuid(query, EditorNavigation.PropertyIdParameter);
    }

    private static Guid? ReadGuid(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
            ? value switch
            {
                Guid guid when guid != Guid.Empty => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => null,
            }
            : null;

    private static List<Guid> ReadGuids(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) && value is string text
            ? [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Guid.TryParse(part, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)]
            : [];
}
