using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Period dialog of the Reports screen; shown as a popup or inside <see cref="EditorDialogPage"/>.</summary>
public partial class ReportPeriodDialogView : ContentView
{
    public ReportPeriodDialogView()
    {
        InitializeComponent();
        BindingContextChanged += (_, _) => ApplyRange();
    }

    /// <summary>Limits both calendars to the months that have data (only the month of the picked date counts).</summary>
    private void ApplyRange()
    {
        if (BindingContext is not ReportPeriodDialogViewModel dialog)
        {
            return;
        }

        var minimum = dialog.MinimumDate.ToDateTime(TimeOnly.MinValue);
        var maximum = dialog.MaximumDate.ToDateTime(TimeOnly.MinValue);
        foreach (var picker in new[] { FromPicker, ToPicker })
        {
            picker.MinimumDate = minimum;
            picker.MaximumDate = maximum;
        }
    }
}
