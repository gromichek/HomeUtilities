using HomeUtilities.App.Services;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly LayoutModeService _layoutModeService;

    public App(AppShell shell, LayoutModeService layoutModeService, IThemeService themeService)
    {
        InitializeComponent();
        _shell = shell;
        _layoutModeService = layoutModeService;
        themeService.Set(themeService.Current);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);
        _layoutModeService.Attach(window);
        return window;
    }
}
