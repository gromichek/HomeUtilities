using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>Vertical radio rows of a single-choice dialog.</summary>
public partial class RadioChoiceList : ContentView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IEnumerable<ReportOptionViewModel>),
        typeof(RadioChoiceList),
        null,
        propertyChanged: (view, _, value) => BindableLayout.SetItemsSource(((RadioChoiceList)view).Rows, value as IEnumerable<ReportOptionViewModel>));

    public RadioChoiceList()
    {
        InitializeComponent();
    }

    public IEnumerable<ReportOptionViewModel>? Items
    {
        get => (IEnumerable<ReportOptionViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
}
