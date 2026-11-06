using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// A date (or month) field: bordered box, calendar icon, the text formatted by the app — <see cref="DateFormat"/> for a full date,
/// or the view model's own <see cref="Text"/> (a month in the app language). The platform date picker lies over it, invisible, and
/// opens on tap. <see cref="Date"/> binds like <c>DatePicker.Date</c> (with the <c>DateOnly</c> converter).
/// </summary>
public partial class DateField : ContentView
{
    public static readonly BindableProperty DateProperty = BindableProperty.Create(nameof(Date), typeof(DateTime?), typeof(DateField), null,
        BindingMode.TwoWay, propertyChanged: (field, _, _) => ((DateField)field).OnDateChanged());

    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(DateField), string.Empty,
        propertyChanged: (field, _, _) => ((DateField)field).UpdateText());

    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime), typeof(DateField), new DateTime(1900, 1, 1),
        propertyChanged: (field, _, value) => ((DateField)field).Picker.MinimumDate = (DateTime)value);

    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime), typeof(DateField), new DateTime(2100, 12, 31),
        propertyChanged: (field, _, value) => ((DateField)field).Picker.MaximumDate = (DateTime)value);

    private bool _syncing;

    public DateField()
    {
        InitializeComponent();
        UpdateText();
    }

    /// <summary>The date; two-way.</summary>
    public DateTime? Date
    {
        get => (DateTime?)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    /// <summary>Text shown instead of the formatted date (a month in the app language); empty = the date as <c>dd.MM.yyyy</c>.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The earliest date the picker offers.</summary>
    public DateTime MinimumDate
    {
        get => (DateTime)GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    /// <summary>The latest date the picker offers.</summary>
    public DateTime MaximumDate
    {
        get => (DateTime)GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    private void OnDateChanged()
    {
        if (!_syncing && Date is { } date)
        {
            _syncing = true;
            try
            {
                Picker.Date = date;
            }
            finally
            {
                _syncing = false;
            }
        }

        UpdateText();
    }

    private void UpdateText() =>
        TextLabel.Text = Text.Length > 0 ? Text : Date is { } date ? DateFormat.Full(date) : string.Empty;

    private void OnPickerDateSelected(object? sender, DateChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        // DatePicker.Date is DateTime? since .NET MAUI 10 (DateTime before): both convert to a nullable here.
        DateTime? picked = Picker.Date;
        if (picked is { } date)
        {
            _syncing = true;
            try
            {
                Date = date;
            }
            finally
            {
                _syncing = false;
            }

            UpdateText();
        }
    }
}
