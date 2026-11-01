using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// The readings screen. Leaving it with typed but unsaved values (another menu section, Back) asks the standard
/// "Save changes?" question through the central navigation guard (see <c>AppShell</c> and <c>PageBase</c>); pushing
/// the history page does not, the values stay on this page.
/// </summary>
public partial class ReadingsPage : PageBase, IQueryAttributable
{
    public ReadingsPage(ReadingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>
    /// As a pushed page (opened from a charge warning) the query parameters <c>propertyId</c> and <c>date</c> choose the
    /// property and the reading date; as a menu section there are none.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is ReadingsViewModel viewModel
            && query.TryGetValue(EditorNavigation.PropertyIdParameter, out var property)
            && (property is Guid || property is string text && Guid.TryParse(text, out _)))
        {
            var propertyId = property is Guid guid ? guid : Guid.Parse((string)property);
            var date = query.TryGetValue(ReadingsViewModel.DateParameter, out var value) && value is DateOnly parsed ? parsed : DateOnly.FromDateTime(DateTime.Today);
            viewModel.OpenFor(propertyId, date);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Coming back from the browser does not make the page appear again; the window is activated instead.
        if (Window is { } window)
        {
            window.Activated += OnWindowActivated;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Window is { } window)
        {
            window.Activated -= OnWindowActivated;
        }
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (BindingContext is ReadingsViewModel viewModel)
        {
            await viewModel.CheckReturnFromSubmitPageAsync();
        }
    }
}
