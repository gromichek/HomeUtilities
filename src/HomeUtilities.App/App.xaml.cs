using Microsoft.Extensions.DependencyInjection;
using HomeUtilities.App.Services;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly LayoutModeService _layoutModeService;

    // AppShell is resolved lazily in CreateWindow: its XAML uses StaticResource colours from App.xaml,
    // which exist only after InitializeComponent() has run. Injecting AppShell into this constructor
    // would build it too early and fail with "StaticResource not found".
    public App(IServiceProvider services, LayoutModeService layoutModeService, IThemeService themeService)
    {
        InitializeComponent();
        _services = services;
        _layoutModeService = layoutModeService;
        themeService.Set(themeService.Current);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        _layoutModeService.Attach(window);
        return window;
    }
}
