namespace HomeUtilities.ViewModels;

public interface INavigationService
{
    Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null);

    Task GoBackAsync();

    /// <summary>True if the page currently on top of the navigation stack is bound to <paramref name="viewModel"/>.</summary>
    bool IsShowing(object viewModel);
}
