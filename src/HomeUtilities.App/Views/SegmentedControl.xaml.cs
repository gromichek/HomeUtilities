using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>2–3 short, mutually exclusive options that switch a view (Table | Chart), as one bordered group of equal segments.</summary>
public partial class SegmentedControl : ContentView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IEnumerable<ReportOptionViewModel>),
        typeof(SegmentedControl),
        null,
        propertyChanged: (view, _, value) => BindableLayout.SetItemsSource(((SegmentedControl)view).Row, value as IEnumerable<ReportOptionViewModel>));

    public SegmentedControl()
    {
        InitializeComponent();
    }

    public IEnumerable<ReportOptionViewModel>? Items
    {
        get => (IEnumerable<ReportOptionViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
}
