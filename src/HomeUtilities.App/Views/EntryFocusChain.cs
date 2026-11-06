using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;

namespace HomeUtilities.App.Views;

/// <summary>
/// <c>views:EntryFocusChain.Enabled="True"</c> on an Entry: when the keyboard's Next / Done key is pressed the focus moves to the
/// next chained Entry of the page in screen order (R-49); the last one just closes the keyboard. The order itself, and which key the
/// last field shows, come from the view model (<c>IsLastEntry</c>).
/// </summary>
public static class EntryFocusChain
{
    public static readonly BindableProperty EnabledProperty = BindableProperty.CreateAttached(
        "Enabled",
        typeof(bool),
        typeof(EntryFocusChain),
        false,
        propertyChanged: OnEnabledChanged);

    public static bool GetEnabled(BindableObject view) => (bool)view.GetValue(EnabledProperty);

    public static void SetEnabled(BindableObject view, bool value) => view.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Entry entry)
        {
            return;
        }

        entry.Completed -= OnCompleted;
        if ((bool)newValue)
        {
            entry.Completed += OnCompleted;
        }
    }

    private static void OnCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry current || FindPage(current) is not { } page)
        {
            return;
        }

        var chain = ((IVisualTreeElement)page).GetVisualTreeDescendants()
            .OfType<Entry>()
            .Where(x => GetEnabled(x) && x.IsVisible && x.IsEnabled)
            .ToList();
        var index = chain.IndexOf(current);
        if (index >= 0 && index + 1 < chain.Count)
        {
            chain[index + 1].Focus();
        }
        else
        {
            current.Unfocus();
        }
    }

    private static Page? FindPage(Element element)
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is Page page)
            {
                return page;
            }
        }

        return null;
    }
}
