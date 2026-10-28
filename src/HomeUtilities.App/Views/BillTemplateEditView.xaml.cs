using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>The bill template editor; shown in the right-hand pane of <see cref="BillTemplatesPage"/> and on <see cref="BillTemplateEditPage"/>.</summary>
public partial class BillTemplateEditView : ContentView
{
    public BillTemplateEditView()
    {
        InitializeComponent();

        // Only the month and year of the chosen date count; the range is the one of the billing period.
        StartPicker.MinimumDate = ChargesViewModel.EarliestMonth.FirstDay.ToDateTime(TimeOnly.MinValue);
        StartPicker.MaximumDate = ChargesViewModel.LatestMonth.LastDay.ToDateTime(TimeOnly.MinValue);
        EndPicker.MinimumDate = StartPicker.MinimumDate;
        EndPicker.MaximumDate = StartPicker.MaximumDate;
    }
}
