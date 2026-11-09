using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows.Input;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// The one "pick one of a list" field of the app (UI_GUIDELINES §3a): caption, current value, chevron. A tap opens the app's own
/// choice dialog (never the native picker) and the answer is the **index** of the chosen row — never its display text, so two
/// records with the same name stay apart (R-06).
/// Two ways to use it: bind <see cref="ItemsSource"/> with <see cref="DisplayMember"/> and <see cref="SelectedIndex"/> (or
/// <see cref="SelectedItem"/>) like a Picker, or bind <see cref="Value"/> and <see cref="Command"/> when the view model runs the
/// dialog itself (Reports and the Bills / Payments filters).
/// </summary>
public partial class ChoiceField : ContentView
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(nameof(Caption), typeof(string), typeof(ChoiceField), string.Empty,
        propertyChanged: (field, _, _) => ((ChoiceField)field).OnCaptionChanged());

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(string), typeof(ChoiceField), string.Empty,
        propertyChanged: (field, _, _) => ((ChoiceField)field).UpdateDisplay());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ChoiceField));

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ChoiceField),
        propertyChanged: (field, oldValue, newValue) => ((ChoiceField)field).OnItemsSourceChanged(oldValue as IEnumerable, newValue as IEnumerable));

    public static readonly BindableProperty DisplayMemberProperty = BindableProperty.Create(nameof(DisplayMember), typeof(string), typeof(ChoiceField), string.Empty,
        propertyChanged: (field, _, _) => ((ChoiceField)field).UpdateDisplay());

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(ChoiceField), -1,
        BindingMode.TwoWay, propertyChanged: (field, _, _) => ((ChoiceField)field).OnSelectedIndexChanged());

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ChoiceField), null,
        BindingMode.TwoWay, propertyChanged: (field, _, _) => ((ChoiceField)field).OnSelectedItemChanged());

    public static readonly BindableProperty DialogTitleProperty = BindableProperty.Create(nameof(DialogTitle), typeof(string), typeof(ChoiceField), string.Empty);

    private INotifyCollectionChanged? _observed;
    private INotifyCollectionChanged? _source;
    private bool _isLoaded;
    private bool _syncing;
    private bool _picking;
    private string _displayText = string.Empty;

    public ChoiceField()
    {
        InitializeComponent();

        // The inner layout binds to the control; the control itself keeps the page's binding context.
        Field.BindingContext = this;

        // The collection is observed only while the field is on screen (M-4): a source that outlives the page (a singleton
        // filter state) must not keep the field alive. Loaded subscribes again and shows the current items.
        Loaded += (_, _) =>
        {
            _isLoaded = true;
            Observe();
            UpdateDisplay();
        };
        Unloaded += (_, _) =>
        {
            _isLoaded = false;
            Observe();
        };
    }

    /// <summary>Label above the field; empty = none (the page then has its own label above).</summary>
    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Text of the current value when the view model runs the dialog (with <see cref="Command"/>).</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Run on tap instead of the built-in dialog.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>The options, like <c>Picker.ItemsSource</c>.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Name of the property of an item that holds its text (<c>DisplayName</c>, <c>Name</c>, <c>Text</c>); empty = the item's own string.</summary>
    public string DisplayMember
    {
        get => (string)GetValue(DisplayMemberProperty);
        set => SetValue(DisplayMemberProperty, value);
    }

    /// <summary>Index of the chosen option, -1 = none; two-way.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The chosen option; two-way (for view models that keep the option object instead of an index).</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Title of the choice dialog; empty = the caption, or the text of the label just above the field.</summary>
    public string DialogTitle
    {
        get => (string)GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    /// <summary>True when the caption is shown.</summary>
    public bool HasCaption => Caption.Length > 0;

    /// <summary>The text in the box.</summary>
    public string DisplayText
    {
        get => _displayText;
        private set
        {
            if (_displayText != value)
            {
                _displayText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Screen-reader name: caption and value.</summary>
    public string AccessibleName => string.IsNullOrEmpty(Caption) ? DisplayText : $"{Caption}: {DisplayText}";

    private void OnCaptionChanged()
    {
        OnPropertyChanged(nameof(HasCaption));
        OnPropertyChanged(nameof(AccessibleName));
    }

    private void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        _source = newValue as INotifyCollectionChanged;
        Observe();
        UpdateDisplay();
    }

    // Keeps the subscription in line with "loaded and has an observable source".
    private void Observe()
    {
        var wanted = _isLoaded ? _source : null;
        if (ReferenceEquals(_observed, wanted))
        {
            return;
        }

        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnItemsChanged;
        }

        _observed = wanted;
        if (wanted is not null)
        {
            wanted.CollectionChanged += OnItemsChanged;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateDisplay();

    private void OnSelectedIndexChanged()
    {
        if (!_syncing)
        {
            _syncing = true;
            try
            {
                var items = Items();
                SelectedItem = SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex] : null;
            }
            finally
            {
                _syncing = false;
            }
        }

        UpdateDisplay();
    }

    private void OnSelectedItemChanged()
    {
        if (!_syncing)
        {
            _syncing = true;
            try
            {
                var index = SelectedItem is { } item ? Items().IndexOf(item) : -1;
                if (index != SelectedIndex)
                {
                    SelectedIndex = index;
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        UpdateDisplay();
    }

    private List<object> Items() => ItemsSource is null ? [] : [.. ItemsSource.Cast<object>()];

    private string TextOf(object item)
    {
        if (DisplayMember.Length > 0
            && item.GetType().GetProperty(DisplayMember, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item) is { } text)
        {
            return text.ToString() ?? string.Empty;
        }

        return item.ToString() ?? string.Empty;
    }

    private void UpdateDisplay()
    {
        if (ItemsSource is null)
        {
            DisplayText = Value;
        }
        else
        {
            var items = Items();
            var index = SelectedIndex;
            if (SelectedItem is { } selected && items.IndexOf(selected) is var found && found >= 0)
            {
                index = found;
            }

            DisplayText = index >= 0 && index < items.Count ? TextOf(items[index]) : string.Empty;
        }

        OnPropertyChanged(nameof(AccessibleName));
    }

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled || _picking)
        {
            return;
        }

        if (Command is { } command)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            return;
        }

        if (ItemsSource is null || Handler?.MauiContext?.Services.GetService<IDialogService>() is not { } dialogs)
        {
            return;
        }

        _picking = true;
        try
        {
            var items = Items();
            var picked = await dialogs.ChooseAsync(TitleForDialog(), [.. items.Select(TextOf)], SelectedIndex >= 0 && SelectedIndex < items.Count ? SelectedIndex : null);
            if (picked is { } index && index >= 0 && index < items.Count)
            {
                // The answer is the index; the item is the one at that position, whatever its text.
                SelectedIndex = index;
                SelectedItem = items[index];
            }
        }
        finally
        {
            _picking = false;
        }
    }

    private string TitleForDialog()
    {
        if (DialogTitle.Length > 0)
        {
            return DialogTitle;
        }

        if (Caption.Length > 0)
        {
            return Caption;
        }

        // Forms put a muted label right above the field: it names the choice.
        if (Parent is Layout layout)
        {
            var position = layout.Children.IndexOf(this);
            if (position > 0 && layout.Children[position - 1] is Label label && !string.IsNullOrWhiteSpace(label.Text))
            {
                return label.Text;
            }
        }

        return Shell.Current?.CurrentPage?.Title ?? string.Empty;
    }
}
