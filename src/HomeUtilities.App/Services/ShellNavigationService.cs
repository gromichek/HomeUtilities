using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ShellNavigationService : INavigationService
{
    private static readonly HashSet<string> SectionRoutes =
    [
        Routes.Overview, Routes.Properties, Routes.Suppliers, Routes.Readings, Routes.Charges, Routes.Bills, Routes.Payments, Routes.Reports, Routes.Help, Routes.Settings,
    ];

    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        var target = SectionRoutes.Contains(route) ? $"//{route}" : route;
        return parameters is null
            ? Shell.Current.GoToAsync(target)
            : Shell.Current.GoToAsync(target, new Dictionary<string, object>(parameters));
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");

    public bool IsShowing(object viewModel) => ReferenceEquals(Shell.Current?.CurrentPage?.BindingContext, viewModel);
}
