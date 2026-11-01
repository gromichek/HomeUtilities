using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Brings the highlighted card (the item opened last) back into view when a list screen appears again after its editor
/// page was closed (T-028f). Reloading the list puts the list at the top; the item the user came from is scrolled into view.
/// </summary>
public static class ListScroll
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);

    public static void RevealSelected(CollectionView view) =>
        view.Dispatcher.DispatchDelayed(Delay, () =>
        {
            var selected = view.ItemsSource?.Cast<object>().FirstOrDefault(item => item is ISelectableListItem { IsSelected: true });
            if (selected is not null)
            {
                view.ScrollTo(selected, position: ScrollToPosition.MakeVisible, animate: false);
            }
        });
}
