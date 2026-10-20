using System.ComponentModel;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

public partial class PropertiesPage : PageBase
{
    private readonly PropertiesViewModel _viewModel;

    public PropertiesPage(PropertiesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PropertiesViewModel.IsTwoPane))
        {
            UpdateColumns();
        }
    }

    // List on the left (~40%) and the editor on the right in medium/expanded layout; the list alone in compact.
    private void UpdateColumns()
    {
        var twoPane = _viewModel.IsTwoPane;
        Root.ColumnDefinitions[0].Width = twoPane ? new GridLength(2, GridUnitType.Star) : GridLength.Star;
        Root.ColumnDefinitions[1].Width = twoPane ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
    }
}
