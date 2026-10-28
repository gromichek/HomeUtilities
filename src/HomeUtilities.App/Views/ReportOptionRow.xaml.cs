using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>A row of selectable buttons (reports, presets, groupings…) or chips (properties, categories).</summary>
public partial class ReportOptionRow : ContentView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IEnumerable<ReportOptionViewModel>),
        typeof(ReportOptionRow),
        null,
        propertyChanged: (view, _, value) => BindableLayout.SetItemsSource(((ReportOptionRow)view).Row, value as IEnumerable<ReportOptionViewModel>));

    public ReportOptionRow()
    {
        InitializeComponent();
    }

    public IEnumerable<ReportOptionViewModel>? Items
    {
        get => (IEnumerable<ReportOptionViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
}
