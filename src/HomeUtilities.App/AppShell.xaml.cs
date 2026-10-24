using HomeUtilities.App.Views;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class AppShell : Shell
{
    private readonly INavigationGuard _guard;
    private bool _bypassGuard;

    public AppShell(ShellViewModel viewModel, INavigationGuard guard)
    {
        _guard = guard;
        InitializeComponent();
        Routing.RegisterRoute(Routes.PropertyEdit, typeof(PropertyEditPage));
        Routing.RegisterRoute(Routes.SupplierEdit, typeof(SupplierEditPage));
        Routing.RegisterRoute(Routes.ContractEdit, typeof(ContractEditPage));
        Routing.RegisterRoute(Routes.MeterEdit, typeof(MeterEditPage));
        Routing.RegisterRoute(Routes.ReadingHistory, typeof(ReadingHistoryPage));
        Routing.RegisterRoute(Routes.Categories, typeof(CategoriesPage));
        Routing.RegisterRoute(Routes.CategoryEdit, typeof(CategoryEditPage));
        BindingContext = viewModel;
        Navigating += OnNavigating;
    }

    // One central check for every way out of a page: menu item, toolbar / hardware Back, in-app "..". Pushing a page
    // keeps the current one alive, so it is not guarded.
    private async void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        if (_bypassGuard || e.Source is not (ShellNavigationSource.Pop
            or ShellNavigationSource.PopToRoot
            or ShellNavigationSource.ShellItemChanged
            or ShellNavigationSource.ShellSectionChanged
            or ShellNavigationSource.ShellContentChanged))
        {
            return;
        }

        var sectionSwitch = e.Source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot);
        var source = CurrentItem?.CurrentItem;
        var deferral = e.GetDeferral();
        try
        {
            if (!await _guard.CanLeaveAsync())
            {
                e.Cancel();
                return;
            }

            // Leaving a section closes everything that was pushed inside it, so the menu always opens its list.
            if (sectionSwitch && source is { Stack.Count: > 1 })
            {
                _bypassGuard = true;
                try
                {
                    await source.Navigation.PopToRootAsync(false);
                }
                finally
                {
                    _bypassGuard = false;
                }
            }
        }
        finally
        {
            deferral.Complete();
        }
    }
}
