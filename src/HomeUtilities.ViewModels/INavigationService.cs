namespace HomeUtilities.ViewModels;

public interface INavigationService
{
    Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null);

    Task GoBackAsync();
}
