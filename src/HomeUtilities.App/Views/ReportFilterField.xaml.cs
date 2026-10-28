using System.Windows.Input;

namespace HomeUtilities.App.Views;

/// <summary>A filter field of the Reports screen: caption, current value and a tap that opens the picker dialog.</summary>
public partial class ReportFilterField : ContentView
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(nameof(Caption), typeof(string), typeof(ReportFilterField), string.Empty);

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(string), typeof(ReportFilterField), string.Empty);

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ReportFilterField));

    public ReportFilterField()
    {
        InitializeComponent();

        // The inner layout binds to the control; the control itself keeps the page's binding context.
        Field.BindingContext = this;
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
