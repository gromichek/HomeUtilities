using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>The bill editor; shown in the right-hand pane of <see cref="BillsPage"/> and on <see cref="BillEditPage"/>.</summary>
public partial class BillEditView : ContentView
{
    public BillEditView()
    {
        InitializeComponent();

        // Only the month and year of the chosen date count; the range is the one of the billing period.
        PeriodPicker.MinimumDate = ChargesViewModel.EarliestMonth.FirstDay.ToDateTime(TimeOnly.MinValue);
        PeriodPicker.MaximumDate = ChargesViewModel.LatestMonth.LastDay.ToDateTime(TimeOnly.MinValue);
    }
}
