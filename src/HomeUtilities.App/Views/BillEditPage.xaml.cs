using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Compact-layout bill editor page, see <see cref="EditorPageBase"/>. A new bill can start from a contract (<c>contractId</c>),
/// a property (<c>propertyId</c>), a month (<c>period</c>, <c>yyyy-MM</c>) and a calculated total (<c>gross</c>, shown as a hint) and a kind (<c>kind</c>, a <c>BillKind</c> name).
/// </summary>
public partial class BillEditPage : EditorPageBase
{
    public BillEditPage(BillEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void PrepareFresh(PaneEditorViewModel editor, IDictionary<string, object> query)
    {
        if (editor is not BillEditViewModel bill)
        {
            return;
        }

        bill.PresetContractId = ReadGuid(query, EditorNavigation.ContractIdParameter);
        bill.PresetPropertyId = ReadGuid(query, EditorNavigation.PropertyIdParameter);
        if (query.TryGetValue(BillEditViewModel.PeriodParameter, out var period) && period is string text && YearMonth.TryParse(text, out var month))
        {
            bill.PresetPeriod = month;
        }

        if (query.TryGetValue(BillEditViewModel.KindParameter, out var kind) && kind is string kindName && Enum.TryParse<HomeUtilities.Core.Billing.BillKind>(kindName, out var presetKind))
        {
            bill.PresetKind = presetKind;
        }

        if (query.TryGetValue(BillEditViewModel.GrossParameter, out var gross) && gross is decimal suggested)
        {
            bill.PresetGross = suggested;
        }
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
}
