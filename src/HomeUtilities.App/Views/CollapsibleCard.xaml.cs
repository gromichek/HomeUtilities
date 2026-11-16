using System.ComponentModel;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// A card with a header row (title, one-line summary, chevron) that opens and closes the card body with a tap (T-057).
/// The state lives in <see cref="Card"/>. The body (the XAML children) is attached to the card only while it is open: while
/// it is collapsed its bindings are not resolved and a bound list builds no row views. The body keeps the binding context of the
/// page (or of the control), so it binds like the content of a plain section card.
/// </summary>
[ContentProperty(nameof(CardContent))]
public partial class CollapsibleCard : ContentView
{
    public static readonly BindableProperty CardProperty = BindableProperty.Create(nameof(Card), typeof(CollapsibleCardViewModel), typeof(CollapsibleCard),
        propertyChanged: (view, _, _) => ((CollapsibleCard)view).OnCardChanged());

    public static readonly BindableProperty CardContentProperty = BindableProperty.Create(nameof(CardContent), typeof(View), typeof(CollapsibleCard),
        propertyChanged: (view, _, _) => ((CollapsibleCard)view).ApplyExpanded());

    private CollapsibleCardViewModel? _observed;
    private bool _isLoaded;

    public CollapsibleCard()
    {
        InitializeComponent();

        // Header and body are inside the control's own tree; the header binds to the card, the body keeps the inherited context.
        Loaded += (_, _) =>
        {
            _isLoaded = true;
            Observe();
            ApplyExpanded();
        };
        Unloaded += (_, _) =>
        {
            _isLoaded = false;
            Observe();
        };
    }

    /// <summary>Title, summary and open/closed state of the card.</summary>
    public CollapsibleCardViewModel? Card
    {
        get => (CollapsibleCardViewModel?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    /// <summary>The body of the card (shown while it is open).</summary>
    public View? CardContent
    {
        get => (View?)GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    private void OnCardChanged()
    {
        Header.BindingContext = Card;
        Observe();
        ApplyExpanded();
    }

    // The card view model is observed only while the control is on screen, so a view model that outlives the page cannot keep it alive.
    private void Observe()
    {
        var wanted = _isLoaded ? Card : null;
        if (ReferenceEquals(_observed, wanted))
        {
            return;
        }

        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnCardPropertyChanged;
        }

        _observed = wanted;
        if (_observed is not null)
        {
            _observed.PropertyChanged += OnCardPropertyChanged;
        }
    }

    private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollapsibleCardViewModel.IsExpanded))
        {
            ApplyExpanded();
        }
    }

    private void ApplyExpanded()
    {
        var open = Card?.IsExpanded == true;
        var content = open ? CardContent : null;
        if (!ReferenceEquals(Body.Content, content))
        {
            Body.Content = content;
        }

        Body.IsVisible = content is not null;
    }
}
