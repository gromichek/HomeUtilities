using CommunityToolkit.Maui;
using HomeUtilities.App.Services;
using HomeUtilities.App.Views;
using HomeUtilities.Core;
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
            .UseMauiCommunityToolkit();

        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ILanguageService, LanguageService>();
        builder.Services.AddSingleton<ILocalizer, ResxLocalizer>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
