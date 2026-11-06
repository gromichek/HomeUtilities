using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// <c>views:CompactFill.Enabled="True"</c> on an action button: full width on the compact layout (phone), natural width on wider
/// windows (UI_GUIDELINES §3.3, C-11). Stacked action buttons of a card all get the same width that way.
/// </summary>
public static class CompactFill
{
    public static readonly BindableProperty EnabledProperty = BindableProperty.CreateAttached(
        "Enabled",
        typeof(bool),
        typeof(CompactFill),
        false,
        propertyChanged: OnEnabledChanged);

    public static bool GetEnabled(BindableObject view) => (bool)view.GetValue(EnabledProperty);

    public static void SetEnabled(BindableObject view, bool value) => view.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view)
        {
            return;
        }

        // The messenger holds the button weakly; a repeated registration would throw, so drop an earlier one first.
        WeakReferenceMessenger.Default.Unregister<LayoutModeChangedMessage>(view);
        if ((bool)newValue)
        {
            WeakReferenceMessenger.Default.Register<View, LayoutModeChangedMessage>(view, static (button, message) => Apply(button, message.Mode));
            Apply(view, IPlatformApplication.Current?.Services.GetService<ILayoutModeService>()?.Current ?? LayoutMode.Compact);
        }
    }

    private static void Apply(View view, LayoutMode mode) =>
        view.HorizontalOptions = mode == LayoutMode.Compact ? LayoutOptions.Fill : LayoutOptions.Start;
}
