using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace HomeUtilities.App.WinUI;

/// <summary>
/// The inner horizontal ScrollViewer of a report table takes the mouse wheel, so the page stops scrolling while the pointer is over
/// the table. A plain vertical wheel is forwarded to the nearest outer ScrollViewer; Shift + wheel, a horizontal wheel, a
/// touchpad swipe and the scrollbar keep scrolling the table sideways.
/// </summary>
internal static class WheelForwardingWindows
{
    public static void Attach(Microsoft.Maui.Controls.ScrollView scroll)
    {
        if (scroll.Handler?.PlatformView is not ScrollViewer inner)
        {
            return;
        }

        // handledEventsToo: the inner viewer marks the wheel as handled itself.
        inner.RemoveHandler(UIElement.PointerWheelChangedEvent, (PointerEventHandler)OnWheel);
        inner.AddHandler(UIElement.PointerWheelChangedEvent, (PointerEventHandler)OnWheel, true);
    }

    private static void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer inner)
        {
            return;
        }

        var properties = e.GetCurrentPoint(inner).Properties;
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        if (properties.IsHorizontalMouseWheel || shift || properties.MouseWheelDelta == 0)
        {
            return;
        }

        var outer = FindOuterScrollViewer(inner);
        if (outer is null)
        {
            return;
        }

        // The inner viewer may already have moved sideways for this wheel step: put it back.
        var horizontal = inner.HorizontalOffset;
        outer.ChangeView(null, outer.VerticalOffset - properties.MouseWheelDelta, null, true);
        inner.ChangeView(horizontal, null, null, true);
        e.Handled = true;
    }

    private static ScrollViewer? FindOuterScrollViewer(DependencyObject start)
    {
        for (var parent = VisualTreeHelper.GetParent(start); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer { ScrollableHeight: > 0 } viewer)
            {
                return viewer;
            }
        }

        return null;
    }
}
