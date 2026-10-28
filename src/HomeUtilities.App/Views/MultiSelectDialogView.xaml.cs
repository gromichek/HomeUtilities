using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Check-box dialog (All / None, OK / Cancel); shown as a popup or inside <see cref="EditorDialogPage"/>.</summary>
public partial class MultiSelectDialogView : ContentView
{
    public MultiSelectDialogView()
    {
        InitializeComponent();
    }

    // A tap on the name toggles the box too (the box itself is small on a phone).
    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: MultiSelectItemViewModel item })
        {
            item.IsSelected = !item.IsSelected;
        }
    }
}
