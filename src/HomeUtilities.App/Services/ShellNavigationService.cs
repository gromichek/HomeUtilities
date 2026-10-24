using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ShellNavigationService : INavigationService
{
    private static readonly HashSet<string> SectionRoutes =
    [
        Routes.Overview, Routes.Properties, Routes.Suppliers, Routes.Readings, Routes.Charges, Routes.Bills, Routes.Payments, Routes.Reports, Routes.Help, Routes.Settings,
    ];

    private readonly NavigationGate _pushGate = new();

    public async Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        if (SectionRoutes.Contains(route))
        {
            await Shell.Current.GoToAsync($"//{route}");
            return;
        }

        // A quick double tap must not push the same page twice.
        await _pushGate.TryRunAsync(() => parameters is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, new Dictionary<string, object>(parameters)));
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");

    public bool IsShowing(object viewModel) => ReferenceEquals(Shell.Current?.CurrentPage?.BindingContext, viewModel);
}
