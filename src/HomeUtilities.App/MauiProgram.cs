using CommunityToolkit.Maui;
using HomeUtilities.App.Services;
using HomeUtilities.App.Views;
using HomeUtilities.Core;
using HomeUtilities.Data;
using HomeUtilities.ViewModels;
using Microsoft.Extensions.Logging;

namespace HomeUtilities.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => fonts.AddFont("MaterialSymbolsOutlined.ttf", "MaterialSymbols"));

        var appPaths = new AppPaths();
        builder.Services.AddSingleton<IAppPaths>(appPaths);
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ILanguageService, LanguageService>();
        builder.Services.AddSingleton<ILocalizer, ResxLocalizer>();
        builder.Services.AddHomeUtilitiesData(appPaths.DatabasePath);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<LayoutModeService>();
        builder.Services.AddSingleton<ILayoutModeService>(sp => sp.GetRequiredService<LayoutModeService>());
        builder.Services.AddSingleton<HomeUtilities.ViewModels.IAppInfo, AppInfoService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddTransient<OverviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<PlaceholderViewModel>();
        builder.Services.AddTransient<PropertiesViewModel>();
        builder.Services.AddTransient<PropertyEditViewModel>();
        builder.Services.AddTransient<Func<PropertyEditViewModel>>(sp => () => sp.GetRequiredService<PropertyEditViewModel>());
        builder.Services.AddTransient<OverviewPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<PlaceholderPage>();
        builder.Services.AddTransient<PropertiesPage>();
        builder.Services.AddTransient<PropertyEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
