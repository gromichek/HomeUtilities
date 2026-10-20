using System.ComponentModel;

namespace HomeUtilities.App.Views;

/// <summary>Sizes the columns of a list + editor page: list ~40% and editor ~60% in two-pane layouts, the list alone otherwise.</summary>
public static class TwoPaneLayout
{
    public static void Attach(Grid root, INotifyPropertyChanged viewModel, Func<bool> isTwoPane)
    {
        void Update()
        {
            var twoPane = isTwoPane();
            root.ColumnDefinitions[0].Width = twoPane ? new GridLength(2, GridUnitType.Star) : GridLength.Star;
            root.ColumnDefinitions[1].Width = twoPane ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
        }

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "IsTwoPane")
            {
                Update();
            }
        };
        Update();
    }
}
