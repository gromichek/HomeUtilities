using CommunityToolkit.Maui;
using HomeUtilities.App.Services;
using HomeUtilities.App.Views;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Data.Backup;
using HomeUtilities.Data;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;
using Microsoft.Extensions.Logging;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif

namespace HomeUtilities.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => fonts.AddFont("MaterialIconsOutlined-Regular.otf", "MaterialIcons"));

#if WINDOWS
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows =>
            windows.OnWindowCreated(window => HomeUtilities.App.WinUI.WindowCloseGuard.Attach(window))));
#endif

        var appPaths = new AppPaths();
        builder.Services.AddSingleton<IAppPaths>(appPaths);
        var errorLog = new FileErrorLog(appPaths.ErrorLogPath);
        GlobalErrorHandlers.Install(errorLog);
        builder.Services.AddSingleton<IErrorLog>(errorLog);
        builder.Services.AddSingleton<IErrorReporter, ErrorReporter>();
        var clock = new SystemClock();
        builder.Services.AddSingleton<IClock>(clock);

        // A staged restore (or wipe) replaces the database file, so it runs before anything can open the database.
        var backup = new BackupService(
            new BackupOptions(
                appPaths.DatabasePath,
                appPaths.AttachmentsFolder,
                appPaths.RestoreFolder,
                appPaths.BackupsFolder,
                AppInfo.Current.VersionString,
                DeviceInfo.Platform.ToString()),
            clock);
        // Off the UI thread: the restore blocks on async file work, which would deadlock on the UI thread.
        var restore = Task.Run(backup.ApplyPendingRestore).GetAwaiter().GetResult();
        if (restore is { Succeeded: false })
        {
            errorLog.Append("Startup restore", restore.Exception ?? new InvalidOperationException(restore.Message));
        }

        builder.Services.AddSingleton(new StartupNotice { RestoreFailure = restore is { Succeeded: false } ? restore.Message : null });
        builder.Services.AddSingleton<IBackupService>(backup);
        builder.Services.AddSingleton<IBackupFileService, BackupFileService>();
        builder.Services.AddSingleton<IAppLifecycle, AppLifecycle>();
        builder.Services.AddSingleton<ILanguageService, LanguageService>();
        builder.Services.AddSingleton<ILocalizer, ResxLocalizer>();
        builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
        builder.Services.AddHomeUtilitiesData(appPaths.DatabasePath, appPaths.AttachmentsFolder);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<LayoutModeService>();
        builder.Services.AddSingleton<ILayoutModeService>(sp => sp.GetRequiredService<LayoutModeService>());
        builder.Services.AddSingleton<HomeUtilities.ViewModels.IAppInfo, AppInfoService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IActiveEditorProvider, ShellActiveEditorProvider>();
        builder.Services.AddSingleton<INavigationGuard, NavigationGuard>();
        builder.Services.AddSingleton<ISectionSelectionMemory, SectionSelectionMemory>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<ILauncherService, LauncherService>();
        builder.Services.AddSingleton<IAppAssetReader, AppAssetReader>();
        builder.Services.AddSingleton<IHelpContentService, HelpContentService>();
        builder.Services.AddSingleton<IMediaService, MediaService>();
        builder.Services.AddSingleton<IFileViewer, FileViewer>();
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();
        builder.Services.AddSingleton<IToastService, ToastService>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddTransient<OverviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<PlaceholderViewModel>();
        builder.Services.AddTransient<HelpViewModel>();
        builder.Services.AddTransient<PropertiesViewModel>();
        builder.Services.AddTransient<PropertyEditViewModel>();
        builder.Services.AddTransient<Func<PropertyEditViewModel>>(sp => () => sp.GetRequiredService<PropertyEditViewModel>());
        builder.Services.AddTransient<SuppliersViewModel>();
        builder.Services.AddTransient<SupplierEditViewModel>();
        builder.Services.AddTransient<Func<SupplierEditViewModel>>(sp => () => sp.GetRequiredService<SupplierEditViewModel>());
        builder.Services.AddTransient<ContractEditViewModel>();
        builder.Services.AddTransient<Func<ContractEditViewModel>>(sp => () => sp.GetRequiredService<ContractEditViewModel>());
        builder.Services.AddTransient<MeterEditViewModel>();
        builder.Services.AddTransient<Func<MeterEditViewModel>>(sp => () => sp.GetRequiredService<MeterEditViewModel>());
        builder.Services.AddTransient<ReadingsViewModel>();
        builder.Services.AddTransient<ReadingHistoryViewModel>();
        builder.Services.AddTransient<Func<ReadingHistoryViewModel>>(sp => () => sp.GetRequiredService<ReadingHistoryViewModel>());
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<CategoryEditViewModel>();
        builder.Services.AddTransient<Func<CategoryEditViewModel>>(sp => () => sp.GetRequiredService<CategoryEditViewModel>());
        builder.Services.AddTransient<OverviewPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<PlaceholderPage>();
        builder.Services.AddTransient<HelpPage>();
        builder.Services.AddTransient<PropertiesPage>();
        builder.Services.AddTransient<PropertyEditPage>();
        builder.Services.AddTransient<ContractEditPage>();
        builder.Services.AddTransient<MeterEditPage>();
        builder.Services.AddTransient<ReadingsPage>();
        builder.Services.AddTransient<ReadingHistoryPage>();
        builder.Services.AddTransient<SuppliersPage>();
        builder.Services.AddTransient<SupplierEditPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<CategoryEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
